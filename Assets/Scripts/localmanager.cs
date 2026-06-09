using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// LocalDataManager — replaces Firebase with local JSON storage.
///
/// Why local JSON instead of Firebase?
/// • Works completely offline — no internet required
/// • No SDK dependencies or initialization failures
/// • Files are human-readable and can be opened in Excel/any text editor
/// • Therapist can access session files directly from the device
/// • No API keys, no quotas, no connection errors
///
/// Storage location:
///   Android/iOS: Application.persistentDataPath (survives app updates)
///   Windows/Mac editor: same path, shown in console on first save
///
/// File structure:
///   /SunvaleData/
///     {playerName}_{sessionId}/
///       game1_session.json
///       game2_session.json
///       adaptive_state.json
/// </summary>
public static class LocalDataManager
{
    // ── Path helpers ──────────────────────────────────────────────────────

    static string RootPath =>
        Path.Combine(Application.persistentDataPath, "SunvaleData");

    static string PlayerFolder(string playerName, string sessionId) =>
        Path.Combine(RootPath, SanitiseName(playerName) + "_" + sessionId);

    static string SanitiseName(string name) =>
        string.IsNullOrEmpty(name) ? "Unknown"
            : name.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");

    // ── Session ID (generated once per app launch, persists in memory) ───

    static string _sessionId;
    public static string SessionId
    {
        get
        {
            if (string.IsNullOrEmpty(_sessionId))
                _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmm");
            return _sessionId;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // DATA MODELS
    // ════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class Game1SessionData
    {
        public string playerName;
        public string sessionId;
        public string timestamp;
        public int currentLevel;
        public int attemptNumber;        // how many times this level has been attempted

        // Round-by-round performance
        public List<RoundRecord> rounds = new List<RoundRecord>();

        // Level totals
        public int totalCorrect;
        public int totalWrong;
        public float avgResponseTimeSec;
        public int hintsUsed;
        public int hint1Used;            // elimination hints
        public int hint2Used;            // memory replay hints
        public bool levelPassed;
        public int starsEarned;

        // Final adaptive state at end of this level
        public float finalDelay;
        public string finalGrid;
        public int finalFruitsR0;
        public int finalFruitsR1;

        // All adaptive changes that fired this level
        public string adaptiveChanges;

        // Which difficulty mode was active: "adaptive" or "fixed"
        public string difficultyMode;
    }

    [Serializable]
    public class RoundRecord
    {
        public int roundNumber;
        public int fruitsShown;
        public bool orderRequired;
        public int correctTaps;
        public int wrongTaps;
        public float responseTimeSec;
        public int hintsUsed;
        public int hint1Used;
        public int hint2Used;
        public bool passed;
        public string adaptiveStateSnapshot;
        public List<string> wrongFruitsTapped = new List<string>();
    }

    [Serializable]
    public class Game2SessionData
    {
        public string playerName;
        public string sessionId;
        public string timestamp;
        public int currentLevel;
        public int attemptNumber;
        public string emotionTested;

        public int correctAnswers;
        public int wrongAnswers;
        public float avgResponseTimeSec;
        public int hintsUsed;
        public int hint1Used;
        public int hint2Used;
        public bool levelPassed;
        public int starsEarned;

        public List<ConfusionEntry> confusionPairs = new List<ConfusionEntry>();
    }

    [Serializable]
    public class ConfusionEntry
    {
        public string correctEmotion;
        public string selectedEmotion;
        public int count;
    }

    [Serializable]
    public class AdaptiveStateData
    {
        public string playerName;
        public string lastUpdated;
        public float delay;
        public int gridCols;
        public int gridRows;
        public int fruitsR0;
        public int fruitsR1;
        public int perfScore;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SAVE — GAME 1
    // ════════════════════════════════════════════════════════════════════════

    public static void SaveGame1Session(Game1SessionData data)
    {
        data.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string folder = PlayerFolder(data.playerName, data.sessionId);
        // Include attemptNumber in filename so retries don't overwrite previous attempts.
        string path = Path.Combine(folder,
            "game1_L" + data.currentLevel + "_A" + data.attemptNumber + ".json");
        WriteJson(folder, path, data);
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.SendGame1Session(data);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SAVE — GAME 2
    // ════════════════════════════════════════════════════════════════════════

    public static void SaveGame2Session(Game2SessionData data)
    {
        data.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string folder = PlayerFolder(data.playerName, data.sessionId);
        string path = Path.Combine(folder,
            "game2_L" + data.currentLevel + "_A" + data.attemptNumber + ".json");
        WriteJson(folder, path, data);
        if (FirebaseManager.Instance != null) FirebaseManager.Instance.SendGame2Session(data);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SAVE — ADAPTIVE STATE  (called after every round)
    // ════════════════════════════════════════════════════════════════════════

    public static void SaveAdaptiveState(AdaptiveStateData data)
    {
        data.lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string folder = PlayerFolder(data.playerName, SessionId);
        string path = Path.Combine(folder, "adaptive_state.json");
        WriteJson(folder, path, data);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LOAD — ADAPTIVE STATE
    // ════════════════════════════════════════════════════════════════════════

    public static AdaptiveStateData LoadAdaptiveState(string playerName)
    {
        string folder = PlayerFolder(playerName, SessionId);
        string path = Path.Combine(folder, "adaptive_state.json");

        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<AdaptiveStateData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LocalData] Could not load adaptive state: " + e.Message);
            return null;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // LOAD — ALL GAME 1 SESSIONS FOR A PLAYER (for dashboard/report)
    // ════════════════════════════════════════════════════════════════════════

    public static List<Game1SessionData> LoadAllGame1Sessions(string playerName)
    {
        var results = new List<Game1SessionData>();
        string root = RootPath;
        if (!Directory.Exists(root)) return results;

        string sanitised = SanitiseName(playerName);
        foreach (string folder in Directory.GetDirectories(root))
        {
            if (!Path.GetFileName(folder).StartsWith(sanitised)) continue;
            // Match both old format (game1_L0.json) and new format (game1_L0_A1.json)
            foreach (string file in Directory.GetFiles(folder, "game1_L*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<Game1SessionData>(json);
                    if (data != null) results.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LocalData] Could not read " + file + ": " + e.Message);
                }
            }
        }
        // Sort by timestamp so first/last session comparisons are correct
        results.Sort((a, b) => string.Compare(a.timestamp, b.timestamp, StringComparison.Ordinal));
        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // LOAD — ALL GAME 2 SESSIONS FOR A PLAYER (for dashboard/report)
    // ════════════════════════════════════════════════════════════════════════

    public static List<Game2SessionData> LoadAllGame2Sessions(string playerName)
    {
        var results = new List<Game2SessionData>();
        string root = RootPath;
        if (!Directory.Exists(root)) return results;

        string sanitised = SanitiseName(playerName);
        foreach (string folder in Directory.GetDirectories(root))
        {
            if (!Path.GetFileName(folder).StartsWith(sanitised)) continue;
            foreach (string file in Directory.GetFiles(folder, "game2_L*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonUtility.FromJson<Game2SessionData>(json);
                    if (data != null) results.Add(data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LocalData] Could not read " + file + ": " + e.Message);
                }
            }
        }
        results.Sort((a, b) => string.Compare(a.timestamp, b.timestamp, StringComparison.Ordinal));
        return results;
    }

    // ════════════════════════════════════════════════════════════════════════
    // INTERNAL
    // ════════════════════════════════════════════════════════════════════════

    static void WriteJson<T>(string folder, string path, T data)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(path, json);
            Debug.Log("[LocalData] Saved → " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("[LocalData] Save failed: " + e.Message);
        }
    }

    // ── Count existing attempts for a player + game + level ───────────────
    // Returns how many saved files already exist so the next save uses
    // attempt N+1 and never overwrites a previous attempt.
    public static int CountExistingAttempts(string playerName, int game, int level)
    {
        int count = 0;
        string root = RootPath;
        if (!Directory.Exists(root)) return 0;

        string sanitised = SanitiseName(playerName);
        string prefix = (game == 1 ? "game1" : "game2") + "_L" + level + "_A";

        foreach (string folder in Directory.GetDirectories(root))
        {
            if (!Path.GetFileName(folder).StartsWith(sanitised)) continue;
            foreach (string file in Directory.GetFiles(folder, prefix + "*.json"))
                count++;
        }
        return count;
    }

    // ── Utility: delete all data for a player (therapist reset) ──────────

    public static void DeletePlayerData(string playerName)
    {
        string root = RootPath;
        if (!Directory.Exists(root)) return;

        string sanitised = SanitiseName(playerName);
        foreach (string folder in Directory.GetDirectories(root))
        {
            if (Path.GetFileName(folder).StartsWith(sanitised))
            {
                Directory.Delete(folder, recursive: true);
                Debug.Log("[LocalData] Deleted data for: " + playerName);
            }
        }
    }
}