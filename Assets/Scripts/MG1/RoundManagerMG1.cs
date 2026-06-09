using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// RoundManagerMG1 — Fruit Finder (Memory Garden)
///
/// ADAPTIVE DIFFICULTY — complete design
/// ═══════════════════════════════════════════════════════════
/// Parameters adapted across ALL rounds AND levels:
///   • Retention delay   (harder = longer, easier = shorter)
///   • Grid size         (harder = bigger, easier = smaller)
///   • Fruits per round  (harder = more,   easier = fewer)
///
/// Performance scoring (updates after EVERY round):
///   Perfect (0 wrong)       → +1
///   Average (1 wrong)       →  0  (no change)
///   Poor    (2+ wrong/fail) → -1
///
/// Adjustment thresholds:
///   score ≥ +2  → apply HARDER changes, reset score to 0
///   score ≤ -1  → apply EASIER changes, reset score to 0
///
/// Persistence:
///   Saved into GameManager (PlayerPrefs) after every round.
///   Loaded at scene start so state carries across levels.
///   "Not yet initialised" is represented by -1 in GameManager.
///
/// Learning objective constraints (never violated by adaptation):
///   L1 & L2  R0 & R1:  fruits ∈ [1, 2]
///   L3 & L4  R0:        fruits ∈ [2, 3]   ← never drops to 1
///   L3 & L4  R1:        fruits ∈ [1, 2]
///   L4 both rounds:     orderMatters always true (never adapted away)
///
/// Grid bounds:   cols/rows ∈ [2, 4]
/// Delay bounds:  delay     ∈ [0.5s, 4s]
/// ═══════════════════════════════════════════════════════════
/// </summary>
public class RoundManagerMG1 : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Level Settings")]
    public int currentLevel = 0;
    private const int kTotalRounds = 2;
    private const int kPassRequired = 1;

    [Header("Fruit Display — Single")]
    public GameObject fruitDisplayPanel;
    public Image fruitImage;
    public TextMeshProUGUI fruitNameText;
    public GameObject nextBtn;

    [Header("Fruit Display — Sequence")]
    public GameObject sequencePanel;
    // Slot containers — assign the parent box GameObject for each slot.
    // SetSlot() hides the ENTIRE box (not just the image/label children)
    // so empty slots never show a blank green box.
    public GameObject sequenceSlot1;       // parent box for slot 1
    public GameObject sequenceSlot2;       // parent box for slot 2
    public GameObject sequenceSlot3;       // parent box for slot 3
    public Image sequenceFruitImage1;
    public TextMeshProUGUI sequenceFruitName1;
    public Image sequenceFruitImage2;
    public TextMeshProUGUI sequenceFruitName2;
    public Image sequenceFruitImage3;
    public TextMeshProUGUI sequenceFruitName3;
    public GameObject sequenceNextBtn;
    private int _roundsSinceMotivation = 0;

    [Header("Grid")]
    public GameObject gridContainer;
    public GameObject fruitCellPrefab;

    [Header("Reveal Panel — Single")]
    public GameObject revealPanelSingle;
    public TextMeshProUGUI revealTitleSingle;
    public Image revealFruitImageSingle;
    public TextMeshProUGUI revealFruitNameSingle;

    [Header("Reveal Panel — Sequence")]
    public GameObject revealPanelSequence;
    public TextMeshProUGUI revealTitleSequence;
    // Slot containers for the reveal panel — same pattern as sequence panel.
    public GameObject revealSlot1;
    public GameObject revealSlot2;
    public GameObject revealSlot3;
    public Image revealSeqFruitImage1;
    public TextMeshProUGUI revealSeqFruitName1;
    public Image revealSeqFruitImage2;
    public TextMeshProUGUI revealSeqFruitName2;
    public Image revealSeqFruitImage3;
    public TextMeshProUGUI revealSeqFruitName3;

    [Header("HUD")]
    public Image[] heartImages;
    public Sprite heartFull;
    public Sprite heartEmpty;
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI roundText;

    [Header("Lumi")]
    public GameObject lumiCorner;
    public GameObject speechBubble;
    public TextMeshProUGUI speechText;

    [Header("Lumi Characters")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    [Header("Demo")]
    public GameObject demoSkipBtn;

    [Header("Fruit Data")]
    public Sprite[] targetFruitSprites;
    public string[] targetFruitNames;
    public Sprite[] distractorSprites;

    [Header("Sound Effects")]
    public AudioSource sfxAudio;
    public AudioClip correctSfx;
    public AudioClip wrongSfx;
    public AudioClip hintSfx;

    [Header("Background Music")]
    public AudioSource musicAudio;
    public AudioClip backgroundMusic;

    [Header("Lumi Voice — Demo Only")]
    public AudioSource lumiAudio;
    public AudioClip lumiDemoIntro;
    public AudioClip lumiDemoLook;
    public AudioClip lumiDemoPress;
    public AudioClip lumiDemoGetReady;
    public AudioClip lumiDemoFind;
    public AudioClip lumiDemoRounds;
    public AudioClip lumiDemoGoodLuck;

    // ════════════════════════════════════════════════════════════════════════
    // STATIC LEVEL CONFIG  (baseline defaults — never mutated at runtime)
    // ════════════════════════════════════════════════════════════════════════

    // Default fruit counts [level][round] used only when no adaptive state exists yet.
    private static readonly int[][] kBaseFruits = {
        new int[] { 1, 1 },  // L1
        new int[] { 1, 1 },  // L2
        new int[] { 3, 1 },  // L3
        new int[] { 3, 1 },  // L4
    };

    // Default grid sizes per level.
    private static readonly int[] kBaseGridCols = { 2, 3, 4, 4 };
    private static readonly int[] kBaseGridRows = { 2, 3, 4, 4 };

    // Default delay (seconds) when no adaptive state exists.
    private const float kBaseDelay = 1f;
    private const float kDelayMin = 0.5f;
    private const float kDelayMax = 4f;
    private const float kDelayStep = 0.5f;

    // Grid bounds.
    private const int kGridMin = 2;
    private const int kGridMax = 4;

    // Order config — never adapted, always driven by this table.
    private static readonly bool[][] kOrderMatters = {
        new bool[] { false, false },
        new bool[] { false, false },
        new bool[] { false, false },
        new bool[] { true,  true  },  // L4: always ordered
    };

    // ════════════════════════════════════════════════════════════════════════
    // ADAPTIVE STATE  (loaded from / saved to GameManager each round)
    // ════════════════════════════════════════════════════════════════════════

    private float adaptiveDelay;
    private int adaptiveGridCols;
    private int adaptiveGridRows;
    private int[] adaptiveFruits = new int[2];   // [0] = R0 count, [1] = R1 count
    private int perfScore;                     // cumulative across rounds & levels

    // Running log of every change that fired this level session.
    private string changeLog = "None";

    // ════════════════════════════════════════════════════════════════════════
    // ROUND STATE
    // ════════════════════════════════════════════════════════════════════════

    private int currentRound = 0;
    private int roundsCorrect = 0;
    private int attemptCount = 0;
    private int hintsThisLevel = 0;
    private bool nextPressed = false;
    private bool roundActive = false;
    private bool orderMatters = false;
    private bool demoSkipped = false;

    private List<int> targetIndices = new List<int>();
    private List<int> correctlyFound = new List<int>();
    private List<int> usedFruitsThisLevel = new List<int>();
    private List<Sprite> currentPool = new List<Sprite>();

    // ════════════════════════════════════════════════════════════════════════
    // ARABIC LOCALISATION HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Returns english or arabic string based on language setting.</summary>
    // Stores the last raw Arabic string passed through L() — used by ShowLumi for TTS
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

    /// <summary>
    /// Shapes Arabic for LTR TMP display.
    /// Each line is shaped independently so ArabicFixer does not reverse line order.
    /// </summary>
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

    // Arabic fruit name lookup — maps English name → Arabic with tashkeel
    private static readonly Dictionary<string, string> kArabicFruitNames
        = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "Apple",       "تُفَّاحَة"        },
        { "Banana",      "مَوْزَة"          },
        { "Orange",      "بُرْتُقَالَة"     },
        { "Grape",       "عِنَب"            },
        { "Grapes",      "عِنَب"            },
        { "Strawberry",  "فَرَاوِلَة"       },
        { "Strawberries","فَرَاوِلَة"       },
        { "Watermelon",  "بِطِّيخَة"        },
        { "Water Melon", "بِطِّيخَة"        },
        { "Pineapple",   "أَنَانَاس"        },
        { "Mango",       "مَانْجَة"         },
        { "Peach",       "خَوْخَة"          },
        { "Pear",        "إِجَّاصَة"        },
        { "Cherry",      "كَرَز"            },
        { "Lemon",       "لَيْمُونَة"       },
        { "Coconut",     "جَوْزُ الْهِنْد"  },
        { "Kiwi",        "كِيوِي"           },
        { "Plum",        "بَرْقُوق"         },
        { "Melon",       "شَمَّام"          },
        { "Guava",       "جُوَافَة"         },
        { "Avocado",     "أَفُوكَادُو"      },
        { "Blueberry",   "تُوت أَزْرَق"     },
        { "Blueberries", "تُوت أَزْرَق"     },
        { "Raspberry",   "تُوت أَحْمَر"     },
        { "Raspberries",  "تُوت أَحْمَر"    },
        { "Berry",       "تُوتَة"           },
        { "Berries",     "تُوت"             },
        { "Blackberry",  "تُوت أَسْوَد"     },
        { "Blackberries","تُوت أَسْوَد"     },
        { "Pomegranate", "رُمَّانَة"        },
        { "Fig",         "تِينَة"           },
        { "Date",        "تَمْرَة"          },
        { "Apricot",     "مِشْمِشَة"        },
        { "Papaya",      "بَابَايَا"        },
        { "Lime",        "لَيْمُون أَخْضَر" },
    };

    /// <summary>Returns fruit name shaped for TMP display.</summary>
    string FruitName(int idx)
    {
        if (idx < 0 || idx >= targetFruitNames.Length) return "";
        string en = targetFruitNames[idx];
        if (GameManager.IsArabic() && kArabicFruitNames.TryGetValue(en, out string ar))
            return ArabicSupport.ArabicFixer.Fix(ar);
        return en;
    }

    /// <summary>Returns raw unprocessed fruit name for TTS (not shaped by ArabicFixer).</summary>
    string FruitNameRaw(int idx)
    {
        if (idx < 0 || idx >= targetFruitNames.Length) return "";
        string en = targetFruitNames[idx];
        if (GameManager.IsArabic() && kArabicFruitNames.TryGetValue(en, out string ar))
            return ar; // raw Arabic — no ArabicFixer
        return en;
    }

    // Arabic correct feedback lines
    private readonly string[] correctFeedbackAr = {
        "أَحْسَنْتَ، لَقَدْ وَجَدْتَهَا.",
        "رَائِع، لَقَدْ تَذَكَّرْتَهَا.",
        "عَمَلٌ رَائِع، أَجَبْتَ بِشَكْلٍ صَحِيح.",
    };

    // ════════════════════════════════════════════════════════════════════════
    // ANALYTICS
    // ════════════════════════════════════════════════════════════════════════

    private int totalCorrect = 0;
    private int totalWrong = 0;
    private float totalResponseTime = 0f;
    private int responseCount = 0;
    private float lastGridShownTime = 0f;

    // Per-round records collected during play, flushed to LocalDataManager at level end.
    private List<LocalDataManager.RoundRecord> _roundRecords = new List<LocalDataManager.RoundRecord>();
    private float _roundResponseTimeAccum = 0f;
    private int _roundResponseCount = 0;
    private int _roundCorrectTaps = 0;
    private int _roundWrongTaps = 0;
    private int _roundHints = 0;

    // ════════════════════════════════════════════════════════════════════════
    // BOOTSTRAP
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (LLMService.Instance == null)
            new GameObject("LLMService").AddComponent<LLMService>();
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (HeartManager.Instance == null)
            new GameObject("HeartManager").AddComponent<HeartManager>();
    }

    void Start()
    {
        LumiGenderHelper.Apply(lumiMale, lumiFemale);
        DOTween.SetTweensCapacity(500, 50);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentGame = 1;
            currentLevel = GameManager.Instance.currentLevel;
        }

        hintsThisLevel = 0;
        roundsCorrect = 0;
        currentRound = 0;

        LoadAdaptiveState();   // pulls from GameManager (persisted across levels)

        if (LLMService.Instance != null)
        {
            LLMService.Instance.PrewarmCache(new[] {
                "Look at this fruit carefully.",
                "Okay, the grid is coming now.",
                "I can help you with this one.",
                "Take another look at this fruit.",
                L("Try to remember the order.", "حَاوِلْ أَنْ تَتَذَكَّرَ التَّرْتِيب."),
                "You can do it.",
                "Well done, you found it.",
                "Amazing, you remembered it.",
                "Great job, you got it right.",
                "You are doing so well, keep going.",
                "That is the correct answer."
            }, lumiAudio);
        }

        changeLog = "None";

        HideAllUI();
        HideLumiCompletely();
        UpdateHUD();

        string _pNameMG1 = GameManager.Instance != null ? GameManager.Instance.lumiName : "default";
        string _demoKeyMG1 = "MG1DemoSeen_" + _pNameMG1.Trim().Replace(" ", "_");
        bool demoSeen = PlayerPrefs.GetInt(_demoKeyMG1, 0) == 1;
        if (!demoSeen && currentLevel == 0) StartCoroutine(PlayDemo());
        else StartRound();
    }

    // ── Adaptive state persistence ──────────────────────────────────────

    /// <summary>
    /// Pull adaptive state from GameManager.
    /// GameManager uses -1 as sentinel meaning "not initialised yet".
    /// When we see -1 we seed from the level-appropriate base defaults.
    /// This means:
    ///   • Very first ever play           → seeds from base values.
    ///   • After level 1 completes        → carries adapted values into level 2.
    ///   • Player restarts the app        → loads from PlayerPrefs via GameManager.
    /// </summary>
    void LoadAdaptiveState()
    {
        GameManager gm = GameManager.Instance;

        // Delay
        adaptiveDelay = (gm != null && gm.mg1AdaptiveDelay >= 0f)
            ? gm.mg1AdaptiveDelay
            : kBaseDelay;

        // Grid cols
        adaptiveGridCols = (gm != null && gm.mg1AdaptiveGridCols >= 0)
            ? gm.mg1AdaptiveGridCols
            : kBaseGridCols[currentLevel];

        // Grid rows
        adaptiveGridRows = (gm != null && gm.mg1AdaptiveGridRows >= 0)
            ? gm.mg1AdaptiveGridRows
            : kBaseGridRows[currentLevel];

        // Per-round fruit counts.
        // Each round is stored independently so R0 and R1 can adapt separately.
        adaptiveFruits[0] = (gm != null && gm.mg1AdaptiveFruitsR0 >= 0)
            ? gm.mg1AdaptiveFruitsR0
            : kBaseFruits[currentLevel][0];

        adaptiveFruits[1] = (gm != null && gm.mg1AdaptiveFruitsR1 >= 0)
            ? gm.mg1AdaptiveFruitsR1
            : kBaseFruits[currentLevel][1];

        // Performance score carries across levels intentionally —
        // a child who performed perfectly on L1 should enter L2 already
        // closer to the "harder" threshold.
        perfScore = (gm != null) ? gm.mg1PerformanceScore : 0;

        // Clamp everything to valid ranges in case of stale saved data.
        adaptiveDelay = Mathf.Clamp(adaptiveDelay, kDelayMin, kDelayMax);
        adaptiveGridCols = Mathf.Clamp(adaptiveGridCols, kGridMin, kGridMax);
        adaptiveGridRows = Mathf.Clamp(adaptiveGridRows, kGridMin, kGridMax);
        for (int r = 0; r < 2; r++)
            adaptiveFruits[r] = Mathf.Clamp(adaptiveFruits[r], GetMinFruits(r), GetMaxFruits(r));

        Debug.Log("[Adaptive] LOADED — Delay:" + adaptiveDelay + "s" +
                  " Grid:" + adaptiveGridCols + "x" + adaptiveGridRows +
                  " Fruits:[R0=" + adaptiveFruits[0] + " R1=" + adaptiveFruits[1] + "]" +
                  " PerfScore:" + perfScore);
    }

    /// <summary>Write current adaptive state back to GameManager (and PlayerPrefs).</summary>
    void SaveAdaptiveState()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        gm.mg1AdaptiveDelay = adaptiveDelay;
        gm.mg1AdaptiveGridCols = adaptiveGridCols;
        gm.mg1AdaptiveGridRows = adaptiveGridRows;
        gm.mg1AdaptiveFruitsR0 = adaptiveFruits[0];
        gm.mg1AdaptiveFruitsR1 = adaptiveFruits[1];
        gm.mg1PerformanceScore = perfScore;

        gm.SaveData();   // flushes to PlayerPrefs

        // Also persist to local JSON so it survives across sessions.
        LocalDataManager.SaveAdaptiveState(new LocalDataManager.AdaptiveStateData
        {
            playerName = gm.lumiName,
            delay = adaptiveDelay,
            gridCols = adaptiveGridCols,
            gridRows = adaptiveGridRows,
            fruitsR0 = adaptiveFruits[0],
            fruitsR1 = adaptiveFruits[1],
            perfScore = perfScore
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // HUD
    // ════════════════════════════════════════════════════════════════════════

    void UpdateHUD()
    {
        int hearts = HeartManager.Instance != null ? HeartManager.Instance.GetHearts(1) : 3;
        for (int i = 0; i < heartImages.Length; i++)
            heartImages[i].sprite = i < hearts ? heartFull : heartEmpty;

        if (coinText != null && GameManager.Instance != null)
            coinText.text = GameManager.Instance.tempCoins.ToString();

        if (roundText != null)
            roundText.text = GameManager.IsArabic()
                ? ArabicSupport.ArabicFixer.Fix("الجَوْلَة " + (currentRound + 1) + " مِنْ " + kTotalRounds)
                : "Round " + (currentRound + 1) + " of " + kTotalRounds;
    }

    // ════════════════════════════════════════════════════════════════════════
    // UI HELPERS
    // ════════════════════════════════════════════════════════════════════════

    void HideAllUI()
    {
        SafeHide(fruitDisplayPanel);
        SafeHide(nextBtn);
        SafeHide(sequencePanel);
        SafeHide(sequenceNextBtn);
        SafeHide(revealPanelSingle);
        SafeHide(revealPanelSequence);
        SafeHide(speechBubble);
        SafeHide(demoSkipBtn);
        if (gridContainer != null) gridContainer.SetActive(false);
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

    // ════════════════════════════════════════════════════════════════════════
    // LUMI SPEECH
    // ════════════════════════════════════════════════════════════════════════

    // ttsText = raw Arabic (for TTS). message = ArabicFixer-shaped (for display).
    // If ttsText is null, message is used for TTS (English) or TTS is skipped (Arabic already shaped).
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
            // For Arabic: use raw unprocessed text so TTS gets real Unicode
            // _lastRawArabic is set by L() just before ShowLumi is called
            // ttsText override used for LLM-generated messages (already raw)
            string speakText = GameManager.IsArabic()
                ? (ttsText ?? _lastRawArabic ?? message)
                : message;
            _lastRawArabic = null; // clear after use
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

    private readonly string[] correctFeedback = {
        "Well done, you found it.",
        "Amazing, you remembered it.",
        "Great job, you got it right.",
    };

    /// <summary>
    /// Waits until lumiAudio finishes playing, then adds a tail pause.
    /// Falls back to fixedFallback seconds if audio never starts within 3s.
    /// Always waits at least minWait seconds regardless of audio length.
    /// </summary>
    IEnumerator WaitForLumiAudio(float minWait = 1.5f, float fixedFallback = 4f)
    {
        // Arabic TTS is slower due to server round-trip — give it more time to start
        float startWait = GameManager.IsArabic() ? Mathf.Max(fixedFallback, 8f) : fixedFallback;

        float elapsed = 0f;
        while (elapsed < minWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (lumiAudio != null && lumiAudio.isPlaying)
        {
            while (lumiAudio.isPlaying)
                yield return null;
        }
        else
        {
            float waited = 0f;
            while (waited < startWait && (lumiAudio == null || !lumiAudio.isPlaying))
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (lumiAudio != null && lumiAudio.isPlaying)
                while (lumiAudio.isPlaying)
                    yield return null;
        }

        yield return new WaitForSeconds(0.5f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // DEMO
    // ════════════════════════════════════════════════════════════════════════

    public void OnSkipDemoPressed()
    {
        demoSkipped = true;
        StopAllCoroutines();
        if (LLMService.Instance != null && lumiAudio != null)
            LLMService.Instance.StopSpeaking(lumiAudio);
        HideAllUI();
        HideLumiCompletely();
        PlayerPrefs.SetInt("MG1DemoSeen_" + (GameManager.Instance != null ? GameManager.Instance.lumiName.Trim().Replace(" ", "_") : "default"), 1);
        PlayerPrefs.Save();
        StartRound();
    }

    void EndDemo()
    {
        demoSkipped = false;
        HideAllUI();
        HideLumiCompletely();
        PlayerPrefs.SetInt("MG1DemoSeen_" + (GameManager.Instance != null ? GameManager.Instance.lumiName.Trim().Replace(" ", "_") : "default"), 1);
        PlayerPrefs.Save();
        StartRound();
    }

    IEnumerator WaitOrSkip(float seconds)
    {
        float e = 0f;
        while (e < seconds && !demoSkipped) { e += Time.deltaTime; yield return null; }
    }

    IEnumerator PlayDemo()
    {
        demoSkipped = false;
        if (demoSkipBtn != null) demoSkipBtn.SetActive(true);
        lumiCorner?.SetActive(true);

        ShowLumi(L("Hi! I am Lumi. Let me show you how to play!", "مَرْحَباً! أَنَا لُومِي. دَعْنِي أُرِيكَ كَيْفَ تَلْعَب!"));
        yield return StartCoroutine(WaitOrSkip(5f)); if (demoSkipped) yield break;

        ShowLumi(L("Look at this fruit carefully!", "اُنْظُرْ إِلَى هَذِهِ الثَّمَرَةِ بِعِنَايَة!"));
        if (fruitDisplayPanel != null && targetFruitSprites.Length > 0)
        {
            fruitDisplayPanel.SetActive(true);
            fruitImage.sprite = targetFruitSprites[0]; fruitImage.preserveAspect = true;
            fruitNameText.text = FruitName(0);
            fruitDisplayPanel.transform.localScale = Vector3.zero;
            fruitDisplayPanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }
        yield return StartCoroutine(WaitOrSkip(5f)); if (demoSkipped) yield break;

        ShowLumi(L("When you are ready, press the Next button!", "عِنْدَمَا تَكُونُ مُسْتَعِدًّا اِضْغَطْ التَّالِي!"));
        if (nextBtn != null) nextBtn.SetActive(true);
        yield return StartCoroutine(WaitOrSkip(4f)); if (demoSkipped) yield break;

        if (nextBtn != null) nextBtn.SetActive(false);
        if (fruitDisplayPanel != null) fruitDisplayPanel.SetActive(false);

        ShowLumi(L("Okay, the grid is coming now.", "حَسَنًا، تَأْتِي الشَّبَكَةُ الآن."));
        yield return StartCoroutine(WaitOrSkip(3f)); if (demoSkipped) yield break;

        ShowLumi(L("Can you find the fruit hiding in the grid?", "هَلْ تَسْتَطِيعُ إِيجَادَ الثَّمَرَةِ فِي الشَّبَكَة؟"));
        ShowDemoGrid();
        yield return StartCoroutine(WaitOrSkip(4f)); if (demoSkipped) yield break;

        ShowLumi(L("There it is! That is the one to find!", "هَا هِيَ! هَذِهِ هِيَ الَّتِي تَبْحَثُ عَنْهَا!"));
        HighlightDemoCorrectCell();
        yield return StartCoroutine(WaitOrSkip(4f)); if (demoSkipped) yield break;

        if (gridContainer != null) gridContainer.SetActive(false);
        ShowLumi(L("Each level has 2 rounds. Win 1 round to pass the level!", "كُلُّ مُسْتَوًى فِيهِ جَوْلَتَان. اِفُزْ بِجَوْلَةٍ لِتَجْتَازَ الْمُسْتَوَى!"));
        yield return StartCoroutine(WaitOrSkip(5f)); if (demoSkipped) yield break;

        ShowLumi(L("Good luck! You can do it!", "حَظًّا مُوَفَّقًا! أَنْتَ قَادِرٌ عَلَى ذَلِك!"));
        yield return StartCoroutine(WaitOrSkip(3f));
        EndDemo();
    }

    void ShowDemoGrid()
    {
        if (gridContainer == null) return;
        gridContainer.SetActive(true);
        foreach (Transform c in gridContainer.transform) { DOTween.Kill(c); Destroy(c.gameObject); }
        var gl = gridContainer.GetComponent<GridLayoutGroup>();
        if (gl != null) gl.constraintCount = 2;
        for (int i = 0; i < 4; i++)
        {
            if (fruitCellPrefab == null) break;
            GameObject cell = Instantiate(fruitCellPrefab, gridContainer.transform);
            Transform fs = cell.transform.Find("FruitSprite"); if (fs == null) continue;
            Image img = fs.GetComponent<Image>(); if (img == null) continue;
            if (i < targetFruitSprites.Length) { img.sprite = targetFruitSprites[i]; img.preserveAspect = true; }
            Button btn = cell.GetComponent<Button>(); if (btn != null) btn.interactable = false;
        }
    }

    void HighlightDemoCorrectCell()
    {
        if (gridContainer == null || gridContainer.transform.childCount == 0) return;
        Transform first = gridContainer.transform.GetChild(0); if (first == null) return;
        Image img = first.GetComponent<Image>();
        if (img != null) { DOTween.Kill(first); img.DOColor(new Color(0.62f, 0.75f, 0.56f, 1f), 0.4f); }
        DOTween.Kill(first);
        first.DOScale(1.2f, 0.3f).SetLoops(4, LoopType.Yoyo);
    }

    // ── Fixed difficulty settings (used when adaptive is OFF) ─────────────
    // L1: 1 fruit, 2x2, 1s  | L2: 2 fruits, 3x3, 1.5s
    // L3: 3 fruits, 4x4, 2s | L4: 3 fruits, 4x4, 2.5s
    private static readonly float[] kFixedDelay = { 1f, 1.5f, 2f, 2.5f };
    private static readonly int[] kFixedCols = { 2, 3, 4, 4 };
    private static readonly int[] kFixedRows = { 2, 3, 4, 4 };
    private static readonly int[][] kFixedFruits = {
        new int[] { 1, 1 },   // L1: R0=1, R1=1
        new int[] { 2, 2 },   // L2: R0=2, R1=2
        new int[] { 3, 3 },   // L3: R0=3, R1=3
        new int[] { 3, 3 },   // L4: R0=3, R1=3
    };

    // ════════════════════════════════════════════════════════════════════════
    // ROUND MANAGEMENT
    // ════════════════════════════════════════════════════════════════════════

    void StartRound()
    {
        CancelInvoke(nameof(StartRound));
        attemptCount = 0;
        correctlyFound.Clear();
        targetIndices.Clear();
        nextPressed = false;
        roundActive = false;

        HideLumiCompletely();

        // ── Fixed or Adaptive difficulty ─────────────────────────────────
        bool isFixed = !GameManager.IsAdaptiveEnabled();
        if (isFixed)
        {
            // Override adaptive values with fixed settings for this level
            adaptiveDelay = kFixedDelay[currentLevel];
            adaptiveGridCols = kFixedCols[currentLevel];
            adaptiveGridRows = kFixedRows[currentLevel];
            adaptiveFruits[0] = kFixedFruits[currentLevel][0];
            adaptiveFruits[1] = kFixedFruits[currentLevel][1];
        }

        // Order is determined by static config — never adapted away.
        orderMatters = kOrderMatters[currentLevel][currentRound];

        // Reset per-round tracking counters.
        _roundResponseTimeAccum = 0f;
        _roundResponseCount = 0;
        _roundCorrectTaps = 0;
        _roundWrongTaps = 0;
        _roundHints = 0;

        // Fruit count for this round comes entirely from the adaptive array.
        int fruitsCount = adaptiveFruits[currentRound];

        // Build pool of unused targets for this level session.
        List<int> available = new List<int>();
        for (int i = 0; i < targetFruitSprites.Length; i++)
            if (!usedFruitsThisLevel.Contains(i)) available.Add(i);

        if (available.Count < fruitsCount)
        {
            usedFruitsThisLevel.Clear();
            available.Clear();
            for (int i = 0; i < targetFruitSprites.Length; i++) available.Add(i);
        }

        for (int i = 0; i < fruitsCount; i++)
        {
            int pick = Random.Range(0, available.Count);
            targetIndices.Add(available[pick]);
            usedFruitsThisLevel.Add(available[pick]);
            available.RemoveAt(pick);
        }

        Debug.Log("[Round " + (currentRound + 1) + "/" + kTotalRounds + "] START —" +
                  " Fruits:" + fruitsCount +
                  " Grid:" + adaptiveGridCols + "x" + adaptiveGridRows +
                  " Delay:" + adaptiveDelay + "s" +
                  " Order:" + orderMatters);

        UpdateHUD();
        StartCoroutine(ShowFruitsThenGrid());
    }

    // ════════════════════════════════════════════════════════════════════════
    // FRUIT DISPLAY
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator ShowFruitsThenGrid()
    {
        HideAllUI();
        HideLumiCompletely();

        int n = adaptiveFruits[currentRound];   // actual count shown to child

        // Level-specific intro messages that reflect the real adaptive count.
        float readTime = GameManager.IsArabic() ? 4.5f : 3f;
        float readTimeLong = GameManager.IsArabic() ? 5.5f : 3.5f;

        if (currentLevel == 2)
        {
            lumiCorner?.SetActive(true);
            ShowLumi(currentRound == 0
                ? (n == 1
                    ? L("Look at this fruit carefully.", "اُنْظُرْ إِلَى هَذِهِ الثَّمَرَةِ بِعِنَايَة.")
                    : L("Look at these " + n + " fruits.", "اُنْظُرْ إِلَى هَذِهِ الثِّمَارِ الـ" + n + "."))
                : (n == 1
                    ? L("Just one fruit this time.", "ثَمَرَةٌ وَاحِدَةٌ فَقَطْ هَذِهِ الْمَرَّة.")
                    : L("Look at these " + n + " fruits.", "اُنْظُرْ إِلَى هَذِهِ الثِّمَارِ الـ" + n + ".")));
            yield return new WaitForSeconds(readTime);
            HideLumiCompletely();
            yield return new WaitForSeconds(0.3f);
        }
        else if (currentLevel == 3)
        {
            lumiCorner?.SetActive(true);
            ShowLumi(currentRound == 0
                ? (n == 1
                    ? L("Look at this fruit carefully.", "اُنْظُرْ إِلَى هَذِهِ الثَّمَرَةِ بِعِنَايَة.")
                    : L("Look at these " + n + " fruits in order.", "اُنْظُرْ إِلَى هَذِهِ الثِّمَارِ الـ" + n + " بِالتَّرْتِيب."))
                : (n == 1
                    ? L("Just one fruit this round.", "ثَمَرَةٌ وَاحِدَةٌ فَقَطْ هَذِهِ الْجَوْلَة.")
                    : L("Look at these " + n + " fruits.", "اُنْظُرْ إِلَى هَذِهِ الثِّمَارِ الـ" + n + ".")));
            yield return new WaitForSeconds(readTimeLong);
            HideLumiCompletely();
            yield return new WaitForSeconds(0.3f);
        }

        // Display mode driven entirely by adaptive count.
        if (n == 1)
            yield return StartCoroutine(ShowSingleFruit());
        else
            yield return StartCoroutine(ShowSequenceFruits(n));

        HideAllUI();
        HideLumiCompletely();

        lumiCorner?.SetActive(true);
        ShowLumi(L("Okay, the grid is coming now.", "حَسَنًا، تَأْتِي الشَّبَكَةُ الآن."));
        yield return new WaitForSeconds(GameManager.IsArabic() ? 3.5f : 2f);
        HideLumiCompletely();

        // ── The retention delay — core adaptive parameter ──
        yield return new WaitForSeconds(adaptiveDelay);

        lastGridShownTime = Time.time;
        ShowGrid();
    }

    IEnumerator ShowSingleFruit()
    {
        if (fruitDisplayPanel == null || targetIndices.Count == 0) yield break;

        lumiCorner?.SetActive(true);
        ShowLumi(L("Look at this fruit carefully.", "اُنْظُرْ إِلَى هَذِهِ الثَّمَرَةِ بِعِنَايَة."));

        int idx = targetIndices[0];
        fruitDisplayPanel.SetActive(true);
        if (fruitImage != null) { fruitImage.sprite = targetFruitSprites[idx]; fruitImage.preserveAspect = true; }
        if (fruitNameText != null) fruitNameText.text = FruitName(idx);

        // Wait for Lumi intro to finish, then speak fruit name
        yield return StartCoroutine(WaitForLumiAudio(1f, 3f));
        if (LLMService.Instance != null)
            LLMService.Instance.SpeakOnSource(FruitNameRaw(idx), lumiAudio);

        yield return new WaitForSeconds(1.5f);
        if (nextBtn != null) nextBtn.SetActive(true);
        nextPressed = false;
        yield return new WaitUntil(() => nextPressed);

        if (nextBtn != null) nextBtn.SetActive(false);
        HideLumiCompletely();
        DOTween.Kill(fruitDisplayPanel.transform);
        fruitDisplayPanel.SetActive(false);
    }

    /// <summary>
    /// Displays the sequence panel showing exactly <paramref name="visibleCount"/> slots.
    /// Slots beyond visibleCount are hidden so no stale sprite from a previous round is visible.
    /// This is the fix for the mismatch between displayed and expected fruit counts.
    /// </summary>
    IEnumerator ShowSequenceFruits(int visibleCount)
    {
        if (sequencePanel == null) yield break;
        sequencePanel.SetActive(true);

        SetSlot(sequenceSlot1, sequenceFruitImage1, sequenceFruitName1, 0, visibleCount);
        SetSlot(sequenceSlot2, sequenceFruitImage2, sequenceFruitName2, 1, visibleCount);
        SetSlot(sequenceSlot3, sequenceFruitImage3, sequenceFruitName3, 2, visibleCount);

        DOTween.Kill(sequencePanel.transform);
        sequencePanel.transform.localScale = Vector3.zero;
        sequencePanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.5f);

        // Speak each fruit name aloud in sequence
        if (LLMService.Instance != null)
        {
            for (int i = 0; i < visibleCount && i < targetIndices.Count; i++)
            {
                LLMService.Instance.SpeakOnSource(FruitNameRaw(targetIndices[i]), lumiAudio);
                yield return new WaitForSeconds(1.2f);
            }
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        if (sequenceNextBtn != null) sequenceNextBtn.SetActive(true);
        nextPressed = false;
        yield return new WaitUntil(() => nextPressed);

        if (sequenceNextBtn != null) sequenceNextBtn.SetActive(false);
        HideLumiCompletely();
        DOTween.Kill(sequencePanel.transform);
        sequencePanel.SetActive(false);
        sequencePanel.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Shows/hides a sequence slot based on whether slotIndex < visibleCount.
    /// Prevents stale sprites from being visible.
    /// </summary>
    /// <summary>
    /// Shows or hides a complete sequence slot.
    /// Hides the ENTIRE slot container (the rounded box) when not needed,
    /// so no empty green box is ever visible.
    /// </summary>
    void SetSlot(GameObject slotContainer, Image imgComp, TextMeshProUGUI labelComp,
                 int slotIndex, int visibleCount)
    {
        bool show = slotIndex < visibleCount && slotIndex < targetIndices.Count;

        // Hide/show the whole container box — this is the key fix.
        if (slotContainer != null) slotContainer.SetActive(show);

        if (imgComp != null)
        {
            if (show) { imgComp.sprite = targetFruitSprites[targetIndices[slotIndex]]; imgComp.preserveAspect = true; }
        }
        if (labelComp != null)
        {
            if (show) labelComp.text = FruitName(targetIndices[slotIndex]);
        }
    }

    /// <summary>Same pattern for the reveal panel slots.</summary>
    void SetRevealSlot(GameObject slotContainer, Image imgComp, TextMeshProUGUI labelComp,
                       int slotIndex, int visibleCount)
    {
        bool show = slotIndex < visibleCount && slotIndex < targetIndices.Count;

        if (slotContainer != null) slotContainer.SetActive(show);

        if (imgComp != null)
        {
            if (show) { imgComp.sprite = targetFruitSprites[targetIndices[slotIndex]]; imgComp.preserveAspect = true; }
        }
        if (labelComp != null)
        {
            if (show) labelComp.text = FruitName(targetIndices[slotIndex]);
        }
    }

    public void OnNextPressed() { nextPressed = true; }

    // ════════════════════════════════════════════════════════════════════════
    // GRID
    // ════════════════════════════════════════════════════════════════════════

    void ShowGrid()
    {
        if (fruitDisplayPanel != null) { DOTween.Kill(fruitDisplayPanel.transform); fruitDisplayPanel.SetActive(false); }
        if (sequencePanel != null) { DOTween.Kill(sequencePanel.transform); sequencePanel.SetActive(false); }
        if (nextBtn != null) nextBtn.SetActive(false);
        if (sequenceNextBtn != null) sequenceNextBtn.SetActive(false);
        HideLumiCompletely();

        if (gridContainer == null) return;
        DOTween.Kill(gridContainer.transform);
        gridContainer.SetActive(true);
        roundActive = true;

        foreach (Transform child in gridContainer.transform)
        {
            if (child == null) continue;
            DOTween.Kill(child);
            Destroy(child.gameObject);
        }

        var gl = gridContainer.GetComponent<GridLayoutGroup>();
        if (gl != null) gl.constraintCount = adaptiveGridCols;

        int total = adaptiveGridCols * adaptiveGridRows;

        // Seed cells with target indices.
        List<int> cells = new List<int>(targetIndices);

        // Build distractor pool: non-target fruit sprites + explicit distractors.
        currentPool = new List<Sprite>();
        for (int a = 0; a < targetFruitSprites.Length; a++)
            if (!targetIndices.Contains(a)) currentPool.Add(targetFruitSprites[a]);
        if (distractorSprites != null)
            foreach (Sprite s in distractorSprites) if (s != null) currentPool.Add(s);

        Shuffle(currentPool);

        int poolIdx = 0;
        while (cells.Count < total)
        {
            if (currentPool.Count == 0) break;
            if (poolIdx >= currentPool.Count) { Shuffle(currentPool); poolIdx = 0; }
            cells.Add(-1000 - poolIdx);
            poolIdx++;
        }

        Shuffle(cells);
        CreateCells(cells);
    }

    void CreateCells(List<int> cells)
    {
        for (int a = 0; a < cells.Count; a++)
        {
            if (fruitCellPrefab == null) break;
            GameObject c = Instantiate(fruitCellPrefab, gridContainer.transform);
            int cv = cells[a];

            CellData cd = c.AddComponent<CellData>();
            cd.cellValue = cv;

            Transform fs = c.transform.Find("FruitSprite"); if (fs == null) continue;
            Image img = fs.GetComponent<Image>(); if (img == null) continue;

            if (cv >= 0)
                img.sprite = targetFruitSprites[cv];
            else
            {
                int pIdx = -(cv + 1000);
                if (pIdx >= 0 && pIdx < currentPool.Count) img.sprite = currentPool[pIdx];
            }
            img.preserveAspect = true;

            int cap = cv;
            Button btn = c.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => OnCellTapped(cap, c));

            DOTween.Kill(c.transform);
            c.transform.localScale = Vector3.zero;
            c.transform.DOScale(1f, 0.15f).SetDelay(a * 0.03f);
        }
    }

    void Shuffle<T>(List<T> list)
    {
        for (int a = list.Count - 1; a > 0; a--)
        {
            int b = Random.Range(0, a + 1);
            T t = list[a]; list[a] = list[b]; list[b] = t;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // CELL TAP
    // ════════════════════════════════════════════════════════════════════════

    void OnCellTapped(int cellValue, GameObject cell)
    {
        if (!roundActive || cell == null) return;

        bool isTarget = cellValue >= 0 &&
                        targetIndices.Contains(cellValue) &&
                        !correctlyFound.Contains(cellValue);

        if (isTarget && orderMatters)
        {
            int expected = correctlyFound.Count;
            isTarget = expected < targetIndices.Count && cellValue == targetIndices[expected];
        }

        if (isTarget)
        {
            totalCorrect++;
            _roundCorrectTaps++;
            float tapRT = Time.time - lastGridShownTime;
            totalResponseTime += tapRT;
            _roundResponseTimeAccum += tapRT;
            responseCount++;
            _roundResponseCount++;
            lastGridShownTime = Time.time;

            correctlyFound.Add(cellValue);

            if (cell.transform != null) { DOTween.Kill(cell.transform); cell.transform.DOScale(1.2f, 0.15f).SetLoops(2, LoopType.Yoyo); }
            Image ci = cell.GetComponent<Image>();
            if (ci != null) ci.DOColor(new Color(0.62f, 0.75f, 0.56f, 1f), 0.3f);

            if (sfxAudio != null && correctSfx != null) sfxAudio.PlayOneShot(correctSfx);
            GameManager.Instance?.AddTempCoins(hintsThisLevel == 0 ? 20 : 12);
            UpdateHUD();

            if (correctlyFound.Count >= targetIndices.Count)
            {
                roundActive = false;
                StartCoroutine(DelayThenOnRoundCorrect());
            }
        }
        else
        {
            totalWrong++;
            _roundWrongTaps++;
            attemptCount++;

            if (cell.transform != null) { DOTween.Kill(cell.transform); cell.transform.DOShakePosition(0.3f, 8f, 15); }
            Image ci = cell.GetComponent<Image>();
            if (ci != null)
                ci.DOColor(new Color(0.95f, 0.71f, 0.70f, 1f), 0.1f)
                  .OnComplete(() => { if (ci != null) ci.DOColor(Color.white, 0.2f); });

            if (sfxAudio != null && wrongSfx != null) sfxAudio.PlayOneShot(wrongSfx);
            HandleWrongAnswer();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // SCAFFOLDING / HINTS
    // ════════════════════════════════════════════════════════════════════════

    void HandleWrongAnswer()
    {
        if (HeartManager.Instance != null && HeartManager.Instance.GetHearts(1) <= 0)
        {
            roundActive = false;
            StartCoroutine(RedirectToWait());
            return;
        }

        if (attemptCount == 1) { hintsThisLevel++; _roundHints++; StartCoroutine(Attempt1Eliminate()); }
        else if (attemptCount == 2) { hintsThisLevel++; _roundHints++; StartCoroutine(Attempt2MemoryReplay()); }
        else { roundActive = false; StartCoroutine(RoundLost()); }
    }

    IEnumerator Attempt1Eliminate()
    {
        // Hint — NOT an adaptive change, so do NOT write to changeLog here.
        lumiCorner?.SetActive(true);
        ShowLumi(L("I can help you with this one.", "يُمْكِنُنِي مُسَاعَدَتُكَ فِي هَذِه."));
        yield return new WaitForSeconds(2f);
        HideSpeechBubble();
        yield return new WaitForSeconds(0.5f);

        // Collect candidates: only cells that are NOT targets at all (distractors only)
        // Do NOT dim remaining unfound targets OR already correctly found targets
        List<GameObject> candidates = new List<GameObject>();
        foreach (Transform child in gridContainer.transform)
        {
            if (child == null) continue;
            CellData cd = child.GetComponent<CellData>();
            if (cd == null) continue;

            bool isAnyTarget = cd.cellValue >= 0 && targetIndices.Contains(cd.cellValue);
            if (!isAnyTarget)
                candidates.Add(child.gameObject);
        }

        Shuffle(candidates);

        // Eliminate roughly half but keep at least one distractor visible.
        int elimCount = Mathf.Clamp(candidates.Count / 2, 2, Mathf.Max(1, candidates.Count - 1));
        for (int i = 0; i < elimCount && i < candidates.Count; i++)
        {
            GameObject wc = candidates[i]; if (wc == null) continue;

            // Dim the cell box itself
            Image img = wc.GetComponent<Image>();
            if (img != null) img.DOColor(new Color(0.75f, 0.75f, 0.75f, 0.4f), 0.5f);

            // Also dim all child Images (fruit sprites) inside the cell
            foreach (Image childImg in wc.GetComponentsInChildren<Image>())
                if (childImg.gameObject != wc) // skip the cell's own Image
                    childImg.DOColor(new Color(0.75f, 0.75f, 0.75f, 0.4f), 0.5f);

            Button btn = wc.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    IEnumerator Attempt2MemoryReplay()
    {
        // Hint — NOT an adaptive change.
        roundActive = false;
        if (gridContainer != null) gridContainer.SetActive(false);

        int n = adaptiveFruits[currentRound];

        if (n == 1)
        {
            lumiCorner?.SetActive(true);
            ShowLumi(L("Take another look at this fruit.", "اُنْظُرْ مَرَّةً أُخْرَى إِلَى هَذِهِ الثَّمَرَة."));

            if (fruitDisplayPanel != null && targetIndices.Count > 0)
            {
                int idx = targetIndices[0];
                fruitDisplayPanel.SetActive(true);
                if (fruitImage != null) { fruitImage.sprite = targetFruitSprites[idx]; fruitImage.preserveAspect = true; }
                if (fruitNameText != null) fruitNameText.text = FruitName(idx);
                DOTween.Kill(fruitDisplayPanel.transform);
                fruitDisplayPanel.transform.localScale = Vector3.zero;
                fruitDisplayPanel.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
            }
            yield return new WaitForSeconds(3f);
            if (fruitDisplayPanel != null) { DOTween.Kill(fruitDisplayPanel.transform); fruitDisplayPanel.SetActive(false); }
            HideLumiCompletely();
        }
        else
        {
            lumiCorner?.SetActive(true);
            ShowLumi(orderMatters
                ? L("Try to remember the order.", "حَاوِلْ أَنْ تَتَذَكَّرَ التَّرْتِيب.")
                : "Take another look at these fruits.");
            yield return new WaitForSeconds(1.5f);
            HideLumiCompletely();

            if (sequencePanel != null)
            {
                sequencePanel.SetActive(true);
                SetSlot(sequenceSlot1, sequenceFruitImage1, sequenceFruitName1, 0, n);
                SetSlot(sequenceSlot2, sequenceFruitImage2, sequenceFruitName2, 1, n);
                SetSlot(sequenceSlot3, sequenceFruitImage3, sequenceFruitName3, 2, n);
                DOTween.Kill(sequencePanel.transform);
                sequencePanel.transform.localScale = Vector3.zero;
                sequencePanel.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
                yield return new WaitForSeconds(3f);
                DOTween.Kill(sequencePanel.transform);
                sequencePanel.SetActive(false);
                sequencePanel.transform.localScale = Vector3.one;
            }
        }

        yield return new WaitForSeconds(0.5f);
        lastGridShownTime = Time.time;   // reset timer from replay moment
        if (gridContainer != null) gridContainer.SetActive(true);
        roundActive = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    // ROUND LOST
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator RoundLost()
    {
        if (sfxAudio != null && wrongSfx != null) sfxAudio.PlayOneShot(wrongSfx);

        foreach (Transform child in gridContainer.transform)
        {
            if (child == null) continue;
            Button btn = child.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
        if (gridContainer != null) gridContainer.SetActive(false);

        yield return StartCoroutine(ShowRevealPanel());
        yield return new WaitForSeconds(0.5f);

        EvaluatePerformance(false);
        NextRound();
    }

    IEnumerator ShowRevealPanel()
    {
        int n = adaptiveFruits[currentRound];

        if (n == 1)
        {
            if (revealPanelSingle == null) yield break;
            if (revealTitleSingle != null) revealTitleSingle.text = L("This was the answer!", "كَانَتْ هَذِهِ هِيَ الإِجَابَة!");

            if (targetIndices.Count > 0)
            {
                int idx = targetIndices[0];
                if (revealFruitImageSingle != null) { revealFruitImageSingle.sprite = targetFruitSprites[idx]; revealFruitImageSingle.preserveAspect = true; }
                if (revealFruitNameSingle != null) revealFruitNameSingle.text = FruitName(idx);
            }

            DOTween.Kill(revealPanelSingle.transform);
            revealPanelSingle.SetActive(true);
            revealPanelSingle.transform.localScale = Vector3.zero;
            revealPanelSingle.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(4f);
            DOTween.Kill(revealPanelSingle.transform);
            revealPanelSingle.transform.DOScale(0f, 0.25f)
                .OnComplete(() => { if (revealPanelSingle != null) revealPanelSingle.SetActive(false); });
        }
        else
        {
            if (revealPanelSequence == null) yield break;
            if (revealTitleSequence != null) revealTitleSequence.text = L("This was the answer!", "كَانَتْ هَذِهِ هِيَ الإِجَابَة!");

            SetRevealSlot(revealSlot1, revealSeqFruitImage1, revealSeqFruitName1, 0, n);
            SetRevealSlot(revealSlot2, revealSeqFruitImage2, revealSeqFruitName2, 1, n);
            SetRevealSlot(revealSlot3, revealSeqFruitImage3, revealSeqFruitName3, 2, n);

            DOTween.Kill(revealPanelSequence.transform);
            revealPanelSequence.SetActive(true);
            revealPanelSequence.transform.localScale = Vector3.zero;
            revealPanelSequence.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(4f);
            DOTween.Kill(revealPanelSequence.transform);
            revealPanelSequence.transform.DOScale(0f, 0.25f)
                .OnComplete(() => { if (revealPanelSequence != null) revealPanelSequence.SetActive(false); });
        }

        yield return new WaitForSeconds(0.3f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PERFORMANCE EVALUATION & ADAPTIVE DIFFICULTY
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator DelayThenOnRoundCorrect()
    {
        yield return new WaitForSeconds(0.5f);
        OnRoundCorrect();
    }

    void OnRoundCorrect()
    {
        _roundsSinceMotivation++;

        // Show LLM motivation every 2 correct rounds or after the child struggled
        bool shouldMotivate = _roundsSinceMotivation >= 2 || attemptCount >= 1;
        if (shouldMotivate)
        {
            _roundsSinceMotivation = 0;
            StartCoroutine(ShowCorrectWithMotivation());
        }
        else
        {
            StartCoroutine(QuickCorrectAndNext());
        }
    }

    IEnumerator QuickCorrectAndNext()
    {
        EvaluatePerformance(true);
        roundsCorrect++;
        lumiCorner?.SetActive(true);
        ShowLumi(L("That is the correct answer.", "هَذِهِ هِيَ الإِجَابَةُ الصَّحِيحَة."));
        yield return StartCoroutine(WaitForLumiAudio(1f, 3f));
        yield return new WaitForSeconds(2f);
        HideSpeechBubble();
        lumiCorner?.SetActive(false);
        yield return new WaitForSeconds(0.2f);
        NextRound();
    }

    IEnumerator ShowCorrectWithMotivation()
    {
        EvaluatePerformance(true);
        roundsCorrect++;

        // Request LLM motivational message immediately
        bool struggled = attemptCount >= 2;
        bool perfect = attemptCount == 0;
        var motCtx = new LLMService.MotivationContext
        {
            game = 1,
            currentLevel = currentLevel,
            recentWrongCount = attemptCount,
            recentCorrectCount = roundsCorrect,
            justSucceededAfterStruggle = struggled,
            situation = struggled ? "struggling" :   // used hints/had wrong taps then succeeded
                        perfect ? "perfect" :   // got it right first try
                                    "improving"       // got it right with minor effort
        };

        string motivationText = null;
        bool motivationReady = false;
        if (LLMService.Instance != null)
            LLMService.Instance.GetMotivation(motCtx,
                msg => { motivationText = msg; motivationReady = true; });

        // Wait up to 6 seconds for LLM — no fallback, we want real LLM messages only
        float waited = 0f;
        while (!motivationReady && waited < 6f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Show "this is the correct answer" first
        lumiCorner?.SetActive(true);
        ShowLumi(L("That is the correct answer.", "هَذِهِ هِيَ الإِجَابَةُ الصَّحِيحَة."));
        yield return StartCoroutine(WaitForLumiAudio(1f, 3f));
        yield return new WaitForSeconds(2f);  // minimum reading time
        HideSpeechBubble();
        yield return new WaitForSeconds(0.2f);

        // Only show motivation if LLM responded — no hardcoded fallback
        if (motivationReady && !string.IsNullOrEmpty(motivationText))
        {
            lumiCorner?.SetActive(true);
            // Shape each line independently to prevent line-order reversal
            string raw = motivationText;
            string shaped = GameManager.IsArabic()
                ? ShapeArabicLines(motivationText)
                : motivationText;
            ShowLumi(shaped, raw);
            yield return StartCoroutine(WaitForLumiAudio(1f, 4f));
            yield return new WaitForSeconds(2f);  // minimum reading time
            HideSpeechBubble();
            yield return new WaitForSeconds(0.3f);
        }

        NextRound();
    }

    void EvaluatePerformance(bool success)
    {
        // Skip adaptive evaluation entirely in fixed difficulty mode
        if (!GameManager.IsAdaptiveEnabled()) return;

        string outcome;

        if (success && attemptCount == 0)
        {
            perfScore++;
            outcome = "PERFECT (0 wrong) → perfScore=" + perfScore;
        }
        else if (!success || attemptCount >= 2)
        {
            perfScore--;
            outcome = (success ? "POOR (2+ wrong)" : "FAILED") + " → perfScore=" + perfScore;
        }
        else
        {
            outcome = "AVERAGE (1 wrong) → perfScore unchanged=" + perfScore;
        }

        Debug.Log("[Perf] Round " + (currentRound + 1) + ": " + outcome);

        // Record this round's data.
        float roundAvgRT = _roundResponseCount > 0
            ? _roundResponseTimeAccum / _roundResponseCount : 0f;

        _roundRecords.Add(new LocalDataManager.RoundRecord
        {
            roundNumber = currentRound + 1,
            fruitsShown = adaptiveFruits[currentRound],
            orderRequired = orderMatters,
            correctTaps = _roundCorrectTaps,
            wrongTaps = _roundWrongTaps,
            responseTimeSec = roundAvgRT,
            hintsUsed = _roundHints,
            passed = success,
            adaptiveStateSnapshot = "Delay:" + adaptiveDelay + "s|Grid:" +
                                    adaptiveGridCols + "x" + adaptiveGridRows +
                                    "|Fruits:" + adaptiveFruits[currentRound]
        });

        ApplyAdaptiveDifficulty();

        // Persist after every round so cross-level carry works.
        SaveAdaptiveState();
    }

    void ApplyAdaptiveDifficulty()
    {
        List<string> changes = new List<string>();

        if (perfScore >= 2)
        {
            // ── HARDER ──────────────────────────────────────────────────
            // Longer delay = harder working memory load.
            float prev = adaptiveDelay;
            adaptiveDelay = Mathf.Min(adaptiveDelay + kDelayStep, kDelayMax);
            if (!Mathf.Approximately(adaptiveDelay, prev))
                changes.Add("Delay " + prev.ToString("F1") + "→" + adaptiveDelay.ToString("F1") + "s");

            // Larger grid = harder visual search.
            int pc = adaptiveGridCols, pr = adaptiveGridRows;
            adaptiveGridCols = Mathf.Min(adaptiveGridCols + 1, kGridMax);
            adaptiveGridRows = Mathf.Min(adaptiveGridRows + 1, kGridMax);
            if (adaptiveGridCols != pc || adaptiveGridRows != pr)
                changes.Add("Grid " + pc + "x" + pr + "→" + adaptiveGridCols + "x" + adaptiveGridRows);

            // More fruits = higher memory load (per-round, respecting constraints).
            for (int r = 0; r < 2; r++)
            {
                int pf = adaptiveFruits[r];
                adaptiveFruits[r] = Mathf.Min(adaptiveFruits[r] + 1, GetMaxFruits(r));
                if (adaptiveFruits[r] != pf)
                    changes.Add("R" + r + "Fruits " + pf + "→" + adaptiveFruits[r]);
            }

            perfScore = 0;   // reset so threshold must be earned again

            Debug.Log("[Adaptive] ▲ HARDER applied. New state —" +
                      " Delay:" + adaptiveDelay + "s" +
                      " Grid:" + adaptiveGridCols + "x" + adaptiveGridRows +
                      " Fruits:[R0=" + adaptiveFruits[0] + " R1=" + adaptiveFruits[1] + "]");
        }
        else if (perfScore <= -1)
        {
            // ── EASIER ──────────────────────────────────────────────────
            float prev = adaptiveDelay;
            adaptiveDelay = Mathf.Max(adaptiveDelay - kDelayStep, kDelayMin);
            if (!Mathf.Approximately(adaptiveDelay, prev))
                changes.Add("Delay " + prev.ToString("F1") + "→" + adaptiveDelay.ToString("F1") + "s");

            int pc = adaptiveGridCols, pr = adaptiveGridRows;
            adaptiveGridCols = Mathf.Max(adaptiveGridCols - 1, kGridMin);
            adaptiveGridRows = Mathf.Max(adaptiveGridRows - 1, kGridMin);
            if (adaptiveGridCols != pc || adaptiveGridRows != pr)
                changes.Add("Grid " + pc + "x" + pr + "→" + adaptiveGridCols + "x" + adaptiveGridRows);

            for (int r = 0; r < 2; r++)
            {
                int pf = adaptiveFruits[r];
                adaptiveFruits[r] = Mathf.Max(adaptiveFruits[r] - 1, GetMinFruits(r));
                if (adaptiveFruits[r] != pf)
                    changes.Add("R" + r + "Fruits " + pf + "→" + adaptiveFruits[r]);
            }

            perfScore = 0;

            Debug.Log("[Adaptive] ▼ EASIER applied. New state —" +
                      " Delay:" + adaptiveDelay + "s" +
                      " Grid:" + adaptiveGridCols + "x" + adaptiveGridRows +
                      " Fruits:[R0=" + adaptiveFruits[0] + " R1=" + adaptiveFruits[1] + "]");
        }
        else
        {
            Debug.Log("[Adaptive] — No change (perfScore=" + perfScore + ")." +
                      " Delay:" + adaptiveDelay + "s" +
                      " Grid:" + adaptiveGridCols + "x" + adaptiveGridRows +
                      " Fruits:[R0=" + adaptiveFruits[0] + " R1=" + adaptiveFruits[1] + "]");
        }

        if (changes.Count > 0)
        {
            string entry = "R" + (currentRound + 1) + "[" + string.Join("|", changes) + "]";
            changeLog = (changeLog == "None") ? entry : changeLog + "," + entry;
        }
    }

    // ── Learning-objective constraints ────────────────────────────────────

    int GetMaxFruits(int round)
    {
        // L1 & L2: up to 2 fruits.
        if (currentLevel <= 1) return 2;
        // L3 & L4 R0: up to 3 fruits.
        // L3 & L4 R1: up to 2 fruits.
        return (round == 0) ? 3 : 2;
    }

    int GetMinFruits(int round)
    {
        // L1 & L2: down to 1 fruit.
        if (currentLevel <= 1) return 1;
        // L3 & L4 R0: minimum 2 (learning objective — must see multiple stimuli).
        // L3 & L4 R1: down to 1 fruit.
        return (round == 0) ? 2 : 1;
    }

    // ════════════════════════════════════════════════════════════════════════
    // ROUND PROGRESSION
    // ════════════════════════════════════════════════════════════════════════

    void NextRound()
    {
        currentRound++;
        CancelInvoke(nameof(StartRound));
        HideAllUI();
        HideLumiCompletely();
        UpdateHUD();

        if (currentRound >= kTotalRounds)
            StartCoroutine(EndLevel());
        else
            Invoke(nameof(StartRound), 0.5f);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LEVEL END
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator EndLevel()
    {
        usedFruitsThisLevel.Clear();
        yield return new WaitForSeconds(0.3f);

        float avgRT = responseCount > 0 ? totalResponseTime / responseCount : 0f;

        // ── Save session data locally (no Firebase/internet required) ─────
        string playerName = GameManager.Instance != null
            ? GameManager.Instance.lumiName : "Unknown";

        var sessionData = new LocalDataManager.Game1SessionData
        {
            playerName = playerName,
            sessionId = LocalDataManager.SessionId,
            currentLevel = currentLevel,
            totalCorrect = totalCorrect,
            totalWrong = totalWrong,
            avgResponseTimeSec = avgRT,
            hintsUsed = hintsThisLevel,
            adaptiveChanges = changeLog,
            finalDelay = adaptiveDelay,
            finalGrid = adaptiveGridCols + "x" + adaptiveGridRows,
            finalFruitsR0 = adaptiveFruits[0],
            finalFruitsR1 = adaptiveFruits[1],
            difficultyMode = GameManager.IsAdaptiveEnabled() ? "adaptive" : "fixed"
        };

        // Track hint1Used and hint2Used from round records
        sessionData.hint1Used = _roundRecords.Sum(r => r.hint1Used);
        sessionData.hint2Used = _roundRecords.Sum(r => r.hint2Used);

        // Add per-round records that were collected during play.
        foreach (var r in _roundRecords) sessionData.rounds.Add(r);

        bool passed = roundsCorrect >= kPassRequired;
        sessionData.levelPassed = passed;

        if (passed)
        {
            sessionData.starsEarned = (roundsCorrect == 2 && hintsThisLevel == 0) ? 3
                                    : (roundsCorrect == 2) ? 2 : 1;
        }

        LocalDataManager.SaveGame1Session(sessionData);

        Debug.Log("[LocalData] Saved — Correct:" + totalCorrect +
                  " Wrong:" + totalWrong +
                  " AvgRT:" + avgRT.ToString("F2") + "s" +
                  " Hints:" + hintsThisLevel +
                  " Changes:" + changeLog);

        if (passed)
        {
            int stars = (roundsCorrect == 2 && hintsThisLevel == 0) ? 3
                      : (roundsCorrect == 2) ? 2
                      : 1;

            if (GameManager.Instance != null)
            {
                if (stars > GameManager.Instance.mg1Stars[currentLevel])
                    GameManager.Instance.mg1Stars[currentLevel] = stars;

                // Cap perfScore at 1 between levels — prevents immediate HARDER
                // firing on the very first round of the next level
                perfScore = Mathf.Min(perfScore, 1);
                GameManager.Instance.mg1PerformanceScore = perfScore;

                GameManager.Instance.currentGame = 1;
                GameManager.Instance.lastSessionCoins = GameManager.Instance.tempCoins;
                GameManager.Instance.CommitCoins();
                GameManager.Instance.UnlockNextLevel(1, currentLevel);
                GameManager.Instance.SaveData();
            }

            SceneManager.LoadScene("LevelCompleteScene");
        }
        else
        {
            // ── On level fail: reset per-round fruit counts to this level's base ──
            // The child will retry the level, so fruit counts should restart from
            // the level's base values — not from whatever adaptation reached.
            // Delay and grid size are NOT reset — they reflect general ability
            // and should persist even on retry.
            // perfScore is reset to 0 so the child isn't double-penalised:
            // the EASIER already fired during the rounds; we don't want another
            // EASIER to fire immediately at the start of the retry.
            adaptiveFruits[0] = kBaseFruits[currentLevel][0];
            adaptiveFruits[1] = kBaseFruits[currentLevel][1];
            perfScore = 0;
            _roundsSinceMotivation = 0;
            SaveAdaptiveState();

            Debug.Log("[Adaptive] Level failed — fruit counts reset to base for retry." +
                      " R0=" + adaptiveFruits[0] + " R1=" + adaptiveFruits[1]);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.currentGame = 1;
                GameManager.Instance.DiscardCoins();
                GameManager.Instance.totalCoins += 10;
                GameManager.Instance.SaveData();
            }

            HeartManager.Instance?.LoseHeart(1);
            yield return new WaitForSeconds(0.3f);

            SceneManager.LoadScene(
                (HeartManager.Instance != null && HeartManager.Instance.GetHearts(1) <= 0)
                    ? "WaitScene" : "LevelLostScene");
        }
    }

    IEnumerator RedirectToWait()
    {
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("WaitScene");
    }

    // ── Back button ───────────────────────────────────────────────────────
    public void OnBackPressed()
    {
        SceneManager.LoadScene("LevelSelectMG1");
    }
}