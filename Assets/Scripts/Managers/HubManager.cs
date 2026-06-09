using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;

public class HubManager : MonoBehaviour
{
    [Header("Lumi")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    [Header("Disclaimer")]
    public TextMeshProUGUI disclaimerText;

    [Header("Intro Next Button")]
    public GameObject introNextBtn;

    [Header("Lumi Speech — First Visit")]
    public GameObject speechBubble;
    public TextMeshProUGUI speechText;
    public AudioSource lumiAudio;

    [Header("Menu Panel")]
    public GameObject menuPanel;

    [Header("Friends")]
    public Image[] friendImages;
    public GameObject[] lockIcons;

    [Header("HUD")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI lumiNameText;

    [Header("Audio Buttons")]
    public Button muteBtn;
    public Button voiceBtn;
    public Sprite musicOnSprite;
    public Sprite musicOffSprite;
    public Sprite voiceOnSprite;
    public Sprite voiceOffSprite;
    private bool musicMuted = false;
    private bool voiceMuted = false;

    [Header("Background Button")]
    [Tooltip("The button that toggles scene backgrounds on/off")]
    public Button backgroundBtn;
    public Sprite backgroundOnSprite;
    public Sprite backgroundOffSprite;
    [Tooltip("TMP label on the Backgrounds settings row")]
    public TextMeshProUGUI backgroundLabel;

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public GameObject settingsOverlay;

    [Header("Settings — Language Buttons")]
    [Tooltip("The English button (englishbtn)")]
    public Button englishBtn;
    [Tooltip("The Arabic button (arabicbtn)")]
    public Button arabicBtn;

    // Colors for selected/unselected language buttons
    private static readonly Color kLangSelected = new Color(0.18f, 0.49f, 0.20f, 1f);
    private static readonly Color kLangUnselected = new Color(0.85f, 0.85f, 0.85f, 1f);
    private static readonly Color kLangTextSelected = Color.white;
    private static readonly Color kLangTextUnselected = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("Dashboard Button")]
    [Tooltip("TMP label on the Check Progress / Dashboard button")]
    public TextMeshProUGUI checkProgressLabel;

    private GameObject activeLumi;

    // Speech lines for first visit
    private string[] introLines;

    void Start()
    {
        // Apply gender
        LumiGenderHelper.Apply(lumiMale, lumiFemale);
        activeLumi = (GameManager.Instance != null &&
                      GameManager.Instance.lumiGender == 1)
            ? lumiFemale : lumiMale;

        if (activeLumi != null)
        {
            activeLumi.transform.position =
                new Vector3(0f, -3.2f, 8f);
            activeLumi.transform.rotation =
                Quaternion.Euler(0f, 180f, 0f);
        }

        if (menuPanel != null)
        {
            menuPanel.transform.localScale =
                Vector3.zero;
            menuPanel.SetActive(false);
        }

        if (speechBubble != null)
            speechBubble.SetActive(false);

        UpdateHUD();
        UpdateFriends();
        LoadAudioPrefs();

        // Build intro lines using child's name
        string name = GameManager.Instance != null ?
            GameManager.Instance.lumiName : "friend";

        bool arabic = GameManager.IsArabic();

        if (arabic)
        {
            string arName = name;
            introLines = new string[] {
                FixAr("مَرْحَباً " + name + "! اِسْمِي لُومِي. أَهْلًا بِكَ فِي سَنْفَيْل!"),
                FixAr("عَاصِفَةٌ كَبِيرَةٌ فَرَّقَت أَصْدِقَائِي وَأَضَعْتُهُم..."),
                FixAr("يُمْكِنُكَ مُسَاعَدَتِي فِي إِنْقَاذِهِم بِاللَّعِبِ فِي هَذِهِ الأَلْعَاب!"),
                FixAr("أَكْمِلِ الْمُسْتَوَى نَفْسَهُ فِي بَاحِثُ الثِّمَار وَرِحْلَةُ الْمَشَاعِر لِإِنْقَاذِ صَدِيق!"),
                FixAr("هَيَّا! أَنَا أَعْلَمُ أَنَّكَ قَادِرٌ عَلَى ذَلِك!")
            };

        }
        else
        {
            introLines = new string[] {
                "Hi " + name + "! My name is Lumi.\nWelcome to Sunvale!",
                "A big storm scattered all my\nfriends and I lost them...",
                "You can help me rescue them\nby playing these 2 games!",
                "Complete the same level in\nFruit Finder and Emotion Quest \n to rescue a friend!",
                "Let's go! I know you can do it!"
            };
        }

        // Per-player intro key — each child sees intro once only
        string _introPlayer = GameManager.Instance != null ? GameManager.Instance.lumiName : "default";
        string _introKey = "HubIntroSeen_" + _introPlayer.Trim().Replace(" ", "_");
        bool introSeen = PlayerPrefs.GetInt(_introKey, 0) == 1;

        if (!introSeen)
            StartCoroutine(PlayIntroStreaming());

        RefreshArabicUI();
        SyncBackgroundButton();
    }

    /// <summary>
    /// Called on Start and after language switch to ensure all
    /// language-sensitive UI is in the correct state.
    /// </summary>
    void RefreshArabicUI()
    {
        bool arabic = GameManager.IsArabic();
        if (checkProgressLabel != null && GameManager.Instance != null)
        {
            if (arabic && GameManager.Instance.arabicFallbackFont != null)
            {
                checkProgressLabel.font = GameManager.Instance.arabicFallbackFont;
                checkProgressLabel.text = ArabicSupport.ArabicFixer.Fix("تَحَقَّقْ مِنَ التَّقَدُّم");
            }
            else if (!arabic && GameManager.Instance.englishDefaultFont != null)
            {
                checkProgressLabel.font = GameManager.Instance.englishDefaultFont;
                checkProgressLabel.text = "Check Progress";
            }
        }
        if (backgroundLabel != null && GameManager.Instance != null)
        {
            if (arabic && GameManager.Instance.arabicFallbackFont != null)
            {
                backgroundLabel.font = GameManager.Instance.arabicFallbackFont;
                backgroundLabel.text = ArabicSupport.ArabicFixer.Fix("خلفية");
            }
            else if (!arabic && GameManager.Instance.englishDefaultFont != null)
            {
                backgroundLabel.font = GameManager.Instance.englishDefaultFont;
                backgroundLabel.text = "Backgrounds";
            }
        }
    }

    private bool nextPressed = false;
    private bool _nextLineReady = false;

    public void OnEmotionMirrorPressed()
    {
        SceneManager.LoadScene("EmotionMirrorScene");
    }

    // ── Settings Panel ────────────────────────────────────────────────────

    public void OnSettingsPressed()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            settingsPanel.transform.localScale = Vector3.zero;
            settingsPanel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
        }
        if (settingsOverlay != null)
        {
            settingsOverlay.SetActive(true);
            var img = settingsOverlay.GetComponent<Image>();
            if (img != null)
            {
                img.color = new Color(0f, 0f, 0f, 0f);
                img.DOFade(0.6f, 0.2f);
            }
        }
        if (activeLumi != null) activeLumi.SetActive(false);
        UpdateLanguageButtons();
    }

    public void OnSettingsClosePressed()
    {
        if (settingsPanel != null)
            settingsPanel.transform.DOScale(0f, 0.18f)
                .OnComplete(() => settingsPanel.SetActive(false));
        if (settingsOverlay != null)
        {
            var img = settingsOverlay.GetComponent<Image>();
            if (img != null)
                img.DOFade(0f, 0.2f).OnComplete(() => settingsOverlay.SetActive(false));
            else
                settingsOverlay.SetActive(false);
        }
        if (activeLumi != null) activeLumi.SetActive(true);
    }

    // ── Language Buttons ──────────────────────────────────────────────────

    public void OnEnglishPressed()
    {
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.languageArabic) return;
        GameManager.SetLanguageArabic(false);
        GameManager.Instance.ApplyArabicFallbackFont(false);
        if (LocalisationManager.Instance != null)
            LocalisationManager.Instance.RestoreAll();
        // Stop any playing Arabic TTS
        if (lumiAudio != null && lumiAudio.isPlaying)
            lumiAudio.Stop();
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.StopSpeaking(lumiAudio);
        // Rebuild intro lines in English so next speech shows correctly
        string name = GameManager.Instance != null ? GameManager.Instance.lumiName : "friend";
        introLines = new string[] {
            "Hi " + name + "! My name is Lumi.\nWelcome to Sunvale!",
            "A big storm scattered all my\nfriends and I lost them...",
            "You can help me rescue them\nby playing these 2 games!",
            "Complete the same level in\nFruit Finder and Emotion Quest\nto rescue a friend!",
            "Let's go! I know you can do it!"
        };
        RefreshArabicUI();
        UpdateLanguageButtons();
    }

    public void OnArabicPressed()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.languageArabic) return;
        GameManager.SetLanguageArabic(true);
        GameManager.Instance.ApplyArabicFallbackFont(true);
        ApplyLanguage(true);
        // Stop any playing English TTS
        if (lumiAudio != null && lumiAudio.isPlaying)
            lumiAudio.Stop();
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.StopSpeaking(lumiAudio);
        // Rebuild intro lines in Arabic
        string name = GameManager.Instance != null ? GameManager.Instance.lumiName : "صَدِيق";
        introLines = new string[] {
            FixAr("مَرْحَباً " + name + "! اِسْمِي لُومِي. أَهْلًا بِكَ فِي سَنْفَيْل!"),
            FixAr("عَاصِفَةٌ كَبِيرَةٌ فَرَّقَت أَصْدِقَائِي وَأَضَعْتُهُم..."),
            FixAr("يُمْكِنُكَ مُسَاعَدَتِي فِي إِنْقَاذِهِم بِاللَّعِبِ فِي هَذِهِ الأَلْعَاب!"),
            FixAr("أَكْمِلِ الْمُسْتَوَى نَفْسَهُ فِي بَاحِثُ الثِّمَار وَرِحْلَةُ الْمَشَاعِر لِإِنْقَاذِ صَدِيق!"),
            FixAr("هَيَّا! أَنَا أَعْلَمُ أَنَّكَ قَادِرٌ عَلَى ذَلِك!")
        };
        if (LocalisationManager.Instance != null)
            LocalisationManager.Instance.TranslateAll();
        RefreshArabicUI();
        UpdateLanguageButtons();
    }

    void ApplyLanguage(bool arabic)
    {
        // Stop any playing Lumi speech — TTS is English only
        if (lumiAudio != null && lumiAudio.isPlaying)
            lumiAudio.Stop();

        // RTLTMPro handles Arabic rendering — no font swap needed
    }

    private string _lastRawArabic = null;

    string FixAr(string arabic)
    {
        _lastRawArabic = arabic;
        return ShapeArabic(arabic);
    }

    string ShapeArabic(string arabic)
    {
        if (string.IsNullOrEmpty(arabic)) return arabic;
        return ArabicSupport.ArabicFixer.Fix(arabic);
    }

    void ApplyLanguageFonts(bool arabic)
    {
        ApplyLanguage(arabic);
    }

    void SwapFont(TextMeshProUGUI tmp, TMP_FontAsset font)
    {
        if (tmp == null || font == null) return;
    }

    void UpdateLanguageButtons()
    {
        bool arabic = GameManager.IsArabic();
        SetLanguageButton(englishBtn, !arabic); // English selected when NOT arabic
        SetLanguageButton(arabicBtn, arabic); // Arabic  selected when arabic
    }

    void SetLanguageButton(Button btn, bool selected)
    {
        if (btn == null) return;

        // Change background color
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.DOColor(selected ? kLangSelected : kLangUnselected, 0.2f);
        }

        // Change text color
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.DOColor(selected ? kLangTextSelected : kLangTextUnselected, 0.2f);
        }

        // Pulse animation on the selected button
        if (selected)
        {
            btn.transform.DOKill();
            btn.transform.localScale = Vector3.one;
            btn.transform.DOPunchScale(Vector3.one * 0.12f, 0.3f, 5, 0.5f);
        }
        else
        {
            btn.transform.DOKill();
            btn.transform.DOScale(1f, 0.15f);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // STREAMING INTRO — show line immediately, cache next line in parallel
    // ════════════════════════════════════════════════════════════════════════
    // HOW IT WORKS:
    // Line 0 starts downloading immediately in Start().
    // We show the disclaimer (3s) while line 0 downloads.
    // By the time the disclaimer fades, line 0 is cached and plays instantly.
    // While the child reads line 0, line 1 downloads in the background.
    // By the time they press Next, line 1 is ready. No waiting ever again.
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator PlayIntroStreaming()
    {
        // Step 1: wait for LLMService (max 3s)
        float waited = 0f;
        while (LLMService.Instance == null && waited < 3f)
        { waited += Time.deltaTime; yield return null; }

        if (LLMService.Instance == null)
        {
            // No LLM — play intro text-only without audio
            StartCoroutine(PlayIntroTextOnly());
            yield break;
        }

        // Step 2: start downloading line 0 NOW (runs in background)
        _nextLineReady = false;
        LLMService.Instance.PrimeCache(introLines[0], () => _nextLineReady = true);

        // Step 3: show disclaimer while line 0 downloads (~3s covers download time)
        if (disclaimerText != null)
        {
            disclaimerText.gameObject.SetActive(true);
            disclaimerText.alpha = 0f;
            disclaimerText.DOFade(1f, 0.6f);
            yield return new WaitForSeconds(2.5f);
            disclaimerText.DOFade(0f, 0.6f);
            yield return new WaitForSeconds(0.8f);
            disclaimerText.gameObject.SetActive(false);
        }
        else
        {
            // No disclaimer — wait for line 0 to cache (max 6s)
            float lineWait = 0f;
            while (!_nextLineReady && lineWait < 6f)
            { lineWait += Time.deltaTime; yield return null; }
        }

        // Step 4: show each line — cache next line while child reads current one
        if (introNextBtn != null)
            introNextBtn.SetActive(true);

        for (int i = 0; i < introLines.Length; i++)
        {
            // Wait for current line to be cached (usually already done)
            float lineWait = 0f;
            while (!_nextLineReady && lineWait < 5f)
            { lineWait += Time.deltaTime; yield return null; }

            // Show current line — audio plays instantly because it's cached
            nextPressed = false;
            yield return null;
            ShowSpeech(introLines[i]);

            // While child is reading, cache the NEXT line in background
            if (i + 1 < introLines.Length)
            {
                _nextLineReady = false;
                LLMService.Instance.PrimeCache(introLines[i + 1], () => _nextLineReady = true);
            }

            // Wait for Next button press
            while (!nextPressed)
                yield return null;

            HideSpeech();
            yield return new WaitForSeconds(0.4f);
        }

        if (introNextBtn != null)
            introNextBtn.SetActive(false);

        string _savePlayer = GameManager.Instance != null ? GameManager.Instance.lumiName : "default";
        PlayerPrefs.SetInt("HubIntroSeen_" + _savePlayer.Trim().Replace(" ", "_"), 1);
        PlayerPrefs.Save();
    }

    // Fallback: no TTS available — show text only, advance on Next press
    IEnumerator PlayIntroTextOnly()
    {
        if (disclaimerText != null)
        {
            disclaimerText.gameObject.SetActive(true);
            disclaimerText.alpha = 0f;
            disclaimerText.DOFade(1f, 0.6f);
            yield return new WaitForSeconds(2.5f);
            disclaimerText.DOFade(0f, 0.6f);
            yield return new WaitForSeconds(0.8f);
            disclaimerText.gameObject.SetActive(false);
        }

        if (introNextBtn != null)
            introNextBtn.SetActive(true);

        for (int i = 0; i < introLines.Length; i++)
        {
            nextPressed = false;
            yield return null;
            ShowSpeech(introLines[i]);
            while (!nextPressed) yield return null;
            HideSpeech();
            yield return new WaitForSeconds(0.4f);
        }

        if (introNextBtn != null)
            introNextBtn.SetActive(false);

        string _savePlayer = GameManager.Instance != null ? GameManager.Instance.lumiName : "default";
        PlayerPrefs.SetInt("HubIntroSeen_" + _savePlayer.Trim().Replace(" ", "_"), 1);
        PlayerPrefs.Save();
    }

    public void OnIntroNextPressed()
    {
        nextPressed = true;
    }

    void ShowSpeech(string message)
    {
        if (speechBubble == null) return;

        if (speechText != null)
        {
            if (GameManager.IsArabic() && GameManager.Instance?.arabicFallbackFont != null)
                speechText.font = GameManager.Instance.arabicFallbackFont;
            else if (!GameManager.IsArabic() && GameManager.Instance?.englishDefaultFont != null)
                speechText.font = GameManager.Instance.englishDefaultFont;
            speechText.alignment = GameManager.IsArabic()
                ? TMPro.TextAlignmentOptions.Right
                : TMPro.TextAlignmentOptions.Left;
            speechText.text = message;
        }

        if (introNextBtn != null)
            introNextBtn.SetActive(true);

        DOTween.Kill(speechBubble.transform);
        speechBubble.SetActive(true);
        speechBubble.transform.localScale = Vector3.zero;
        speechBubble.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);

        if (LLMService.Instance != null && lumiAudio != null)
        {
            string speakText = GameManager.IsArabic()
                ? (_lastRawArabic ?? message)
                : message;
            _lastRawArabic = null;

            // For English: stop previous speech immediately
            // For Arabic: let previous finish (slow server) — only stop if already playing
            if (!GameManager.IsArabic())
                LLMService.Instance.StopSpeaking(lumiAudio);
            else if (lumiAudio.isPlaying)
                LLMService.Instance.StopSpeaking(lumiAudio);

            StartCoroutine(SpeakHubAfterFrame(speakText));
        }
    }

    IEnumerator SpeakHubAfterFrame(string text)
    {
        yield return null;
        yield return null; // two frames for Arabic to settle
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.SpeakOnSource(text, lumiAudio);
    }

    void HideSpeech()
    {
        if (speechBubble == null) return;

        if (introNextBtn != null)
            introNextBtn.SetActive(false);

        // Stop voice when bubble hides
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.StopSpeaking(lumiAudio);

        DOTween.Kill(speechBubble.transform);
        speechBubble.transform
            .DOScale(0f, 0.2f)
            .OnComplete(() => speechBubble.SetActive(false));
    }
    void LoadAudioPrefs()
    {
        musicMuted =
            PlayerPrefs.GetInt("MusicMuted", 0) == 1;
        voiceMuted =
            PlayerPrefs.GetInt("VoiceMuted", 0) == 1;
        AudioListener.volume = musicMuted ? 0f : 1f;
        UpdateAudioButtons();
    }

    void UpdateAudioButtons()
    {
        if (muteBtn != null)
            muteBtn.GetComponent<Image>().sprite =
                musicMuted ?
                musicOffSprite : musicOnSprite;
        if (voiceBtn != null)
            voiceBtn.GetComponent<Image>().sprite =
                voiceMuted ?
                voiceOffSprite : voiceOnSprite;
    }

    void UpdateHUD()
    {
        if (coinText != null &&
            GameManager.Instance != null)
            coinText.text =
                GameManager.Instance
                    .totalCoins.ToString();
        if (lumiNameText != null &&
            GameManager.Instance != null)
            lumiNameText.text =
                GameManager.Instance.lumiName +
                "'s World";
    }

    void UpdateFriends()
    {
        if (GameManager.Instance == null) return;
        for (int i = 0; i < friendImages.Length; i++)
        {
            bool rescued =
                GameManager.Instance
                    .friendsRescued[i];
            friendImages[i].color = rescued ?
                Color.white :
                new Color(0.15f, 0.15f, 0.15f, 1f);
            if (i < lockIcons.Length &&
                lockIcons[i] != null)
                lockIcons[i].SetActive(!rescued);
        }
    }

    public void OnMemoryGardenPressed()
    {
        if (HeartManager.Instance != null &&
            HeartManager.Instance.GetHearts(1) <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.currentGame = 1;
            SceneManager.LoadScene("WaitScene");
        }
        else
            SceneManager.LoadScene("LevelSelectMG1");
    }

    public void OnFeelingsForestPressed()
    {
        if (HeartManager.Instance != null &&
            HeartManager.Instance.GetHearts(2) <= 0)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.currentGame = 2;
            SceneManager.LoadScene("WaitScene");
        }
        else
            SceneManager.LoadScene("LevelSelectMG2");
    }

    public void OpenMenu()
    {
        if (activeLumi != null)
            activeLumi.SetActive(false);

        menuPanel.SetActive(true);
        menuPanel.transform.localScale = Vector3.zero;
        menuPanel.transform
            .DOScale(Vector3.one, 0.25f)
            .SetEase(Ease.OutBack);
    }

    public void CloseMenu()
    {
        if (activeLumi != null)
            activeLumi.SetActive(true);

        menuPanel.transform
            .DOScale(Vector3.zero, 0.2f)
            .OnComplete(() =>
                menuPanel.SetActive(false));
    }

    public void OnShopPressed()
    {
        CloseMenu();
        SceneManager.LoadScene("ShopScene");
    }

    public void OnDashboardPressed()
    {
        Application.OpenURL("https://lumis-dashboard.vercel.app/");
    }

    public void OnMusicPressed()
    {
        musicMuted = !musicMuted;
        AudioListener.volume = musicMuted ? 0f : 1f;
        PlayerPrefs.SetInt(
            "MusicMuted", musicMuted ? 1 : 0);
        PlayerPrefs.Save();
        if (muteBtn != null)
            muteBtn.GetComponent<Image>().sprite =
                musicMuted ?
                musicOffSprite : musicOnSprite;
    }

    public void OnVoicePressed()
    {
        voiceMuted = !voiceMuted;
        PlayerPrefs.SetInt(
            "VoiceMuted", voiceMuted ? 1 : 0);
        PlayerPrefs.Save();
        if (voiceBtn != null)
            voiceBtn.GetComponent<Image>().sprite =
                voiceMuted ?
                voiceOffSprite : voiceOnSprite;
    }

    public void OnBackgroundPressed()
    {
        bool newState = !GameManager.IsBackgroundEnabled();
        GameManager.SetBackgroundEnabled(newState);

        // Update button sprite to reflect new state
        if (backgroundBtn != null)
            backgroundBtn.GetComponent<Image>().sprite =
                newState ? backgroundOnSprite : backgroundOffSprite;
    }

    /// <summary>
    /// Syncs the background button sprite with the saved setting.
    /// Call this from Start() so the button reflects the correct state on load.
    /// </summary>
    void SyncBackgroundButton()
    {
        if (backgroundBtn == null) return;
        bool enabled = GameManager.IsBackgroundEnabled();
        var img = backgroundBtn.GetComponent<Image>();
        if (img != null)
            img.sprite = enabled ? backgroundOnSprite : backgroundOffSprite;
    }
}