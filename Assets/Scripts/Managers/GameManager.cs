using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Arabic")]
    public TMP_FontAsset arabicFallbackFont;

    public TMP_FontAsset englishDefaultFont;

    public int totalCoins = 0;
    public int tempCoins = 0;

    [HideInInspector] public bool languageArabic = false;

    public bool[] mg1LevelsUnlocked = new bool[4] { true, false, false, false };
    public bool[] mg2LevelsUnlocked = new bool[6] { true, false, false, false, false, false };

    [Header("MG1 — Adaptive State (persists across levels)")]
    public float mg1AdaptiveDelay = -1f;
    public int mg1AdaptiveGridCols = -1;
    public int mg1AdaptiveGridRows = -1;
    public int mg1AdaptiveFruitsR0 = -1;
    public int mg1AdaptiveFruitsR1 = -1;
    public int mg1PerformanceScore = 0;

    [Header("MG1 — Current Params (legacy)")]
    public float mg1CurrentDelay = -1f;
    public int mg1CurrentGridCols = -1;
    public int mg1CurrentGridRows = -1;
    public int mg1CurrentFruitCount = -1;
    public bool mg1CurrentOrderReq = false;

    [Header("MG1 — Round Tracking")]
    public float[] mg1RoundResponseTimes = new float[2];
    public int[] mg1RoundWrongAttempts = new int[2];
    public int[] mg1RoundHintsUsed = new int[2];
    public int mg1CorrectStreak = 0;

    [Header("MG1 — Dashboard Data")]
    public float[] mg1ResponseTimes = new float[4];
    public int[] mg1HintsUsed = new int[4];

    [Header("MG2 — Dashboard Data")]
    public float[] mg2ResponseTimes = new float[6];
    public int[] mg2AttemptCounts = new int[6];

    [Header("MG2 — Confusion Tracking")]
    public string[] mg2ConfusionKeys = new string[50];
    public int[] mg2ConfusionCounts = new int[50];
    public int mg2ConfusionCount = 0;

    [Header("Session Tracking")]
    public int totalSessionsPlayed = 0;

    public int[] mg1Stars = { 0, 0, 0, 0 };
    public int[] mg2Stars = new int[6];
    public int[] mg2RoundsPlayed = new int[6];
    public int[] mg2RoundsCorrect = new int[6];
    public int[] mg2HintsUsed = new int[6];
    public int[] mg2SessionsPerLevel = new int[6];

    public int currentLevel = 0;
    public int currentGame = 0;

    public bool[] friendsRescued = { false, false, false, false };

    public string lumiName = "Lumi";
    public int lumiColorIndex = 0;
    public int lumiGender = 0;

    public int lastSessionCoins = 0;

    public static List<string> GetRegisteredPlayers()
    {
        string raw = PlayerPrefs.GetString("RegisteredPlayers", "");
        if (string.IsNullOrEmpty(raw)) return new List<string>();
        var list = new List<string>(raw.Split('|'));
        list.RemoveAll(s => string.IsNullOrWhiteSpace(s));
        return list;
    }

    static void RegisterPlayer(string playerName)
    {
        List<string> players = GetRegisteredPlayers();
        if (!players.Contains(playerName))
        {
            players.Add(playerName);
            PlayerPrefs.SetString("RegisteredPlayers", string.Join("|", players));
            PlayerPrefs.Save();
        }
    }

    static string SafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        return name.Trim().Replace("|", "").Replace(" ", "_");
    }

    static string K(string key, string playerName) => key + "_" + SafeName(playerName);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyFontNextFrame());
    }

    IEnumerator ApplyFontNextFrame()
    {
        ApplyArabicFallbackFont(languageArabic);
        yield return new WaitForSeconds(0.3f);
        ApplyArabicFallbackFont(languageArabic);
    }

    public void ApplyArabicFallbackFont(bool add)
    {
        TMP_FontAsset targetFont = add ? arabicFallbackFont : englishDefaultFont;

        if (targetFont == null)
        {
            Debug.LogWarning(add
                ? "[GameManager] arabicFallbackFont not assigned."
                : "[GameManager] englishDefaultFont not assigned — assign Bobogie Groovy SDF in Inspector.");
            return;
        }

        TextMeshProUGUI[] allTMPs = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int count = 0;
        foreach (var tmp in allTMPs)
        {
            if (tmp == null) continue;
            if (tmp.gameObject.scene.name == null) continue;
            if (tmp.gameObject.scene.name == "") continue;
            if (tmp.gameObject.CompareTag("SkipArabicFont")) continue;
            tmp.font = targetFont;
            count++;
        }

        Debug.Log($"[GameManager] {(add ? "Arabic (NotoNaskh)" : "English (Bobogie Groovy)")} font applied to {count} TMP components.");
    }

    public void AddTempCoins(int amount) { tempCoins += amount; }
    public void DeductTempCoins(int amount) { tempCoins = Mathf.Max(0, tempCoins - amount); }

    public void CommitCoins()
    {
        tempCoins = Mathf.Max(10, tempCoins);
        totalCoins += tempCoins;
        tempCoins = 0;
        SaveData();
    }

    public void DiscardCoins() { tempCoins = 0; }

    public void UnlockNextLevel(int game, int level)
    {
        if (game == 1)
        {
            int next = level + 1;
            if (next < mg1LevelsUnlocked.Length) mg1LevelsUnlocked[next] = true;
        }
        else
        {
            int next = level + 1;
            if (next < mg2LevelsUnlocked.Length) mg2LevelsUnlocked[next] = true;
        }
        SaveData();
    }

    public void SaveData()
    {
        string p = lumiName;

        RegisterPlayer(p);

        PlayerPrefs.SetString(K("LumiName", p), lumiName);
        PlayerPrefs.SetInt(K("LumiColor", p), lumiColorIndex);
        PlayerPrefs.SetInt(K("LumiGender", p), lumiGender);
        PlayerPrefs.SetInt(K("TotalCoins", p), totalCoins);

        SaveMG1Progress();
        for (int i = 0; i < 4; i++)
            PlayerPrefs.SetInt(K("FriendRescued_" + i, p), friendsRescued[i] ? 1 : 0);

        for (int i = 0; i < 6; i++)
        {
            PlayerPrefs.SetInt(K("MG2Stars_" + i, p), mg2Stars[i]);
            PlayerPrefs.SetInt(K("MG2Unlock_" + i, p), mg2LevelsUnlocked[i] ? 1 : 0);
        }

        PlayerPrefs.SetFloat(K("MG1_AdaptDelay", p), mg1AdaptiveDelay);
        PlayerPrefs.SetInt(K("MG1_AdaptGridCols", p), mg1AdaptiveGridCols);
        PlayerPrefs.SetInt(K("MG1_AdaptGridRows", p), mg1AdaptiveGridRows);
        PlayerPrefs.SetInt(K("MG1_AdaptFruitsR0", p), mg1AdaptiveFruitsR0);
        PlayerPrefs.SetInt(K("MG1_AdaptFruitsR1", p), mg1AdaptiveFruitsR1);
        PlayerPrefs.SetInt(K("MG1_PerfScore", p), mg1PerformanceScore);
        PlayerPrefs.SetInt(K("LanguageArabic", p), languageArabic ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        string p = lumiName;

        lumiColorIndex = PlayerPrefs.GetInt(K("LumiColor", p), 0);
        totalCoins = PlayerPrefs.GetInt(K("TotalCoins", p), 0);

        LoadMG1Progress();
        for (int i = 0; i < 4; i++)
            friendsRescued[i] = PlayerPrefs.GetInt(K("FriendRescued_" + i, p), 0) == 1;

        for (int i = 0; i < 6; i++)
        {
            mg2Stars[i] = PlayerPrefs.GetInt(K("MG2Stars_" + i, p), 0);
            mg2LevelsUnlocked[i] = PlayerPrefs.GetInt(K("MG2Unlock_" + i, p), i == 0 ? 1 : 0) == 1;
        }

        mg1AdaptiveDelay = PlayerPrefs.GetFloat(K("MG1_AdaptDelay", p), -1f);
        mg1AdaptiveGridCols = PlayerPrefs.GetInt(K("MG1_AdaptGridCols", p), -1);
        mg1AdaptiveGridRows = PlayerPrefs.GetInt(K("MG1_AdaptGridRows", p), -1);
        mg1AdaptiveFruitsR0 = PlayerPrefs.GetInt(K("MG1_AdaptFruitsR0", p), -1);
        mg1AdaptiveFruitsR1 = PlayerPrefs.GetInt(K("MG1_AdaptFruitsR1", p), -1);
        mg1PerformanceScore = PlayerPrefs.GetInt(K("MG1_PerfScore", p), 0);
        languageArabic = PlayerPrefs.GetInt(K("LanguageArabic", p), 0) == 1;
    }

    public static bool IsArabic()
        => Instance != null && Instance.languageArabic;

    public static void SetLanguageArabic(bool arabic)
    {
        if (Instance == null) return;
        Instance.languageArabic = arabic;
        Instance.SaveData();
    }

    public static bool IsAdaptiveEnabled()
        => PlayerPrefs.GetInt("GlobalAdaptiveDifficulty", 1) == 1;

    public static string MG1ModeKey()
        => IsAdaptiveEnabled() ? "Adaptive" : "Fixed";

    public static void SetAdaptiveEnabled(bool value)
    {
        PlayerPrefs.SetInt("GlobalAdaptiveDifficulty", value ? 1 : 0);
        PlayerPrefs.Save();

        if (Instance != null)
            Instance.LoadMG1Progress();
    }

    public void LoadMG1Progress()
    {
        string p = lumiName;
        string mode = MG1ModeKey();
        for (int i = 0; i < 4; i++)
        {
            mg1Stars[i] = PlayerPrefs.GetInt(K("MG1Stars_" + mode + "_" + i, p), 0);
            mg1LevelsUnlocked[i] = PlayerPrefs.GetInt(K("MG1Unlock_" + mode + "_" + i, p), i == 0 ? 1 : 0) == 1;
        }
    }

    public void SaveMG1Progress()
    {
        string p = lumiName;
        string mode = MG1ModeKey();
        for (int i = 0; i < 4; i++)
        {
            PlayerPrefs.SetInt(K("MG1Stars_" + mode + "_" + i, p), mg1Stars[i]);
            PlayerPrefs.SetInt(K("MG1Unlock_" + mode + "_" + i, p), mg1LevelsUnlocked[i] ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    public static bool IsBackgroundEnabled()
        => PlayerPrefs.GetInt("GlobalBackgroundsEnabled", 1) == 1;

    public static void SetBackgroundEnabled(bool value)
    {
        PlayerPrefs.SetInt("GlobalBackgroundsEnabled", value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResetMG1AdaptiveState()
    {
        mg1AdaptiveDelay = -1f;
        mg1AdaptiveGridCols = -1;
        mg1AdaptiveGridRows = -1;
        mg1AdaptiveFruitsR0 = -1;
        mg1AdaptiveFruitsR1 = -1;
        mg1PerformanceScore = 0;
        SaveData();
    }

    public void ResetCurrentPlayer()
    {
        totalCoins = 0;
        lumiColorIndex = 0;
        mg1Stars = new int[4];
        mg2Stars = new int[6];
        friendsRescued = new bool[4];
        mg1LevelsUnlocked = new bool[4] { true, false, false, false };
        mg2LevelsUnlocked = new bool[6] { true, false, false, false, false, false };

        string safeName = SafeName(lumiName);
        PlayerPrefs.DeleteKey("HubIntroSeen_" + safeName);
        PlayerPrefs.DeleteKey("MG1DemoSeen_" + safeName);
        PlayerPrefs.DeleteKey("MG2DemoSeen_" + safeName);
        PlayerPrefs.Save();

        ResetMG1AdaptiveState();
    }
}