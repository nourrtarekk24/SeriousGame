using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Image = UnityEngine.UI.Image;

public class RoundManager2 : MonoBehaviour
{

    [Header("Level Settings")]
    public int currentLevel = 0;

    [Header("Emotion Image")]
    public GameObject emotionImagePanel;
    public Image emotionImage;

    [Header("Question")]
    public GameObject questionText;

    [Header("Answer Container")]
    public GameObject answerContainer;
    public GameObject answerButtonPrefab;

    [Header("Lumi")]
    public GameObject lumiCorner;
    public GameObject speechBubble;
    public TextMeshProUGUI speechText;

    [Header("Lumi Characters")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    [Header("Demo")]
    public GameObject demoSkipBtn;

    [Header("Reveal Panel")]
    public GameObject revealPanel;
    public Image revealEmoji;
    public TextMeshProUGUI revealEmotionName;
    public TextMeshProUGUI revealTitle;

    [Header("HUD")]
    public Image[] heartImages;
    public Sprite heartFull;
    public Sprite heartEmpty;
    public TextMeshProUGUI coinText;

    [Header("Emotion Images")]
    public Sprite[] emotionSprites;

    [Header("Emoji Sprites")]
    public Sprite[] emojiSprites;

    [Header("Lumi Voice — TTS")]
    public AudioSource lumiAudio;

    [Header("LLM Settings")]
    [Tooltip("Seconds — if the child takes longer than this without answering, show a proactive hint")]
    public float slowResponseThreshold = 8f;

    private readonly string[] emotionNames = {
        "Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted", "Neutral"
    };

    private string _lastRawArabic = null;

    string L(string english, string arabic)
    {
        if (!GameManager.IsArabic()) { _lastRawArabic = null; return english; }
        _lastRawArabic = arabic;
        return ShapeArabic(arabic);
    }

    string AR(string arabic)
    {
        _lastRawArabic = arabic;
        return ShapeArabic(arabic);
    }

    string FixAr(string arabic)
        => ShapeArabic(arabic);

    string ShapeArabic(string arabic)
    {
        if (string.IsNullOrEmpty(arabic)) return arabic;
        return ArabicSupport.ArabicFixer.Fix(arabic);
    }

    string ShapeArabicLines(string arabic)
    {
        if (string.IsNullOrEmpty(arabic)) return arabic;
        if (!arabic.Contains("\n")) return ArabicSupport.ArabicFixer.Fix(arabic);
        var lines = arabic.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = ArabicSupport.ArabicFixer.Fix(lines[i]);
        return string.Join("\n", lines);
    }

    private readonly string[] emotionNamesAr = {
        "سَعِيد", "حَزِين", "خَائِف", "غَاضِب", "مُتَفَاجِئ", "مُشْمَئِزّ", "مُحَايِد"
    };

    string EmotionName(int idx)
    {
        if (idx < 0) return "";
        var arr = GameManager.IsArabic() ? emotionNamesAr : emotionNames;
        string name = idx < arr.Length ? arr[idx] : "";
        if (GameManager.IsArabic() && !string.IsNullOrEmpty(name))
            name = ArabicSupport.ArabicFixer.Fix(name);
        return name;
    }

    string EmotionNameRaw(int idx)
    {
        if (idx < 0) return "";
        var arr = GameManager.IsArabic() ? emotionNamesAr : emotionNames;
        return idx < arr.Length ? arr[idx] : "";
    }

    private readonly int[][] levelAnswerOptions = {
        new int[] { 0, 1 },
        new int[] { 1, 3 },
        new int[] { 2, 4, 0 },
        new int[] { 3, 5, 1 },
        new int[] { 4, 2, 0, 3 },
        new int[] { 5, 3, 1, 0 }
    };

    private readonly int[] correctEmotionPerLevel = { 0, 1, 2, 3, 4, 5 };

    private readonly Color[] emotionColors = {
        new Color(0.98f, 0.93f, 0.60f, 1f),
        new Color(0.72f, 0.85f, 0.98f, 1f),
        new Color(0.70f, 0.90f, 0.70f, 1f),
        new Color(0.98f, 0.55f, 0.35f, 1f),
        new Color(0.65f, 0.92f, 0.88f, 1f),
        new Color(0.78f, 0.65f, 0.92f, 1f),
        new Color(0.97f, 0.97f, 0.97f, 1f),
    };

    private readonly string[][] situationStoriesMulti = {
        new[] { "Think about how you feel\nwhen you open a birthday present!", "Think about how you feel\nwhen your favourite cartoon comes on!", "Think about how you feel\nwhen you get your favourite ice cream!", "Think about how you feel\nwhen a friend comes to play with you!" },
        new[] { "Think about how you feel\nwhen you drop your ice cream!", "Think about how you feel\nwhen your best friend has to go home!", "Think about how you feel\nwhen your favourite toy breaks!", "Think about how you feel\nwhen you miss someone you love!" },
        new[] { "Think about how you feel\nwhen the lights go off at night!", "Think about how you feel\nwhen you hear very loud thunder!", "Think about how you feel\nwhen you see a big scary dog!", "Think about how you feel\nbefore a doctor gives you an injection!" },
        new[] { "Think about how you feel\nwhen someone takes your toy!", "Think about how you feel\nwhen someone breaks something of yours!", "Think about how you feel\nwhen someone skips your turn!", "Think about how you feel\nwhen someone does not let you speak!" },
        new[] { "Think about how you feel\nwhen everyone yells surprise at your party!", "Think about how you feel\nwhen you find a gift you did not expect!", "Think about how you feel\nwhen someone jumps out from behind a door!", "Think about how you feel\nwhen something pops loudly out of nowhere!" },
        new[] { "Think about how you feel\ntasting medicine that is very bitter!", "Think about how you feel\nwhen you smell something really horrible!", "Think about how you feel\nseeing food you really cannot stand!", "Think about how you feel\nstepping in something wet and slimy!" },
    };

    private readonly string[] situationStories = {
        "Think about how you feel\non your birthday!",
        "Think about how you feel\nwhen you miss someone.",
        "Think about how you feel\nhearing a loud noise!",
        "Think about how you feel\nwhen something is not fair!",
        "Think about how you feel\nwhen something unexpected happens!",
        "Think about how you feel\nwhen you smell something bad!",
    };

    private readonly string[] followUpPhrases = {
        "Now can you tell how\nthis friend is feeling?",
        "Does that help?\nWhat is this friend feeling?",
        "Have another look. What do you think now?",
    };

    private readonly string[] correctMessages = {
        "Yes! This friend is feeling\n{0}!",
        "That is right!\nThis friend feels {0}!",
        "Amazing! You got it!\nThis friend is {0}!",
        "Well done!\nThis friend is feeling {0}!",
        "Correct! You could tell\nthis friend feels {0}!",
    };

    private readonly string[] correctMessagesAr = {
        "نَعَم! هَذَا الصَّدِيقُ يَشْعُرُ بِـ {0}!",
        "صَحِيح! هَذَا الصَّدِيقُ يَشْعُرُ بِـ {0}!",
        "رَائِع! عَرَفْتَهَا! هَذَا الصَّدِيقُ {0}!",
        "أَحْسَنْتَ! هَذَا الصَّدِيقُ يَشْعُرُ بِـ {0}!",
        "صَحِيح! اِسْتَطَعْتَ مَعْرِفَةَ أَنَّ هَذَا الصَّدِيقَ {0}!",
    };

    string[] correctMessagesRaw =>
        GameManager.IsArabic() ? correctMessagesAr : correctMessages;

    private int correctEmotionIndex = 0;
    private int attemptCount = 0;
    private bool roundActive = false;
    private bool demoSkipped = false;
    private bool hintUsed = false;
    private int starsEarned = 0;
    private int _roundsSinceMotivation = 0;

    private float roundStartTime = 0f;
    private bool slowHintFired = false;
    private Coroutine slowTimerCoroutine = null;

    private int totalCorrect = 0;
    private int totalWrong = 0;
    private float totalResponseTime = 0f;
    private int responseCount = 0;
    private int hintsUsed = 0;

    private List<string> confusionLog = new List<string>();

    private Dictionary<string, int> confusionCounts = new Dictionary<string, int>();

    private List<string> wrongThisRound = new List<string>();

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (HeartManager.Instance == null)
            new GameObject("HeartManager").AddComponent<HeartManager>();

        if (LLMService.Instance == null)
            new GameObject("LLMService").AddComponent<LLMService>();
    }

    void Start()
    {
        LumiGenderHelper.Apply(lumiMale, lumiFemale);
        DOTween.SetTweensCapacity(500, 50);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentGame = 2;
            currentLevel = GameManager.Instance.currentLevel;
        }

        attemptCount = 0;
        starsEarned = 0;
        roundActive = false;

        HideAllUI();
        HideLumiCompletely();
        UpdateHUD();

        if (LLMService.Instance != null)
        {
            LLMService.Instance.PrewarmCache(new[] {
                "Let me think of a clue for you.",
                "Here is another clue for you.",
                "Take your time.",
                "Look at this face carefully.",
                L("How does this friend feel?", "كَيْفَ يَشْعُرُ هَذَا الصَّدِيق؟"),
                "Now can you tell how this friend is feeling?",
                "Does that help? What is this friend feeling?",
                "Have another look. What do you think now?"
            }, lumiAudio);
        }

        string _pName = GameManager.Instance != null ? GameManager.Instance.lumiName : "default";
        string _demoKey = "MG2DemoSeen_" + _pName.Trim().Replace(" ", "_");
        bool demoSeen = PlayerPrefs.GetInt(_demoKey, 0) == 1;
        if (!demoSeen && currentLevel == 0) StartCoroutine(PlayDemo());
        else StartRound();
    }

    void KillAllButtonTweens()
    {
        if (answerContainer == null) return;
        foreach (Transform child in answerContainer.transform)
        {
            if (child != null) DOTween.Kill(child);
        }
    }

    void UpdateHUD()
    {
        int hearts = HeartManager.Instance != null ? HeartManager.Instance.GetHearts(2) : 3;
        for (int i = 0; i < heartImages.Length; i++)
            heartImages[i].sprite = i < hearts ? heartFull : heartEmpty;

        if (coinText != null && GameManager.Instance != null)
            coinText.text = GameManager.Instance.tempCoins.ToString();
    }

    void HideAllUI()
    {
        SafeHide(questionText);
        SafeHide(answerContainer);
        SafeHide(speechBubble);
        SafeHide(demoSkipBtn);
        SafeHide(revealPanel);
        if (emotionImagePanel != null) emotionImagePanel.SetActive(false);
    }

    void SafeHide(GameObject obj)
    {
        if (obj == null) return;
        DOTween.Kill(obj.transform);
        obj.SetActive(false);
    }

    void HideLumiCompletely()
    {
        if (speechBubble != null) { DOTween.Kill(speechBubble.transform); speechBubble.SetActive(false); }
        if (lumiCorner != null) lumiCorner.SetActive(false);
    }

    void HideSpeechBubble()
    {
        if (speechBubble == null) { HideLumiCompletely(); return; }
        DOTween.Kill(speechBubble.transform);
        speechBubble.transform.DOScale(0f, 0.2f).OnComplete(() =>
        {
            if (speechBubble != null) speechBubble.SetActive(false);
            if (lumiCorner != null) lumiCorner.SetActive(false);
        });
    }

    void ShowLumi(string message, string ttsText = null)
    {
        if (lumiCorner == null) return;
        lumiCorner.SetActive(true);
        if (speechBubble == null) return;

        DOTween.Kill(speechBubble.transform);
        speechBubble.SetActive(true);

        if (speechText != null)
        {
            speechText.gameObject.SetActive(true);
            if (GameManager.IsArabic() && GameManager.Instance?.arabicFallbackFont != null)
                speechText.font = GameManager.Instance.arabicFallbackFont;
            else if (!GameManager.IsArabic() && GameManager.Instance?.englishDefaultFont != null)
                speechText.font = GameManager.Instance.englishDefaultFont;
            speechText.alignment = GameManager.IsArabic() ? TMPro.TextAlignmentOptions.Right : TMPro.TextAlignmentOptions.Left;
            speechText.text = message;
            speechText.ForceMeshUpdate();
        }

        speechBubble.transform.localScale = Vector3.zero;
        speechBubble.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        if (LLMService.Instance != null)
        {
            string speakText = GameManager.IsArabic()
                ? (ttsText ?? _lastRawArabic ?? message)
                : message;
            _lastRawArabic = null;
            LLMService.Instance.StopSpeaking(lumiAudio);
            StartCoroutine(SpeakAfterFrame(speakText));
        }
    }

    IEnumerator SpeakAfterFrame(string message)
    {
        yield return null;
        if (LLMService.Instance != null)
            LLMService.Instance.SpeakOnSource(message, lumiAudio);
    }

    public void OnSkipDemoPressed()
    {
        if (demoSkipped) return;
        demoSkipped = true;
        StopAllCoroutines();
        DOTween.KillAll();
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.StopSpeaking(lumiAudio);
        HideAllUI();
        HideLumiCompletely();
        if (demoSkipBtn != null) demoSkipBtn.SetActive(false);
        PlayerPrefs.SetInt("MG2DemoSeen_" + (GameManager.Instance != null ? GameManager.Instance.lumiName.Trim().Replace(" ", "_") : "default"), 1);
        PlayerPrefs.Save();
        StartCoroutine(DelayedStartRound());
    }

    IEnumerator DelayedStartRound() { yield return new WaitForSeconds(0.2f); StartRound(); }

    void EndDemo()
    {
        demoSkipped = false;
        HideAllUI();
        HideLumiCompletely();
        PlayerPrefs.SetInt("MG2DemoSeen_" + (GameManager.Instance != null ? GameManager.Instance.lumiName.Trim().Replace(" ", "_") : "default"), 1);
        PlayerPrefs.Save();
        StartRound();
    }

    IEnumerator WaitOrSkip(float seconds)
    {
        float e = 0f;
        while (e < seconds && !demoSkipped) { e += Time.deltaTime; yield return null; }
    }

    IEnumerator WaitForLumiAudio(float maxStartWait = 5f, float unused = 0f)
    {

        float startWait = GameManager.IsArabic() ? Mathf.Max(maxStartWait, 6f) : maxStartWait;

        if (lumiAudio == null) { yield return new WaitForSeconds(2f); yield break; }

        float waited = 0f;
        while (!lumiAudio.isPlaying && waited < startWait)
        {
            if (demoSkipped) yield break;
            waited += Time.deltaTime;
            yield return null;
        }
        while (lumiAudio.isPlaying)
        {
            if (demoSkipped) yield break;
            yield return null;
        }
        yield return new WaitForSeconds(0.4f);
    }

    IEnumerator PlayDemo()
    {
        demoSkipped = false;
        if (demoSkipBtn != null) demoSkipBtn.SetActive(true);
        lumiCorner?.SetActive(true);

        ShowLumi(L("Hi! I am Lumi.\nWelcome to Emotion Quest!", "مَرْحَباً! أَنَا لُومِي. أَهْلًا بِكَ فِي رِحْلَةِ الْمَشَاعِر!"));
        yield return StartCoroutine(WaitForLumiAudio(5f));
        yield return new WaitForSeconds(2f);
        if (demoSkipped) yield break;

        ShowLumi(L("You will see a friend's face.\nCan you tell how they feel?", "سَتَرَى وَجْهَ صَدِيقٍ. هَلْ تَسْتَطِيعُ مَعْرِفَةَ شُعُورِه؟"));
        if (emotionImagePanel != null)
        {
            emotionImagePanel.SetActive(true);
            Image panelImg = emotionImagePanel.GetComponent<Image>();
            if (panelImg != null) panelImg.color = new Color(0.96f, 0.96f, 0.97f, 1f);
            if (emotionImage != null && emotionSprites.Length > 6)
            { emotionImage.sprite = emotionSprites[6]; emotionImage.preserveAspect = true; }
            DOTween.Kill(emotionImagePanel.transform);
            emotionImagePanel.transform.localScale = Vector3.zero;
            emotionImagePanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }
        yield return StartCoroutine(WaitForLumiAudio(5f));
        yield return new WaitForSeconds(2f);
        if (demoSkipped) yield break;

        ShowLumi(L("You will choose the answer\nthat matches the feeling!", "سَتَخْتَارُ الإِجَابَةَ الَّتِي تُطَابِقُ الشُّعُور!"));
        if (answerContainer != null) { answerContainer.SetActive(true); BuildDemoAnswers(); }
        if (questionText != null) questionText.SetActive(true);
        yield return StartCoroutine(WaitForLumiAudio(5f));
        yield return new WaitForSeconds(2f);
        if (demoSkipped) yield break;

        ShowLumi(L("Great! This friend is feeling Neutral!", "رَائِع! هَذَا الصَّدِيقُ يَشْعُرُ بِالْحِيَادِ!"));
        HighlightDemoCorrectButton();
        yield return StartCoroutine(WaitForLumiAudio(5f));
        yield return new WaitForSeconds(2f);
        if (demoSkipped) yield break;

        if (answerContainer != null) answerContainer.SetActive(false);
        if (questionText != null) questionText.SetActive(false);
        if (emotionImagePanel != null) emotionImagePanel.SetActive(false);

        ShowLumi(L("Now it is your turn!\nGood luck!", "الآنَ جَاءَ دَوْرُك! حَظًّا مُوَفَّقًا!"));
        yield return StartCoroutine(WaitForLumiAudio(5f));
        yield return new WaitForSeconds(1f);
        EndDemo();
    }

    void BuildDemoAnswers()
    {
        foreach (Transform child in answerContainer.transform) { DOTween.Kill(child); Destroy(child.gameObject); }
        SetAnswerLayout(1);
        int[] demoOptions = { 6 };
        foreach (int idx in demoOptions)
        {
            if (answerButtonPrefab == null) break;
            GameObject btn = Instantiate(answerButtonPrefab, answerContainer.transform);
            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = emotionColors[0];
            Image emoji = btn.transform.Find("BtnEmoji")?.GetComponent<Image>();
            if (emoji != null && idx < emojiSprites.Length) { emoji.sprite = emojiSprites[idx]; emoji.preserveAspect = true; }
            TextMeshProUGUI label = btn.transform.Find("BtnLabel")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = EmotionName(idx);
                if (GameManager.IsArabic() && GameManager.Instance?.arabicFallbackFont != null)
                    label.font = GameManager.Instance.arabicFallbackFont;
            }
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null) btnComp.interactable = false;
        }
    }

    void HighlightDemoCorrectButton()
    {
        if (answerContainer == null || answerContainer.transform.childCount == 0) return;
        Transform first = answerContainer.transform.GetChild(0); if (first == null) return;
        Image btnImg = first.GetComponent<Image>();
        if (btnImg != null) { DOTween.Kill(first); btnImg.DOColor(new Color(0.96f, 0.89f, 0.63f, 1f), 0.3f); }
        DOTween.Kill(first);
        first.DOScale(1.15f, 0.3f).SetLoops(4, LoopType.Yoyo);
    }

    void SetAnswerLayout(int count)
    {
        var grid = answerContainer?.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        if (count == 2) { grid.constraintCount = 2; grid.cellSize = new Vector2(250f, 85f); grid.spacing = new Vector2(20f, 10f); }
        else if (count == 3) { grid.constraintCount = 3; grid.cellSize = new Vector2(190f, 85f); grid.spacing = new Vector2(15f, 10f); }
        else { grid.constraintCount = 2; grid.cellSize = new Vector2(250f, 85f); grid.spacing = new Vector2(20f, 15f); }
        grid.childAlignment = TextAnchor.MiddleCenter;
    }

    void StartRound()
    {
        attemptCount = 0;
        hintUsed = false;
        roundActive = false;
        slowHintFired = false;
        wrongThisRound.Clear();

        HideAllUI();
        HideLumiCompletely();

        correctEmotionIndex = correctEmotionPerLevel[currentLevel];
        UpdateHUD();
        StartCoroutine(ShowRound());
    }

    IEnumerator ShowRound()
    {
        correctEmotionIndex = correctEmotionPerLevel[currentLevel];

        if (emotionImagePanel != null)
        {
            emotionImagePanel.SetActive(true);
            Image panelImg = emotionImagePanel.GetComponent<Image>();
            if (panelImg != null) panelImg.color = emotionColors[correctEmotionIndex];
            if (emotionImage != null && correctEmotionIndex < emotionSprites.Length)
            { emotionImage.sprite = emotionSprites[correctEmotionIndex]; emotionImage.preserveAspect = true; }
            DOTween.Kill(emotionImagePanel.transform);
            emotionImagePanel.transform.localScale = Vector3.zero;
            emotionImagePanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(1f);

        if (questionText != null) questionText.SetActive(true);
        BuildAnswerOptions();

        if (answerContainer != null)
        {
            answerContainer.SetActive(true);
            answerContainer.transform.localScale = Vector3.zero;
            answerContainer.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(0.5f);

        roundActive = true;
        roundStartTime = Time.time;

        if (slowTimerCoroutine != null) StopCoroutine(slowTimerCoroutine);
        slowTimerCoroutine = StartCoroutine(SlowResponseTimer());
    }

    IEnumerator SlowResponseTimer()
    {
        yield return new WaitForSeconds(slowResponseThreshold);

        if (!roundActive || slowHintFired) yield break;

        slowHintFired = true;
        hintUsed = true;
        hintsUsed++;

        float rt = Time.time - roundStartTime;
        Debug.Log("[LLM] Slow response detected (" + rt.ToString("F1") + "s) — generating proactive hint.");

        var ctx = BuildHintContext(null, rt, "situational");
        LLMService.Instance.GetHint(ctx, hint =>
        {
            if (!roundActive) return;
            string shaped = GameManager.IsArabic()
                ? ShapeArabicLines(hint)
                : hint;
            ShowLumi(shaped, hint);
        });

        yield return StartCoroutine(WaitForLumiAudio(2f));
        if (roundActive) HideSpeechBubble();
    }

    void BuildAnswerOptions()
    {
        if (answerContainer == null) return;
        foreach (Transform child in answerContainer.transform) { DOTween.Kill(child); Destroy(child.gameObject); }

        int[] options = levelAnswerOptions[currentLevel];
        SetAnswerLayout(options.Length);

        List<int> shuffled = new List<int>(options);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }

        for (int i = 0; i < shuffled.Count; i++)
        {
            if (answerButtonPrefab == null) break;
            int emotionIdx = shuffled[i];
            GameObject btn = Instantiate(answerButtonPrefab, answerContainer.transform);
            btn.transform.localScale = Vector3.zero;
            btn.transform.DOScale(1f, 0.2f).SetDelay(i * 0.08f);

            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = emotionColors[correctEmotionIndex];

            Image emoji = null;
            TextMeshProUGUI label = null;
            foreach (Transform child in btn.transform)
            {
                if (child.name == "BtnEmoji") emoji = child.GetComponent<Image>();
                if (child.name == "BtnLabel") label = child.GetComponent<TextMeshProUGUI>();
            }
            if (emoji != null && emotionIdx < emojiSprites.Length && emojiSprites[emotionIdx] != null)
            { emoji.sprite = emojiSprites[emotionIdx]; emoji.preserveAspect = true; }
            if (label != null)
            {
                label.text = EmotionName(emotionIdx);
                if (GameManager.IsArabic() && GameManager.Instance?.arabicFallbackFont != null)
                    label.font = GameManager.Instance.arabicFallbackFont;
            }

            int captured = emotionIdx;
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null) btnComp.onClick.AddListener(() => OnAnswerSelected(captured));
        }
    }

    void OnAnswerSelected(int selectedEmotion)
    {
        if (!roundActive) return;

        if (slowTimerCoroutine != null) { StopCoroutine(slowTimerCoroutine); slowTimerCoroutine = null; }

        float responseTime = Time.time - roundStartTime;

        bool isCorrect = selectedEmotion == correctEmotionIndex;

        if (isCorrect)
        {
            roundActive = false;
            totalCorrect++;
            totalResponseTime += responseTime;
            responseCount++;

            starsEarned = !hintUsed ? 3 : hintsUsed == 1 ? 2 : 1;

            HighlightCorrectButton(selectedEmotion);
            GameManager.Instance?.AddTempCoins(!hintUsed ? 20 : 12);
            UpdateHUD();

            _roundsSinceMotivation++;
            bool shouldMotivate = _roundsSinceMotivation >= 2 || attemptCount >= 1;
            if (shouldMotivate)
            {
                _roundsSinceMotivation = 0;
                StartCoroutine(ShowCorrectWithMotivation());
            }
            else
            {
                StartCoroutine(QuickCorrectAndContinue());
            }
        }
        else
        {
            totalWrong++;
            attemptCount++;

            string confusionKey = EmotionName(correctEmotionIndex) + "→" + EmotionName(selectedEmotion);
            confusionLog.Add(confusionKey);
            wrongThisRound.Add(EmotionName(selectedEmotion));
            if (!confusionCounts.ContainsKey(confusionKey)) confusionCounts[confusionKey] = 0;
            confusionCounts[confusionKey]++;

            ShakeWrongButton(selectedEmotion);
            HandleWrongAnswer(selectedEmotion, responseTime);
        }
    }
    IEnumerator QuickCorrectAndContinue()
    {
        string emotionRaw = EmotionNameRaw(correctEmotionIndex);
        string rawMsg = string.Format(
            correctMessagesRaw[Random.Range(0, correctMessagesRaw.Length)],
            emotionRaw);
        string displayMsg = GameManager.IsArabic()
            ? ArabicSupport.ArabicFixer.Fix(rawMsg)
            : rawMsg;
        lumiCorner?.SetActive(true);
        ShowLumi(displayMsg, rawMsg);
        yield return StartCoroutine(WaitForLumiAudio(1.5f));
        yield return new WaitForSeconds(2f);
        HideSpeechBubble();
        lumiCorner?.SetActive(false);
        yield return new WaitForSeconds(0.2f);
        OnRoundCorrect();
    }

    IEnumerator ShowCorrectWithMotivation()
    {
        string emotionRaw = EmotionNameRaw(correctEmotionIndex);
        string rawMsg = string.Format(
            correctMessagesRaw[Random.Range(0, correctMessagesRaw.Length)],
            emotionRaw);
        string displayMsg = GameManager.IsArabic()
            ? ArabicSupport.ArabicFixer.Fix(rawMsg)
            : rawMsg;
        lumiCorner?.SetActive(true);
        ShowLumi(displayMsg, rawMsg);

        bool struggled = attemptCount >= 2;
        bool perfect = attemptCount == 0;
        var motCtx = new LLMService.MotivationContext
        {
            game = 2,
            currentLevel = currentLevel,
            recentWrongCount = attemptCount,
            recentCorrectCount = totalCorrect,
            justSucceededAfterStruggle = struggled,
            situation = struggled ? "struggling" :
                        perfect ? "perfect" :
                                    "improving"
        };

        string motivationText = null;
        bool motivationReady = false;
        LLMService.Instance.GetMotivation(motCtx, msg => { motivationText = msg; motivationReady = true; });

        yield return StartCoroutine(WaitForLumiAudio(1.5f));
        yield return new WaitForSeconds(2f);
        HideSpeechBubble();
        yield return new WaitForSeconds(0.3f);

        if (motivationReady && !string.IsNullOrEmpty(motivationText))
        {
            string motivationDisplay = GameManager.IsArabic()
                ? ShapeArabicLines(motivationText)
                : motivationText;
            ShowLumi(motivationDisplay, motivationText);
            yield return StartCoroutine(WaitForLumiAudio(1.5f));
            yield return new WaitForSeconds(2f);
            HideSpeechBubble();
            yield return new WaitForSeconds(0.3f);
        }

        OnRoundCorrect();
    }

    void HandleWrongAnswer(int selectedEmotion, float responseTime)
    {
        if (HeartManager.Instance != null && HeartManager.Instance.GetHearts(2) <= 0)
        {
            roundActive = false;
            StartCoroutine(RedirectToWait());
            return;
        }

        if (attemptCount == 1)
        {

            hintUsed = true;
            hintsUsed++;
            StartCoroutine(Attempt1SituationalHint(selectedEmotion, responseTime));
        }
        else if (attemptCount == 2)
        {

            hintsUsed++;
            StartCoroutine(Attempt2SocialHint(selectedEmotion, responseTime));
        }
        else
        {

            roundActive = false;
            StartCoroutine(Attempt3Reveal());
        }
    }
    IEnumerator Attempt2SocialHint(int selectedEmotion, float responseTime)
    {
        lumiCorner?.SetActive(true);

        var ctx = BuildHintContext(EmotionName(selectedEmotion), responseTime, "facial");
        string hint = null;
        bool hintReady = false;
        LLMService.Instance.GetHint(ctx, result => { hint = result; hintReady = true; });

        ShowLumi(L("Here is another clue for you.", "إِلَيْكَ تَلْمِيحًا آخَر."));
        yield return StartCoroutine(WaitForLumiAudio(1f, 3f));

        float waited = 0f;
        while (!hintReady && waited < 8f) { waited += Time.deltaTime; yield return null; }

        if (hint != null)
        {
            ShowLumi(GameManager.IsArabic() ? ShapeArabicLines(hint) : hint);

            yield return StartCoroutine(WaitForLumiAudio(6f));
            yield return new WaitForSeconds(2f);
        }
        HideSpeechBubble();
    }

    IEnumerator Attempt1SituationalHint(int selectedEmotion, float responseTime)
    {
        lumiCorner?.SetActive(true);

        var ctx = BuildHintContext(EmotionName(selectedEmotion), responseTime, "situational");
        string hint = null;
        bool hintReady = false;
        float groqStart = Time.time;
        LLMService.Instance.GetHint(ctx, result => { hint = result; hintReady = true; });

        ShowLumi(L("Let me think of a clue for you.", "دَعْنِي أُفَكِّرُ فِي تَلْمِيحٍ لَك."));
        yield return StartCoroutine(WaitForLumiAudio(5f));

        float waited = 0f;
        while (!hintReady && waited < 8f) { waited += Time.deltaTime; yield return null; }
        Debug.Log("[Hint] Groq took " + (Time.time - groqStart).ToString("F1") + "s. Ready=" + hintReady);

        if (hint != null)
        {
            ShowLumi(GameManager.IsArabic() ? ShapeArabicLines(hint) : hint);

            yield return StartCoroutine(WaitForLumiAudio(6f));
            yield return new WaitForSeconds(2f);
        }
        HideSpeechBubble();
    }

    IEnumerator Attempt3Reveal()
    {
        foreach (Transform child in answerContainer.transform)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        PulseCorrectButton();
        yield return StartCoroutine(ShowRevealPanel());
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(EndLevel(false));
    }

    void HighlightCorrectButton(int emotionIdx)
    {
        foreach (Transform child in answerContainer.transform)
        {
            TextMeshProUGUI label = null;
            foreach (Transform c in child)
                if (c.name == "BtnLabel") label = c.GetComponent<TextMeshProUGUI>();

            Image btnImg = child.GetComponent<Image>();
            if (label != null && label.text == EmotionName(emotionIdx))
            {
                DOTween.Kill(child);
                child.DOScale(1.15f, 0.2f).SetLoops(4, LoopType.Yoyo);
                if (btnImg != null) btnImg.DOColor(new Color(0.62f, 0.80f, 0.56f, 1f), 0.3f);
            }
        }
    }

    void ShakeWrongButton(int emotionIdx)
    {
        foreach (Transform child in answerContainer.transform)
        {
            TextMeshProUGUI label = null;
            foreach (Transform c in child)
                if (c.name == "BtnLabel") label = c.GetComponent<TextMeshProUGUI>();

            if (label != null && label.text == EmotionName(emotionIdx))
            {
                DOTween.Kill(child);
                child.DOShakePosition(0.3f, 8f, 15);
                Image btnImg = child.GetComponent<Image>();
                if (btnImg != null)
                    btnImg.DOColor(new Color(0.15f, 0.15f, 0.15f, 0.85f), 0.15f)
                          .OnComplete(() => { if (btnImg != null) btnImg.DOColor(emotionColors[correctEmotionIndex], 0.4f); });
            }
        }
    }

    void PulseCorrectButton()
    {
        foreach (Transform child in answerContainer.transform)
        {
            TextMeshProUGUI label = null;
            foreach (Transform c in child)
                if (c.name == "BtnLabel") label = c.GetComponent<TextMeshProUGUI>();
            Image btnImg = child.GetComponent<Image>();

            if (label != null && label.text == EmotionName(correctEmotionIndex))
            {
                DOTween.Kill(child);
                child.DOScale(1.2f, 0.3f).SetLoops(-1, LoopType.Yoyo);
                if (btnImg != null) btnImg.DOColor(new Color(0.62f, 0.80f, 0.56f, 1f), 0.3f);
            }
            else
            {
                Color faded = emotionColors[correctEmotionIndex] * 0.5f; faded.a = 0.5f;
                if (btnImg != null) btnImg.DOColor(faded, 0.3f);
                Button btn = child.GetComponent<Button>(); if (btn != null) btn.interactable = false;
            }
        }
    }

    IEnumerator ShowRevealPanel()
    {
        if (revealPanel == null) yield break;
        if (revealTitle != null) revealTitle.text = "This friend is feeling...";
        if (revealEmoji != null && correctEmotionIndex < emojiSprites.Length)
        { revealEmoji.sprite = emojiSprites[correctEmotionIndex]; revealEmoji.preserveAspect = true; }
        if (revealEmotionName != null) revealEmotionName.text = EmotionName(correctEmotionIndex);

        DOTween.Kill(revealPanel.transform);
        revealPanel.SetActive(true);
        revealPanel.transform.localScale = Vector3.zero;
        revealPanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(4f);
        DOTween.Kill(revealPanel.transform);
        revealPanel.transform.DOScale(0f, 0.25f)
            .OnComplete(() => { if (revealPanel != null) revealPanel.SetActive(false); });
        yield return new WaitForSeconds(0.3f);
    }

    LLMService.HintContext BuildHintContext(string selectedEmotion, float responseTime, string hintType = "situational")
    {
        string confusionPattern = null;
        foreach (var pair in confusionCounts)
            if (pair.Value >= 2) { confusionPattern = pair.Key; break; }

        return new LLMService.HintContext
        {
            correctEmotion = EmotionName(correctEmotionIndex),
            selectedEmotion = selectedEmotion,
            attemptNumber = attemptCount,
            responseTimeSec = responseTime,
            wrongHistory = new List<string>(wrongThisRound),
            confusionPattern = confusionPattern,
            hintType = hintType
        };
    }

    void OnRoundCorrect()
    {
        StartCoroutine(WaitForBubbleThenEnd());
    }

    IEnumerator WaitForBubbleThenEnd()
    {
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(EndLevel(true));
    }

    IEnumerator EndLevel(bool passed)
    {
        KillAllButtonTweens();
        HideAllUI();
        HideLumiCompletely();

        SaveSessionData(passed);

        yield return new WaitForSeconds(0.3f);

        if (passed)
        {
            if (GameManager.Instance != null)
            {
                if (starsEarned > GameManager.Instance.mg2Stars[currentLevel])
                    GameManager.Instance.mg2Stars[currentLevel] = starsEarned;

                GameManager.Instance.currentGame = 2;
                GameManager.Instance.lastSessionCoins = GameManager.Instance.tempCoins;
                GameManager.Instance.CommitCoins();
                GameManager.Instance.UnlockNextLevel(2, currentLevel);
                GameManager.Instance.SaveData();
            }
            SceneManager.LoadScene("LevelCompleteScene");
        }
        else
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.currentGame = 2;
                GameManager.Instance.DiscardCoins();
                GameManager.Instance.totalCoins += 10;
                GameManager.Instance.SaveData();
            }
            HeartManager.Instance?.LoseHeart(2);
            yield return new WaitForSeconds(0.3f);
            SceneManager.LoadScene(
                (HeartManager.Instance != null && HeartManager.Instance.GetHearts(2) <= 0)
                    ? "WaitScene" : "LevelLostScene");
        }
    }

    void SaveSessionData(bool passed)
    {
        string playerName = GameManager.Instance != null ? GameManager.Instance.lumiName : "Unknown";
        float avgRT = responseCount > 0 ? totalResponseTime / responseCount : 0f;

        int thisAttempt = LocalDataManager.CountExistingAttempts(playerName, 2, currentLevel) + 1;

        var data = new LocalDataManager.Game2SessionData
        {
            playerName = playerName,
            sessionId = LocalDataManager.SessionId,
            currentLevel = currentLevel,
            attemptNumber = thisAttempt,
            emotionTested = EmotionName(correctEmotionIndex),
            correctAnswers = totalCorrect,
            wrongAnswers = totalWrong,
            avgResponseTimeSec = avgRT,
            hintsUsed = hintsUsed,
            hint1Used = hintsUsed >= 1 ? 1 : 0,
            hint2Used = hintsUsed >= 2 ? 1 : 0,
            levelPassed = passed,
            starsEarned = starsEarned,
        };

        foreach (var pair in confusionCounts)
            data.confusionPairs.Add(new LocalDataManager.ConfusionEntry
            {
                correctEmotion = pair.Key.Split('→')[0],
                selectedEmotion = pair.Key.Split('→').Length > 1 ? pair.Key.Split('→')[1] : "",
                count = pair.Value
            });

        LocalDataManager.SaveGame2Session(data);
        Debug.Log("[LocalData] Game2 L" + currentLevel + " A" + thisAttempt +
                  " Correct:" + totalCorrect + " Wrong:" + totalWrong + " Hints:" + hintsUsed);
    }

    IEnumerator RedirectToWait()
    {
        KillAllButtonTweens();
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("WaitScene");
    }

    public void OnBackPressed()
    {
        SceneManager.LoadScene("LevelSelectMG2");
    }
}