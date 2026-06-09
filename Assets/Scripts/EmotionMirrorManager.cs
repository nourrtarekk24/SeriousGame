using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// EmotionMirrorManager — Emotion Mirror (Game 3)
///
/// Scene flow:
///   Screen 1 — Emotion selection (7 buttons)
///   Screen 2 — Main game (emotion image + upload + analyse)
///   Screen 3 — Result (scores + success/fail message)
///              → Try Again (both correct and incorrect) → Screen 4
///              → Next Emotion → Screen 1
///              → Back to Hub
///   Screen 4 — Hints + retry upload
/// </summary>
public class EmotionMirrorManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Lumi")]

    [Header("Screen 1 — Emotion Selection")]
    public GameObject selectionScreen;
    public Button[] emotionSelectButtons;   // 7 buttons in order: Happy Sad Fear Angry Surprised Disgusted Neutral
    public Button startButton;

    [Header("Screen 2 — Main Game")]
    public GameObject gameScreen;
    public TextMeshProUGUI emotionPromptText;
    public Image emotionImageDisplay;
    public Sprite[] emotionSprites;         // 7 sprites matching kEmotionNames order
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
    public TextMeshProUGUI confidenceHintText;  // shows "you can do better" if confidence < threshold
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

    // ════════════════════════════════════════════════════════════════════════
    // STATIC DATA
    // ════════════════════════════════════════════════════════════════════════

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
        "Smile as wide as you can.\nRaise your cheeks up high.\nLet your eyes crinkle at the sides.",
        "Pull the corners of your mouth down.\nLet your eyebrows droop in the middle.\nLook downward with your eyes.",
        "Open your eyes as wide as they go.\nRaise your eyebrows up high.\nOpen your mouth slightly.",
        "Push your eyebrows down and together.\nNarrow your eyes.\nPress your lips together tightly.",
        "Raise your eyebrows as high as they go.\nOpen your mouth in a big round O.\nLet your eyes go wide.",
        "Wrinkle your nose up.\nRaise your upper lip slightly.\nPush your eyebrows down a little.",
        "Relax your face completely.\nKeep your mouth gently closed.\nLook straight ahead calmly.",
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

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private int currentEmotionIndex = 0;
    private string uploadedImagePath = null;
    private bool serverAvailable = false;
    private bool analysing = false;
    private bool isHintsMode = false;  // true when on Screen 4

    // ════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

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

        // Apply Arabic localisation if Arabic mode is active
        if (GameManager.IsArabic()) ApplyArabicLocalisation();

        StartCoroutine(AutoStartThenCheck());
    }

    string FixAr(string arabic) => arabic;

    void ApplyArabicLocalisation()
    {

        // Apply Arabic emotion names to selection buttons
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

    IEnumerator AutoStartThenCheck()
    {
        yield return null;
        TryAutoStartServer();
        yield return new WaitForSeconds(4f);
        yield return StartCoroutine(CheckServer());
    }

    void TryAutoStartServer()
    {
        try
        {
            string batPath = FindServerBat();
            if (batPath == null)
            {
                Debug.LogWarning("[EmotionMirror] start_server.bat not found.");
                return;
            }
            Debug.Log("[EmotionMirror] Starting server: " + batPath);
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + batPath + "\"",
                WorkingDirectory = System.IO.Path.GetDirectoryName(batPath),
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EmotionMirror] Could not start server: " + e.Message);
        }
    }

    string FindServerBat()
    {
        var paths = new System.Collections.Generic.List<string>();

        // 1. Regular Desktop
        string desktop = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.Desktop);
        paths.Add(System.IO.Path.Combine(desktop, "SunvaleEmotionServer", "start_server.bat"));

        // 2. OneDrive Desktop (Windows 11 with OneDrive sync)
        string profile = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.UserProfile);
        paths.Add(System.IO.Path.Combine(profile, "OneDrive", "Desktop",
            "SunvaleEmotionServer", "start_server.bat"));

        // 3. Next to the game .exe
        try
        {
            string exeDir = System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            paths.Add(System.IO.Path.Combine(exeDir, "SunvaleEmotionServer", "start_server.bat"));
        }
        catch { }

        // 4. Next to Unity project (Editor)
        paths.Add(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Application.dataPath),
            "SunvaleEmotionServer", "start_server.bat"));

        foreach (string p in paths)
        {
            Debug.Log("[EmotionMirror] Checking: " + p);
            if (System.IO.File.Exists(p)) return p;
        }
        return null;
    }
    // ════════════════════════════════════════════════════════════════════════
    // SCREEN MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════

    void ShowScreen(int screen)
    {
        if (selectionScreen != null) selectionScreen.SetActive(screen == 1);
        if (gameScreen != null) gameScreen.SetActive(screen == 2);
        if (resultScreen != null) resultScreen.SetActive(screen == 3);
        if (hintsScreen != null) hintsScreen.SetActive(screen == 2 || screen == 4);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SCREEN 1 — EMOTION SELECTION
    // ════════════════════════════════════════════════════════════════════════

    public void OnEmotionButtonPressed(int index)
    {
        currentEmotionIndex = index;

        // Highlight selected button, reset others
        for (int i = 0; i < emotionSelectButtons.Length; i++)
        {
            if (emotionSelectButtons[i] == null) continue;
            var img = emotionSelectButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == index)
                    ? new Color(0.23f, 0.43f, 0.07f, 1f)   // selected green
                    : new Color(0.95f, 0.95f, 0.92f, 1f);  // unselected

            var label = emotionSelectButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.color = (i == index) ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);

            // Bounce animation
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
        // Populate hints for selected emotion before showing screen
        if (hintsText != null)
            hintsText.text = kHints[currentEmotionIndex];
        if (hintsEmotionText != null)
            hintsEmotionText.text = kEmotionPrompts[currentEmotionIndex];
        ShowScreen(2);
        ShowCurrentEmotion();
    }

    // ════════════════════════════════════════════════════════════════════════
    // SCREEN 2 — MAIN GAME
    // ════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════
    // SCREEN 3 — RESULT
    // ════════════════════════════════════════════════════════════════════════

    public void OnTryAgainPressed()
    {
        // Go to Screen 4 — hints + retry
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

        // Reset hints screen upload state
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
        // Reset selection highlight
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

    // ════════════════════════════════════════════════════════════════════════
    // SCREEN 4 — HINTS + RETRY
    // ════════════════════════════════════════════════════════════════════════

    public void OnHintsUploadPressed() => StartCoroutine(OpenFilePicker(true));
    public void OnHintsAnalysePressed() => StartCoroutine(AnalyseImage(true));

    // ════════════════════════════════════════════════════════════════════════
    // FILE PICKER
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator OpenFilePicker(bool hintsMode)
    {
        string tempPath = Path.Combine(
            Application.temporaryCachePath, "selected_image.txt");
        if (File.Exists(tempPath)) File.Delete(tempPath);

        // Exit fullscreen so the Windows file dialog appears on top of Unity.
        // In a fullscreen build the dialog opens behind the game — invisible.
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

        // Restore fullscreen after dialog closes
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

    // ════════════════════════════════════════════════════════════════════════
    // ANALYSE
    // ════════════════════════════════════════════════════════════════════════

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

        // ── Delete photo immediately after analysis ───────────────────────
        // The result is now stored as text. The image is no longer needed.
        // This protects child privacy — photos never persist after analysis.
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
        // ─────────────────────────────────────────────────────────────────

        ShowScreen(3);
        ShowResult(response, scores);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RESULT DISPLAY
    // ════════════════════════════════════════════════════════════════════════

    void ShowResult(EmotionResponse response,
                    Dictionary<string, float> scores)
    {
        string targetEmotion = kEmotionNames[currentEmotionIndex];
        string detectedEmotion = response.emotion;
        bool matched = string.Equals(detectedEmotion, targetEmotion,
                             System.StringComparison.OrdinalIgnoreCase);
        float confidence = response.confidence;

        // Three-tier confidence evaluation:
        // Tier 1 (>=70%, correct emotion): fully successful
        // Tier 2 (50-69%, correct emotion): accepted, encourage clarity
        // Tier 3 (<50% or wrong emotion):  incorrect, prompt to retry
        bool tier1 = matched && confidence >= 70f;
        bool tier2 = matched && confidence >= 50f && confidence < 70f;

        bool arabic = GameManager.IsArabic();

        // Animate result panel in
        if (resultScreen != null)
        {
            resultScreen.transform.localScale = Vector3.zero;
            resultScreen.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        // Icon
        if (resultIcon != null)
        {
            resultIcon.sprite = (tier1 || tier2) ? successSprite : failSprite;
            resultIcon.color = tier1 ? successColor
                              : tier2 ? new Color(0.85f, 0.50f, 0.07f, 1f)
                              : failColor;
        }

        // Result message
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

        // Dominant + confidence
        if (confidenceText != null)
            confidenceText.text = "Detected: " + detectedEmotion +
                                  "  (" + confidence.ToString("F0") + "%)";

        // Confidence hint text
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

        // All scores as bar chart
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

        // Try Again button -- always visible
        if (tryAgainButton != null) tryAgainButton.gameObject.SetActive(true);

        // Tier 1 and Tier 2 both count as accepted/correct for analytics
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

        // Append result entry
        string entry = matched
            ? target + ": correct (" + confidence.ToString("F0") + "%)"
            : target + ": incorrect → " + detected + " detected (" + confidence.ToString("F0") + "%)";
        string existing = PlayerPrefs.GetString(prefix + "Results_" + playerName, "");
        string allResults = string.IsNullOrEmpty(existing) ? entry : existing + "|" + entry;
        PlayerPrefs.SetString(prefix + "Results_" + playerName, allResults);

        // ── Send to Firebase ─────────────────────────────────────────
        // improvements tracking: if this emotion was previously incorrect
        // and is now correct, it counts as an improvement
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
        // Send to Firebase as a proper session document
        if (FirebaseManager.Instance != null)
        {
            string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            FirebaseManager.Instance.SendEmotionMirrorSession(
                playerName, sessionId, target, detected, matched, confidence);
        }


        PlayerPrefs.Save();

    }

    // ════════════════════════════════════════════════════════════════════════
    // SERVER CHECK
    // ════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════
    // JSON HELPERS
    // ════════════════════════════════════════════════════════════════════════

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