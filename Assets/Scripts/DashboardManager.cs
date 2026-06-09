using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DashboardManager — Progress Dashboard Scene
///
/// Reads all session data from LocalDataManager, populates the UI,
/// and triggers the LLM therapist report via LLMService.GetTherapistReport().
///
/// UI Structure (build this in Unity):
///   Canvas
///     Header
///       BackButton
///       TitleText
///       PlayerNameText
///     TabRow
///       FruitFinderTabBtn
///       EmotionQuestTabBtn
///     ScrollView
///       Content (Vertical Layout Group)
///         G1Panel (panel for Game 1 content)
///           SummaryCards (Horizontal Layout Group)
///             AccuracyCard, LevelCard, HintsCard, RTCard, TrendCard
///           LevelsLabel
///           LevelsContainer (Grid Layout Group — 4 cells)
///             LevelCard_L1, LevelCard_L2, LevelCard_L3, LevelCard_L4
///           TrendLabel
///           TrendChartContainer
///           AdaptiveLabel
///           AdaptiveContainer (Vertical Layout Group)
///         G2Panel (panel for Game 2 content)
///           (same structure as G1Panel)
///     ReportSection
///       ReportLabel
///       GenerateReportButton
///       ReportStatusText
///       ReportScrollView
///         ReportText
/// </summary>
public class DashboardManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("Header")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI sessionCountText;

    [Header("Tabs")]
    public GameObject g1Panel;
    public GameObject g2Panel;
    public GameObject emPanel;          // Emotion Mirror panel
    public Button fruitFinderTabBtn;
    public Button emotionQuestTabBtn;
    public Button emotionMirrorTabBtn;
    public Color tabActiveColor = new Color(0.23f, 0.43f, 0.07f, 1f);
    public Color tabInactiveColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    public Color tabActiveTextColor = Color.white;
    public Color tabInactiveTextColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    // ── Game 1 summary cards ──────────────────────────────────────────────
    [Header("G1 — Summary Cards")]
    public TextMeshProUGUI g1AccuracyValue;
    public TextMeshProUGUI g1AccuracySub;
    public TextMeshProUGUI g1LevelsValue;
    public TextMeshProUGUI g1HintsValue;
    public TextMeshProUGUI g1RTValue;
    public TextMeshProUGUI g1TrendValue;

    // ── Game 1 level cards (one per level) ───────────────────────────────
    [Header("G1 — Level Cards (assign 4)")]
    public TextMeshProUGUI[] g1LevelAccuracy = new TextMeshProUGUI[4];
    public TextMeshProUGUI[] g1LevelDetail = new TextMeshProUGUI[4];
    public Image[] g1LevelBar = new Image[4];
    public TextMeshProUGUI[] g1LevelStars = new TextMeshProUGUI[4];

    // ── Game 1 adaptive section ───────────────────────────────────────────
    [Header("G1 — Adaptive History")]
    public Transform g1AdaptiveContainer;
    public GameObject adaptiveRowPrefab;   // prefab: horizontal row with 3 TMP labels

    // ── Game 2 summary cards ──────────────────────────────────────────────
    [Header("G2 — Summary Cards")]
    public TextMeshProUGUI g2AccuracyValue;
    public TextMeshProUGUI g2AccuracySub;
    public TextMeshProUGUI g2LevelsValue;
    public TextMeshProUGUI g2HintsValue;
    public TextMeshProUGUI g2TrendValue;
    public TextMeshProUGUI g2ConfusionCount;

    // ── Game 2 level cards (one per level) ───────────────────────────────
    [Header("G2 — Level Cards (assign 6)")]
    public TextMeshProUGUI[] g2LevelName = new TextMeshProUGUI[6];
    public TextMeshProUGUI[] g2LevelAccuracy = new TextMeshProUGUI[6];
    public TextMeshProUGUI[] g2LevelDetail = new TextMeshProUGUI[6];
    public Image[] g2LevelBar = new Image[6];
    public TextMeshProUGUI[] g2LevelStars = new TextMeshProUGUI[6];

    // ── Game 2 confusion section ──────────────────────────────────────────
    [Header("G2 — Confusion Patterns")]
    public Transform g2ConfusionContainer;
    public GameObject confusionRowPrefab;  // prefab: row with label + count

    // ── Report section ────────────────────────────────────────────────────
    [Header("Report")]
    public Button generateReportBtn;
    public TextMeshProUGUI generateReportBtnText;
    public TextMeshProUGUI reportStatusText;
    public TextMeshProUGUI reportOutputText;
    public GameObject reportOutputPanel;

    [Header("Emotion Mirror Panel")]
    public TextMeshProUGUI emTotalAttemptsText;
    public TextMeshProUGUI emAccuracyText;
    public TextMeshProUGUI emResultsText;
    public TextMeshProUGUI emNotPlayedText;

    // ── Colors ────────────────────────────────────────────────────────────
    [Header("Colors")]
    public Color barColorGood = new Color(0.39f, 0.60f, 0.13f, 1f);
    public Color barColorBad = new Color(0.84f, 0.35f, 0.19f, 1f);
    public Color barColorNeutral = new Color(0.70f, 0.70f, 0.70f, 1f);

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private List<LocalDataManager.Game1SessionData> _g1Sessions;
    private List<LocalDataManager.Game2SessionData> _g2Sessions;
    private string _playerName;

    private static readonly string[] kEmotionNames =
        { "Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted" };

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
        _playerName = GameManager.Instance != null
            ? GameManager.Instance.lumiName : "Player";

        if (playerNameText != null)
            playerNameText.text = _playerName + "'s Progress";

        _g1Sessions = LocalDataManager.LoadAllGame1Sessions(_playerName);
        _g2Sessions = LoadAllGame2Sessions(_playerName);

        int total = _g1Sessions.Count + _g2Sessions.Count;
        if (sessionCountText != null)
            sessionCountText.text = total + " sessions recorded";

        PopulateG1();
        PopulateG2();

        ShowTab(1);

        if (reportOutputPanel != null)
            reportOutputPanel.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LOAD — GAME 2 SESSIONS  (mirrors LoadAllGame1Sessions in LocalDataManager)
    // ════════════════════════════════════════════════════════════════════════

    public static List<LocalDataManager.Game2SessionData> LoadAllGame2Sessions(string playerName)
    {
        var results = new List<LocalDataManager.Game2SessionData>();
        string root = System.IO.Path.Combine(
            Application.persistentDataPath, "SunvaleData");

        if (!System.IO.Directory.Exists(root)) return results;

        string sanitised = playerName.Replace(" ", "_")
                                     .Replace("/", "_")
                                     .Replace("\\", "_");
        if (string.IsNullOrEmpty(sanitised)) sanitised = "Unknown";

        foreach (string folder in System.IO.Directory.GetDirectories(root))
        {
            if (!System.IO.Path.GetFileName(folder).StartsWith(sanitised)) continue;
            foreach (string file in System.IO.Directory.GetFiles(folder, "game2_L*.json"))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(file);
                    var data = JsonUtility.FromJson<LocalDataManager.Game2SessionData>(json);
                    if (data != null) results.Add(data);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Dashboard] Could not read " + file + ": " + e.Message);
                }
            }
        }
        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // TABS
    // ════════════════════════════════════════════════════════════════════════

    public void OnFruitFinderTabPressed() => ShowTab(1);
    public void OnEmotionQuestTabPressed() => ShowTab(2);
    public void OnEmotionMirrorTabPressed() => ShowTab(3);

    void ShowTab(int tab)
    {
        if (g1Panel != null) g1Panel.SetActive(tab == 1);
        if (g2Panel != null) g2Panel.SetActive(tab == 2);
        if (emPanel != null) emPanel.SetActive(tab == 3);

        SetTabStyle(fruitFinderTabBtn, tab == 1);
        SetTabStyle(emotionQuestTabBtn, tab == 2);
        SetTabStyle(emotionMirrorTabBtn, tab == 3);

        if (tab == 3) PopulateEMPanel();
    }

    void SetTabStyle(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? tabActiveColor : tabInactiveColor;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = active ? tabActiveTextColor : tabInactiveTextColor;
    }

    // ════════════════════════════════════════════════════════════════════════
    // POPULATE — GAME 1
    // ════════════════════════════════════════════════════════════════════════

    void PopulateG1()
    {
        var s = _g1Sessions;

        if (s.Count == 0)
        {
            SetText(g1AccuracyValue, "—");
            SetText(g1LevelsValue, "0/4");
            SetText(g1HintsValue, "0");
            SetText(g1RTValue, "—");
            SetText(g1TrendValue, "No data yet");
            return;
        }

        int totalC = s.Sum(d => d.totalCorrect);
        int totalW = s.Sum(d => d.totalWrong);
        int total = totalC + totalW;
        int acc = total > 0 ? Mathf.RoundToInt((float)totalC / total * 100) : 0;
        int passed = s.Count(d => d.levelPassed);
        int hints = s.Sum(d => d.hintsUsed);
        float avgRT = s.Average(d => d.avgResponseTimeSec);

        SetText(g1AccuracyValue, acc + "%");
        SetText(g1AccuracySub, totalC + " correct / " + total + " total");
        SetText(g1LevelsValue, passed + "/4");
        SetText(g1HintsValue, hints.ToString());
        SetText(g1RTValue, avgRT.ToString("F1") + "s");
        SetText(g1TrendValue, GetTrend(s, d => Acc(d.totalCorrect, d.totalCorrect + d.totalWrong)));

        // Level cards — one per session (each session = one level attempt)
        for (int i = 0; i < 4; i++)
        {
            var match = s.Where(d => d.currentLevel == i).OrderByDescending(d => d.timestamp).FirstOrDefault();
            if (match == null)
            {
                SetText(g1LevelAccuracy, i, "—");
                SetText(g1LevelDetail, i, "Not played");
                SetText(g1LevelStars, i, "☆☆☆");
                SetBarFill(g1LevelBar, i, 0f, barColorNeutral);
                continue;
            }
            int la = Acc(match.totalCorrect, match.totalCorrect + match.totalWrong);
            SetText(g1LevelAccuracy, i, la + "%");
            SetText(g1LevelDetail, i, match.totalCorrect + "✓  " + match.totalWrong + "✗  ·  " + match.avgResponseTimeSec.ToString("F1") + "s avg");
            SetText(g1LevelStars, i, Stars(match.starsEarned));
            SetBarFill(g1LevelBar, i, la / 100f, la >= 60 ? barColorGood : barColorBad);
        }

        // Adaptive history rows
        if (g1AdaptiveContainer != null && adaptiveRowPrefab != null)
        {
            foreach (Transform child in g1AdaptiveContainer) Destroy(child.gameObject);
            foreach (var d in s)
            {
                var row = Instantiate(adaptiveRowPrefab, g1AdaptiveContainer);
                var labels = row.GetComponentsInChildren<TextMeshProUGUI>();
                if (labels.Length >= 3)
                {
                    labels[0].text = d.sessionId;
                    labels[1].text = "Delay " + d.finalDelay + "s  ·  Grid " + d.finalGrid;
                    labels[2].text = d.adaptiveChanges == "None" ? "—" : d.adaptiveChanges;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // POPULATE — GAME 2
    // ════════════════════════════════════════════════════════════════════════

    void PopulateG2()
    {
        var s = _g2Sessions;

        if (s.Count == 0)
        {
            SetText(g2AccuracyValue, "—");
            SetText(g2LevelsValue, "0/6");
            SetText(g2HintsValue, "0");
            SetText(g2TrendValue, "No data yet");
            SetText(g2ConfusionCount, "0");
            return;
        }

        int totalC = s.Sum(d => d.correctAnswers);
        int totalW = s.Sum(d => d.wrongAnswers);
        int total = totalC + totalW;
        int acc = total > 0 ? Mathf.RoundToInt((float)totalC / total * 100) : 0;
        int passed = s.Count(d => d.levelPassed);
        int hints = s.Sum(d => d.hintsUsed);

        // Build confusion map
        var confusionMap = new Dictionary<string, int>();
        foreach (var d in s)
        {
            if (d.confusionPairs == null) continue;
            foreach (var p in d.confusionPairs)
            {
                string key = p.correctEmotion + " → " + p.selectedEmotion;
                if (!confusionMap.ContainsKey(key)) confusionMap[key] = 0;
                confusionMap[key] += p.count;
            }
        }

        SetText(g2AccuracyValue, acc + "%");
        SetText(g2AccuracySub, totalC + " correct / " + total + " total");
        SetText(g2LevelsValue, passed + "/6");
        SetText(g2HintsValue, hints.ToString());
        SetText(g2TrendValue, GetTrend(s, d => Acc(d.correctAnswers, d.correctAnswers + d.wrongAnswers)));
        SetText(g2ConfusionCount, confusionMap.Count + " pairs");

        // Level cards — one per emotion level
        for (int i = 0; i < 6; i++)
        {
            var match = s.Where(d => d.currentLevel == i).OrderByDescending(d => d.timestamp).FirstOrDefault();
            if (g2LevelName.Length > i && g2LevelName[i] != null)
                g2LevelName[i].text = "Level " + (i + 1) + " · " + kEmotionNames[i];

            if (match == null)
            {
                SetText(g2LevelAccuracy, i, "—");
                SetText(g2LevelDetail, i, "Not played");
                SetText(g2LevelStars, i, "☆☆☆");
                SetBarFill(g2LevelBar, i, 0f, barColorNeutral);
                continue;
            }
            int la = Acc(match.correctAnswers, match.correctAnswers + match.wrongAnswers);
            SetText(g2LevelAccuracy, i, la + "%");
            SetText(g2LevelDetail, i, match.correctAnswers + "✓  " + match.wrongAnswers + "✗  ·  " + match.hintsUsed + " hints");
            SetText(g2LevelStars, i, Stars(match.starsEarned));
            SetBarFill(g2LevelBar, i, la / 100f, match.levelPassed ? barColorGood : barColorBad);
        }

        // Confusion rows
        if (g2ConfusionContainer != null && confusionRowPrefab != null)
        {
            foreach (Transform child in g2ConfusionContainer) Destroy(child.gameObject);
            foreach (var kvp in confusionMap.OrderByDescending(x => x.Value))
            {
                var row = Instantiate(confusionRowPrefab, g2ConfusionContainer);
                var labels = row.GetComponentsInChildren<TextMeshProUGUI>();
                if (labels.Length >= 2)
                {
                    labels[0].text = kvp.Key;
                    labels[1].text = kvp.Value + "×";
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // REPORT GENERATION
    // ════════════════════════════════════════════════════════════════════════

    public void OnGenerateReportPressed()
    {
        StartCoroutine(GenerateReport());
    }

    IEnumerator GenerateReport()
    {
        if (generateReportBtn != null) generateReportBtn.interactable = false;
        if (generateReportBtnText != null) generateReportBtnText.text = "Generating...";
        if (reportStatusText != null) reportStatusText.text = "Asking Gemini to analyse session data...";
        if (reportOutputPanel != null) reportOutputPanel.SetActive(false);

        var ctx = new LLMService.ReportContext
        {
            playerName = _playerName,
            sessionDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
            totalSessions = _g1Sessions.Count + _g2Sessions.Count
        };

        // ── Game 1 ────────────────────────────────────────────────────────
        if (_g1Sessions.Count > 0)
        {
            var ordered = _g1Sessions.OrderBy(d => d.timestamp).ToList();
            var first = ordered.First();
            var last = ordered.Last();

            int g1TotalRounds = _g1Sessions.Count * 2; // 2 rounds per level

            ctx.g1TotalCorrect = _g1Sessions.Sum(d => d.totalCorrect);
            ctx.g1TotalWrong = _g1Sessions.Sum(d => d.totalWrong);
            ctx.g1AvgResponseTimeSec = (float)_g1Sessions.Average(d => d.avgResponseTimeSec);
            ctx.g1HintsUsed = _g1Sessions.Sum(d => d.hintsUsed);
            ctx.g1Hint1Used = _g1Sessions.Sum(d => d.hint1Used);
            ctx.g1Hint2Used = _g1Sessions.Sum(d => d.hint2Used);
            ctx.g1HintDependencyRatio = g1TotalRounds > 0
                ? (float)ctx.g1HintsUsed / g1TotalRounds : 0f;
            ctx.g1LevelsCompleted = _g1Sessions.Count(d => d.levelPassed);
            ctx.g1LevelsFailed = _g1Sessions.Count(d => !d.levelPassed);

            // Accuracy trend
            int firstTotal = first.totalCorrect + first.totalWrong;
            int lastTotal = last.totalCorrect + last.totalWrong;
            ctx.g1FirstSessionAcc = firstTotal > 0 ? (float)first.totalCorrect / firstTotal * 100f : 0f;
            ctx.g1LastSessionAcc = lastTotal > 0 ? (float)last.totalCorrect / lastTotal * 100f : 0f;

            // Response time trend
            ctx.g1FirstSessionRT = first.avgResponseTimeSec;
            ctx.g1LastSessionRT = last.avgResponseTimeSec;

            // Adaptive changes breakdown
            ctx.g1TimesHarderFired = _g1Sessions.Sum(d =>
                d.adaptiveChanges != null
                    ? System.Text.RegularExpressions.Regex.Matches(d.adaptiveChanges, "HARDER|▲").Count
                    : 0);
            ctx.g1TimesEasierFired = _g1Sessions.Sum(d =>
                d.adaptiveChanges != null
                    ? System.Text.RegularExpressions.Regex.Matches(d.adaptiveChanges, "EASIER|▼").Count
                    : 0);
            ctx.g1DifficultyIncreased = last.finalDelay > first.finalDelay;
            ctx.g1AdaptiveChanges = _g1Sessions.Count(d => d.adaptiveChanges != null && d.adaptiveChanges != "None")
                + " of " + _g1Sessions.Count + " sessions had difficulty changes";

            // Max memory capacity — highest fruits correctly handled
            ctx.g1MaxFruitsReached = _g1Sessions
                .SelectMany(d => d.rounds ?? new System.Collections.Generic.List<LocalDataManager.RoundRecord>())
                .Where(r => r.passed)
                .Select(r => r.fruitsShown)
                .DefaultIfEmpty(0)
                .Max();

            // Max delay reached in a passed round
            ctx.g1MaxDelayReached = _g1Sessions
                .Where(d => d.levelPassed)
                .Select(d => d.finalDelay)
                .DefaultIfEmpty(0f)
                .Max();
        }

        // ── Game 2 ────────────────────────────────────────────────────────
        if (_g2Sessions.Count > 0)
        {
            var ordered = _g2Sessions.OrderBy(d => d.timestamp).ToList();
            var first = ordered.First();
            var last = ordered.Last();

            ctx.g2TotalCorrect = _g2Sessions.Sum(d => d.correctAnswers);
            ctx.g2TotalWrong = _g2Sessions.Sum(d => d.wrongAnswers);
            ctx.g2AvgResponseTimeSec = (float)_g2Sessions.Average(d => d.avgResponseTimeSec);
            ctx.g2HintsUsed = _g2Sessions.Sum(d => d.hintsUsed);
            ctx.g2Hint1Used = _g2Sessions.Sum(d => d.hint1Used);
            ctx.g2Hint2Used = _g2Sessions.Sum(d => d.hint2Used);
            ctx.g2HintDependencyRatio = _g2Sessions.Count > 0
                ? (float)ctx.g2HintsUsed / _g2Sessions.Count : 0f;
            ctx.g2LevelsCompleted = _g2Sessions.Count(d => d.levelPassed);
            ctx.g2LevelsFailed = _g2Sessions.Count(d => !d.levelPassed);

            // Accuracy trend
            int firstTotal = first.correctAnswers + first.wrongAnswers;
            int lastTotal = last.correctAnswers + last.wrongAnswers;
            ctx.g2FirstSessionAcc = firstTotal > 0 ? (float)first.correctAnswers / firstTotal * 100f : 0f;
            ctx.g2LastSessionAcc = lastTotal > 0 ? (float)last.correctAnswers / lastTotal * 100f : 0f;

            // Per-emotion accuracy
            var emotions = new[] { "Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted" };
            ctx.g2PerEmotionAccuracy = new System.Collections.Generic.List<string>();
            foreach (var emotion in emotions)
            {
                var emotionSessions = _g2Sessions.Where(d => d.emotionTested == emotion).ToList();
                if (emotionSessions.Count == 0) continue;
                int c = emotionSessions.Sum(d => d.correctAnswers);
                int t = c + emotionSessions.Sum(d => d.wrongAnswers);
                float acc = t > 0 ? (float)c / t * 100f : 0f;
                ctx.g2PerEmotionAccuracy.Add(emotion + ": " + acc.ToString("F0") + "%"
                    + " (" + emotionSessions.Sum(d => d.hintsUsed) + " hints)");
            }

            // Strong / weak emotions
            ctx.g2StrongEmotions = _g2Sessions
                .Where(d => d.levelPassed && d.hintsUsed == 0)
                .Select(d => d.emotionTested)
                .Distinct().ToList();

            ctx.g2WeakEmotions = _g2Sessions
                .Where(d => !d.levelPassed || d.hintsUsed >= 2)
                .Select(d => d.emotionTested)
                .Distinct().ToList();

            // Confusion pairs
            var confMap = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var d in _g2Sessions)
            {
                if (d.confusionPairs == null) continue;
                foreach (var p in d.confusionPairs)
                {
                    string key = p.correctEmotion + " confused with " + p.selectedEmotion;
                    if (!confMap.ContainsKey(key)) confMap[key] = 0;
                    confMap[key] += p.count;
                }
            }
            ctx.g2ConfusionPairs = confMap
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key + " (" + x.Value + " times)")
                .ToList();

            // Level repetition analysis for Game 2
            ctx.g2LevelRepetitions = BuildG2LevelRepetitions(_g2Sessions);
        }

        // ── Grid + stimuli performance (Game 1) ───────────────────────────
        if (_g1Sessions.Count > 0)
            ctx.g1GridPerformance = BuildGridPerformance(_g1Sessions);

        // ── Level repetition (Game 1) ─────────────────────────────────────
        if (_g1Sessions.Count > 0)
            ctx.g1LevelRepetitions = BuildG1LevelRepetitions(_g1Sessions);

        // ── Emotion Mirror ────────────────────────────────────────────────
        // Load from PlayerPrefs — EmotionMirrorManager saves results there
        ctx.emotionMirrorPlayed = PlayerPrefs.GetInt("EmotionMirror_Played_" + _playerName, 0) == 1;
        if (ctx.emotionMirrorPlayed)
        {
            ctx.emotionMirrorTotalAttempts = PlayerPrefs.GetInt("EmotionMirror_Total_" + _playerName, 0);
            ctx.emotionMirrorCorrect = PlayerPrefs.GetInt("EmotionMirror_Correct_" + _playerName, 0);
            ctx.emotionMirrorIncorrect = ctx.emotionMirrorTotalAttempts - ctx.emotionMirrorCorrect;
            string resultsRaw = PlayerPrefs.GetString("EmotionMirror_Results_" + _playerName, "");
            ctx.emotionMirrorResults = string.IsNullOrEmpty(resultsRaw)
                ? new List<string>()
                : new List<string>(resultsRaw.Split('|'));
            string improvRaw = PlayerPrefs.GetString("EmotionMirror_Improvements_" + _playerName, "");
            ctx.emotionMirrorImprovements = string.IsNullOrEmpty(improvRaw)
                ? new List<string>()
                : new List<string>(improvRaw.Split('|'));
        }

        // Call LLMService — routes to Gemini
        string report = null;
        bool done = false;
        LLMService.Instance.GetTherapistReport(ctx, result => { report = result; done = true; });

        float waited = 0f;
        while (!done && waited < 30f) { waited += Time.deltaTime; yield return null; }

        if (!string.IsNullOrEmpty(report))
        {
            if (reportOutputText != null) reportOutputText.text = report;
            if (reportOutputPanel != null) reportOutputPanel.SetActive(true);
            if (reportStatusText != null) reportStatusText.text = "Report generated · " + System.DateTime.Now.ToString("HH:mm");
            if (reportOutputPanel != null)
            {
                reportOutputPanel.transform.localScale = Vector3.zero;
                reportOutputPanel.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
            }
        }
        else
        {
            if (reportStatusText != null)
                reportStatusText.text = "Report failed — check Gemini API key in LLMService Inspector.";
        }

        if (generateReportBtn != null) generateReportBtn.interactable = true;
        if (generateReportBtnText != null) generateReportBtnText.text = "Regenerate report";
    }

    // ════════════════════════════════════════════════════════════════════════
    // REPORT HELPERS
    // ════════════════════════════════════════════════════════════════════════

    List<string> BuildGridPerformance(List<LocalDataManager.Game1SessionData> sessions)
    {
        // Group all rounds by grid + fruits + delay combination
        var groups = new Dictionary<string, (int correct, int total, float delaySum, int delayCount)>();
        foreach (var session in sessions)
        {
            if (session.rounds == null) continue;
            foreach (var round in session.rounds)
            {
                string key = session.finalGrid + " | " +
                             round.fruitsShown + " fruit" + (round.fruitsShown > 1 ? "s" : "") +
                             " | delay " + session.finalDelay.ToString("F1") + "s";
                if (!groups.ContainsKey(key))
                    groups[key] = (0, 0, 0f, 0);
                var g = groups[key];
                int c = round.correctTaps;
                int t = round.correctTaps + round.wrongTaps;
                groups[key] = (g.correct + c, g.total + t,
                               g.delaySum + session.finalDelay, g.delayCount + 1);
            }
        }

        var result = new List<string>();
        foreach (var kvp in groups.OrderBy(x => x.Key))
        {
            float acc = kvp.Value.total > 0
                ? (float)kvp.Value.correct / kvp.Value.total * 100f : 0f;
            result.Add(kvp.Key + ": " + acc.ToString("F0") + "% accuracy" +
                       " (" + kvp.Value.total + " attempts)");
        }
        return result;
    }

    List<string> BuildG1LevelRepetitions(List<LocalDataManager.Game1SessionData> sessions)
    {
        var result = new List<string>();
        // Group by level, ordered by attempt number
        for (int level = 0; level < 4; level++)
        {
            var attempts = sessions
                .Where(d => d.currentLevel == level)
                .OrderBy(d => d.attemptNumber)
                .ToList();
            if (attempts.Count <= 1) continue; // only report levels attempted more than once

            var sb = new StringBuilder();
            sb.Append("Level " + (level + 1) + ": " + attempts.Count + " attempts — ");
            var accs = attempts.Select(d => {
                int t = d.totalCorrect + d.totalWrong;
                return t > 0 ? (float)d.totalCorrect / t * 100f : 0f;
            }).ToList();
            sb.Append(string.Join(" → ", accs.Select(a => a.ToString("F0") + "%")));

            float trend = accs.Last() - accs.First();
            sb.Append(trend > 5f ? " (improving)" : trend < -5f ? " (declining)" : " (stable)");
            result.Add(sb.ToString());
        }
        return result;
    }

    List<string> BuildG2LevelRepetitions(List<LocalDataManager.Game2SessionData> sessions)
    {
        var result = new List<string>();
        var emotions = new[] { "Happy", "Sad", "Fear", "Angry", "Surprised", "Disgusted" };
        for (int level = 0; level < 6; level++)
        {
            var attempts = sessions
                .Where(d => d.currentLevel == level)
                .OrderBy(d => d.timestamp)
                .ToList();
            if (attempts.Count <= 1) continue;

            var sb = new StringBuilder();
            string emotionName = level < emotions.Length ? emotions[level] : "Level " + (level + 1);
            sb.Append(emotionName + ": " + attempts.Count + " attempts — ");
            var accs = attempts.Select(d => {
                int t = d.correctAnswers + d.wrongAnswers;
                return t > 0 ? (float)d.correctAnswers / t * 100f : 0f;
            }).ToList();
            sb.Append(string.Join(" → ", accs.Select(a => a.ToString("F0") + "%")));
            float trend = accs.Last() - accs.First();
            sb.Append(trend > 5f ? " (improving)" : trend < -5f ? " (declining)" : " (stable)");
            result.Add(sb.ToString());
        }
        return result;
    }

    void PopulateEMPanel()
    {
        bool played = PlayerPrefs.GetInt("EmotionMirror_Played_" + _playerName, 0) == 1;

        if (emNotPlayedText != null)
            emNotPlayedText.gameObject.SetActive(!played);

        if (!played) return;

        int total = PlayerPrefs.GetInt("EmotionMirror_Total_" + _playerName, 0);
        int correct = PlayerPrefs.GetInt("EmotionMirror_Correct_" + _playerName, 0);
        float acc = total > 0 ? (float)correct / total * 100f : 0f;

        if (emTotalAttemptsText != null)
            emTotalAttemptsText.text = total + " attempts  ·  " + correct + " correct";

        if (emAccuracyText != null)
            emAccuracyText.text = acc.ToString("F0") + "%";

        if (emResultsText != null)
        {
            string raw = PlayerPrefs.GetString("EmotionMirror_Results_" + _playerName, "");
            if (!string.IsNullOrEmpty(raw))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var entry in raw.Split('|'))
                    sb.AppendLine(entry.Trim());
                emResultsText.text = sb.ToString();
            }
            else
            {
                emResultsText.text = "No results recorded yet.";
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ════════════════════════════════════════════════════════════════════════

    public void OnBackPressed()
    {
        SceneManager.LoadScene("HubScene");
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    static int Acc(int correct, int total) =>
        total == 0 ? 0 : Mathf.RoundToInt((float)correct / total * 100);

    static string Stars(int n) =>
        new string('★', Mathf.Clamp(n, 0, 3)) +
        new string('☆', Mathf.Clamp(3 - n, 0, 3));

    static string GetTrend<T>(List<T> sessions, System.Func<T, int> accFn)
    {
        if (sessions.Count < 2) return "Not enough data";
        int first = accFn(sessions[0]);
        int last = accFn(sessions[sessions.Count - 1]);
        int diff = last - first;
        if (diff > 5) return "↑ Improving";
        if (diff < -5) return "↓ Declining";
        return "→ Stable";
    }

    void SetText(TextMeshProUGUI field, string value)
    {
        if (field != null) field.text = value;
    }

    void SetText(TextMeshProUGUI[] arr, int i, string value)
    {
        if (arr != null && i < arr.Length && arr[i] != null) arr[i].text = value;
    }

    void SetBarFill(Image[] arr, int i, float fill, Color color)
    {
        if (arr == null || i >= arr.Length || arr[i] == null) return;
        arr[i].fillAmount = Mathf.Clamp01(fill);
        arr[i].color = color;
    }
}