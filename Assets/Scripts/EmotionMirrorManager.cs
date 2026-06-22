using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class EmotionMirrorManager : MonoBehaviour
{

    [Header("Lumi")]

    [Header("Screen 1 — Emotion Selection")]
    public GameObject selectionScreen;
    public Button[] emotionSelectButtons;
    public Button startButton;

    [Header("Screen 2 — Main Game")]
    public GameObject gameScreen;
    public TextMeshProUGUI emotionPromptText;
    public Image emotionImageDisplay;
    public Sprite[] emotionSprites;
    public Button uploadButton;
    public TextMeshProUGUI uploadStatusText;
    public RawImage previewImage;
    public Button analyseButton;

    [Header("Screen 3 — Result")]
    public GameObject resultScreen;
    public Image resultIcon;
    public Sprite successSprite;
    public Sprite failSprite;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI confidenceText;
    public TextMeshProUGUI scoresText;
    public TextMeshProUGUI confidenceHintText;
    public Button tryAgainButton;
    public Button nextEmotionButton;

    [Header("Screen 4 — Hints + Retry")]
    public GameObject hintsScreen;
    public TextMeshProUGUI hintsEmotionText;
    public Image hintsImageDisplay;
    public TextMeshProUGUI hintsText;
    public Button hintsUploadButton;
    public TextMeshProUGUI hintsUploadStatusText;
    public RawImage hintsPreviewImage;
    public Button hintsAnalyseButton;

    [Header("Server")]
    public TextMeshProUGUI serverStatusText;
    public string serverUrl = "http://localhost:5050";

    [Header("Colors")]
    public Color successColor = new Color(0.24f, 0.65f, 0.24f, 1f);
    public Color failColor = new Color(0.84f, 0.27f, 0.27f, 1f);

    private static readonly string[] kEmotionNames =
        { "Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted", "Neutral" };

    private static readonly string[] kEmotionNamesAr =
        { "سَعِيد", "حَزِين", "خَائِف", "غَاضِب", "مُتَفَاجِئ", "مُشْمَئِزّ", "مُحَايِد" };

    private static readonly string[] kEmotionPromptsAr = {
        "!اِصْنَعْ وَجْهَ السَّعَادَة",
        "!اِصْنَعْ وَجْهَ الْحُزْن",
        "!اِصْنَعْ وَجْهَ الْخَوف",
        "!اِصْنَعْ وَجْهَ الْغَضَب",
        "!اِصْنَعْ وَجْهَ الدَّهْشَة",
        "!اِصْنَعْ وَجْهَ الِاشْمِئْزَاز",
        ".اِصْنَعْ وَجْهًا مُحَايِدًا",
    };

    private static readonly string[] kEmotionPrompts = {
        "Make a HAPPY face!",
        "Make a SAD face!",
        "Make a SCARED face!",
        "Make an ANGRY face!",
        "Make a SURPRISED face!",
        "Make a DISGUSTED face!",
        "Make a NEUTRAL face.",
    };

    private static readonly string[] kHints = {

        "Look at the corners of their mouth — they are pulled up into a wide smile.\nLook at their cheeks — they are raised and round.\nLook at their eyes — they are slightly squinted from smiling.",

        "Look at the corners of their mouth — they are pulled downward.\nLook at their eyebrows — the inner corners are raised and drooping.\nLook at their eyes — they look heavy and downcast.",

        "Look at their eyes — they are opened very wide.\nLook at their eyebrows — both are raised high and pulled together.\nLook at their mouth — it is slightly open, like they are holding their breath.",

        "Look at their eyebrows — they are pulled down and pushed together in the middle.\nLook at their eyes — they are narrowed and intense.\nLook at their mouth — the lips are pressed tightly together.",

        "Look at their eyebrows — they are raised as high as possible.\nLook at their eyes — they are wide open.\nLook at their mouth — it is dropped open in a round O shape.",

        "Look at their nose — it is wrinkled and scrunched upward.\nLook at their upper lip — it is curled up on one side.\nLook at their eyes — they are slightly narrowed, like they are pulling away from something.",

        "Look at their mouth — it is gently closed with no smile or frown.\nLook at their eyebrows — they are relaxed and flat.\nLook at their eyes — they are calm and looking straight ahead.",
    };

    private static readonly string[] kSuccessMessages = {
        "Well done! That really looks {0}!",
        "Amazing! You nailed the {0} face!",
        "Yes! That is exactly what {0} looks like!",
        "Brilliant! You made a perfect {0} face!",
    };

    private static readonly string[] kFailMessages = {
        "Good try! The photo looked more like {0}.\nThe target was {1} — try again!",
        "Almost! The photo showed {0} instead of {1}.\nHave another go!",
        "Not quite! Try making an even stronger {1} face.",
    };

    private int currentEmotionIndex = 0;
    private string uploadedImagePath = null;
    private bool serverAvailable = false;
    private bool analysing = false;
    private bool isHintsMode = false;

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (LLMService.Instance == null)
            new GameObject("LLMService").AddComponent<LLMService>();
    }

    void Start()
    {
        ShowScreen(1);

        if (GameManager.IsArabic()) ApplyArabicLocalisation();

        StartCoroutine(CheckServer());
    }

    string FixAr(string arabic)
    {
        if (string.IsNullOrEmpty(arabic)) return arabic;
        return ArabicSupport.ArabicFixer.Fix(arabic);
    }

    void ApplyArabicLocalisation()
    {

        for (int i = 0; i < emotionSelectButtons.Length; i++)
        {
            if (i >= kEmotionNamesAr.Length) break;
            if (emotionSelectButtons[i] == null) continue;
            var label = emotionSelectButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = FixAr(kEmotionNamesAr[i]);
            }
        }
    }

    void ShowScreen(int screen)
    {
        if (selectionScreen != null) selectionScreen.SetActive(screen == 1);
        if (gameScreen != null) gameScreen.SetActive(screen == 2);
        if (resultScreen != null) resultScreen.SetActive(screen == 3);
        if (hintsScreen != null) hintsScreen.SetActive(screen == 2 || screen == 4);
    }

    public void OnEmotionButtonPressed(int index)
    {
        currentEmotionIndex = index;

        for (int i = 0; i < emotionSelectButtons.Length; i++)
        {
            if (emotionSelectButtons[i] == null) continue;
            var img = emotionSelectButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == index)
                    ? new Color(0.23f, 0.43f, 0.07f, 1f)
                    : new Color(0.95f, 0.95f, 0.92f, 1f);

            var label = emotionSelectButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.color = (i == index) ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);

            if (i == index)
            {
                emotionSelectButtons[i].transform.localScale = Vector3.one;
                emotionSelectButtons[i].transform
                    .DOScale(1.1f, 0.12f).SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                        emotionSelectButtons[index].transform
                            .DOScale(1f, 0.15f).SetEase(Ease.OutBounce));
            }
        }

        if (startButton != null) startButton.interactable = true;
    }

    public void OnStartPressed()
    {
        ResetGameScreen();

        if (hintsText != null)
            hintsText.text = kHints[currentEmotionIndex];
        if (hintsEmotionText != null)
            hintsEmotionText.text = kEmotionPrompts[currentEmotionIndex];
        ShowScreen(2);
        ShowCurrentEmotion();
    }

    void ShowCurrentEmotion()
    {
        if (emotionPromptText != null)
        {
            bool arabic = GameManager.IsArabic();
            emotionPromptText.text = arabic
                ? FixAr(kEmotionPromptsAr[currentEmotionIndex])
                : kEmotionPrompts[currentEmotionIndex];
        }

        if (emotionImageDisplay != null && emotionSprites != null
            && currentEmotionIndex < emotionSprites.Length
            && emotionSprites[currentEmotionIndex] != null)
        {
            emotionImageDisplay.sprite = emotionSprites[currentEmotionIndex];
            emotionImageDisplay.preserveAspect = true;
        }
    }

    void ResetGameScreen()
    {
        uploadedImagePath = null;
        isHintsMode = false;
        if (uploadStatusText != null) uploadStatusText.text = "No photo uploaded yet.";
        if (previewImage != null) previewImage.gameObject.SetActive(false);
        if (analyseButton != null) analyseButton.interactable = false;
    }

    public void OnUploadPressed() => StartCoroutine(OpenFilePicker(false));
    public void OnAnalysePressed() => StartCoroutine(AnalyseImage(false));

    public void OnTryAgainPressed()
    {

        if (hintsEmotionText != null)
            hintsEmotionText.text = kEmotionPrompts[currentEmotionIndex];

        if (hintsImageDisplay != null && emotionSprites != null
            && currentEmotionIndex < emotionSprites.Length
            && emotionSprites[currentEmotionIndex] != null)
        {
            hintsImageDisplay.sprite = emotionSprites[currentEmotionIndex];
            hintsImageDisplay.preserveAspect = true;
        }

        if (hintsText != null)
            hintsText.text = kHints[currentEmotionIndex];

        uploadedImagePath = null;
        isHintsMode = true;
        if (hintsUploadStatusText != null) hintsUploadStatusText.text = "No photo uploaded yet.";
        if (hintsPreviewImage != null) hintsPreviewImage.gameObject.SetActive(false);
        if (hintsAnalyseButton != null) hintsAnalyseButton.interactable = false;

        ShowScreen(4);
    }

    public void OnNextEmotionPressed()
    {
        ShowScreen(1);

        for (int i = 0; i < emotionSelectButtons.Length; i++)
        {
            if (emotionSelectButtons[i] == null) continue;
            var img = emotionSelectButtons[i].GetComponent<Image>();
            if (img != null) img.color = new Color(0.95f, 0.95f, 0.92f, 1f);
            var label = emotionSelectButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        }
        if (startButton != null) startButton.interactable = false;
    }

    public void OnBackPressed() => SceneManager.LoadScene("HubScene");
    public void OnBackToSelectionPressed() => ShowScreen(1);
    public void OnBackToSelectionFromResult() => ShowScreen(1);

    public void OnHintsUploadPressed() => StartCoroutine(OpenFilePicker(true));
    public void OnHintsAnalysePressed() => StartCoroutine(AnalyseImage(true));

    IEnumerator OpenFilePicker(bool hintsMode)
    {
        string tempPath = Path.Combine(
            Application.temporaryCachePath, "selected_image.txt");
        if (File.Exists(tempPath)) File.Delete(tempPath);

        bool wasFullscreen = Screen.fullScreen;
        if (wasFullscreen)
        {
            Screen.fullScreen = false;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.4f);
        }

        string script =
            "Add-Type -AssemblyName System.Windows.Forms;" +
            "$d = New-Object System.Windows.Forms.OpenFileDialog;" +
            "$d.Filter = 'Image Files|*.jpg;*.jpeg;*.png;*.bmp';" +
            "$d.Title = 'Select a photo of the child';" +
            "if($d.ShowDialog() -eq 'OK'){ $d.FileName | Out-File -FilePath '" +
            tempPath.Replace("\\", "\\\\") + "' -Encoding UTF8 }";

        var proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = "powershell.exe";
        proc.StartInfo.Arguments = "-Command \"" + script + "\"";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        proc.Start();

        float waited = 0f;
        while (!proc.HasExited && waited < 60f) { waited += Time.deltaTime; yield return null; }

        if (wasFullscreen)
        {
            yield return new WaitForSeconds(0.2f);
            Screen.fullScreen = true;
            yield return new WaitForSeconds(0.3f);
        }

        if (!File.Exists(tempPath)) yield break;
        string path = File.ReadAllText(tempPath).Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) yield break;

        uploadedImagePath = path;

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        RawImage preview = hintsMode ? hintsPreviewImage : previewImage;
        TMP_Text statusTxt = hintsMode ? hintsUploadStatusText : uploadStatusText;
        Button analyseBtn = hintsMode ? hintsAnalyseButton : analyseButton;

        if (preview != null)
        {
            preview.texture = tex;
            preview.gameObject.SetActive(true);
            float aspect = (float)tex.width / tex.height;
            var rt = preview.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.y * aspect, rt.sizeDelta.y);
        }

        if (statusTxt != null) statusTxt.text = "Photo loaded: " + Path.GetFileName(path);
        if (analyseBtn != null) analyseBtn.interactable = serverAvailable;
    }

    IEnumerator AnalyseImage(bool hintsMode)
    {
        if (analysing || uploadedImagePath == null) yield break;
        analysing = true;

        Button analyseBtn = hintsMode ? hintsAnalyseButton : analyseButton;
        TMP_Text statusTxt = hintsMode ? hintsUploadStatusText : uploadStatusText;

        if (analyseBtn != null) analyseBtn.interactable = false;
        if (statusTxt != null) statusTxt.text = "Analysing...";

        string body = "{\"path\": \"" +
                      uploadedImagePath.Replace("\\", "\\\\") + "\"}";

        using var req = new UnityEngine.Networking.UnityWebRequest(
            serverUrl + "/detect", "POST");
        req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(
            Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 30;
        yield return req.SendWebRequest();

        analysing = false;

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            if (statusTxt != null)
                statusTxt.text = "Could not reach server. Is start_server.bat running?";
            if (analyseBtn != null) analyseBtn.interactable = true;
            yield break;
        }

        string rawJson = req.downloadHandler.text;
        var response = JsonUtility.FromJson<EmotionResponse>(rawJson);
        var scores = ParseScores(rawJson);

        try
        {
            if (!string.IsNullOrEmpty(uploadedImagePath) &&
                System.IO.File.Exists(uploadedImagePath))
            {
                System.IO.File.Delete(uploadedImagePath);
                Debug.Log("[Privacy] Photo deleted after analysis.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Privacy] Could not delete photo: " + e.Message);
        }
        uploadedImagePath = null;

        ShowScreen(3);
        ShowResult(response, scores);
    }

    void ShowResult(EmotionResponse response,
                    Dictionary<string, float> scores)
    {
        string targetEmotion = kEmotionNames[currentEmotionIndex];
        string detectedEmotion = response.emotion;
        bool matched = string.Equals(detectedEmotion, targetEmotion,
                             System.StringComparison.OrdinalIgnoreCase);
        float confidence = response.confidence;

        bool tier1 = matched && confidence >= 70f;
        bool tier2 = matched && confidence >= 50f && confidence < 70f;

        bool arabic = GameManager.IsArabic();

        if (resultScreen != null)
        {
            resultScreen.transform.localScale = Vector3.zero;
            resultScreen.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        if (resultIcon != null)
        {
            resultIcon.sprite = (tier1 || tier2) ? successSprite : failSprite;
            resultIcon.color = tier1 ? successColor
                              : tier2 ? new Color(0.85f, 0.50f, 0.07f, 1f)
                              : failColor;
        }

        if (resultText != null)
        {
            if (tier1)
            {
                string t = kSuccessMessages[Random.Range(0, kSuccessMessages.Length)];
                resultText.text = string.Format(t, targetEmotion);
                resultText.color = successColor;
            }
            else if (tier2)
            {
                resultText.text = arabic
                    ? ArabicSupport.ArabicFixer.Fix("احسنت! حاول ان تعبر بشكل اوضح!")
                    : "Good effort! Try to express the emotion a little more clearly.";
                resultText.color = new Color(0.85f, 0.50f, 0.07f, 1f);
            }
            else
            {
                string t = kFailMessages[Random.Range(0, kFailMessages.Length)];
                resultText.text = string.Format(t, detectedEmotion, targetEmotion);
                resultText.color = failColor;
            }
        }

        if (confidenceText != null)
            confidenceText.text = "Detected: " + detectedEmotion +
                                  "  (" + confidence.ToString("F0") + "%)";

        if (confidenceHintText != null)
        {
            if (tier2)
            {
                confidenceHintText.text = arabic
                    ? ArabicSupport.ArabicFixer.Fix("قريب جدا! حاول ان تبالغ في التعبير قليلا!")
                    : "So close! Try to exaggerate the expression a little more.";
                confidenceHintText.color = new Color(0.85f, 0.50f, 0.07f, 1f);
                confidenceHintText.gameObject.SetActive(true);
            }
            else
            {
                confidenceHintText.gameObject.SetActive(false);
            }
        }

        if (scoresText != null && scores != null && scores.Count > 0)
        {
            var sorted = new List<KeyValuePair<string, float>>(scores);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new StringBuilder();
            foreach (var kvp in sorted)
            {
                int filled = Mathf.RoundToInt(kvp.Value / 12.5f);
                filled = Mathf.Clamp(filled, 0, 8);
                string bar = new string('\u2588', filled) + new string('\u2591', 8 - filled);
                sb.AppendLine(kvp.Key.PadRight(11) + "  " + bar + "   " +
                              kvp.Value.ToString("F0") + "%");
            }
            scoresText.text = sb.ToString();
        }

        if (tryAgainButton != null) tryAgainButton.gameObject.SetActive(true);

        bool accepted = tier1 || tier2;
        SaveEmotionMirrorResult(targetEmotion, detectedEmotion, accepted, confidence);
    }

    void SaveEmotionMirrorResult(string target, string detected, bool matched, float confidence)
    {
        string playerName = GameManager.Instance != null
            ? GameManager.Instance.lumiName : "Unknown";

        string prefix = "EmotionMirror_";
        PlayerPrefs.SetInt(prefix + "Played_" + playerName, 1);

        int total = PlayerPrefs.GetInt(prefix + "Total_" + playerName, 0) + 1;
        int correct = PlayerPrefs.GetInt(prefix + "Correct_" + playerName, 0) + (matched ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "Total_" + playerName, total);
        PlayerPrefs.SetInt(prefix + "Correct_" + playerName, correct);

        string entry = matched
            ? target + ": correct (" + confidence.ToString("F0") + "%)"
            : target + ": incorrect → " + detected + " detected (" + confidence.ToString("F0") + "%)";
        string existing = PlayerPrefs.GetString(prefix + "Results_" + playerName, "");
        string allResults = string.IsNullOrEmpty(existing) ? entry : existing + "|" + entry;
        PlayerPrefs.SetString(prefix + "Results_" + playerName, allResults);

        string improvements = "";
        if (matched)
        {
            bool wasWrongBefore = existing.Contains(target + ": incorrect");
            if (wasWrongBefore) improvements = target;
        }

        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SendEmotionMirrorResult(
                playerName,
                total,
                correct,
                allResults,
                improvements
            );
        }

        if (FirebaseManager.Instance != null)
        {
            string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            FirebaseManager.Instance.SendEmotionMirrorSession(
                playerName, sessionId, target, detected, matched, confidence);
        }

        PlayerPrefs.Save();

    }

    IEnumerator CheckServer()
    {
        if (serverStatusText != null) serverStatusText.text = "Checking server...";
        using var req = UnityEngine.Networking.UnityWebRequest.Get(serverUrl + "/health");
        req.timeout = 3;
        yield return req.SendWebRequest();
        serverAvailable = req.result ==
            UnityEngine.Networking.UnityWebRequest.Result.Success;
        if (serverStatusText != null)
        {
            serverStatusText.text = serverAvailable ? "" :
                "Server offline — start start_server.bat first.";
            serverStatusText.color = serverAvailable ? successColor : failColor;
        }
    }

    [System.Serializable]
    class EmotionResponse
    {
        public string emotion;
        public float confidence;
        public bool face_found;
        public string error;
    }

    static Dictionary<string, float> ParseScores(string json)
    {
        var dict = new Dictionary<string, float>();
        try
        {
            int start = json.IndexOf("\"scores\":{");
            if (start < 0) return dict;
            start = json.IndexOf("{", start + 9);
            int end = json.IndexOf("}", start);
            string block = json.Substring(start + 1, end - start - 1);
            foreach (string pair in block.Split(','))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;
                string key = kv[0].Trim().Trim('"');
                if (float.TryParse(kv[1].Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float val))
                    dict[key] = val;
            }
        }
        catch { }
        return dict;
    }
}