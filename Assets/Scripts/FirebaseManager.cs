using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// FirebaseManager — sends session data to Firestore via REST API.
///
/// WHY REST instead of the Firebase Unity SDK?
/// The Firebase Unity SDK requires complex setup and has version conflicts
/// with many Unity projects. The REST API does the exact same thing with
/// zero dependencies — just HTTP requests Unity already supports.
///
/// HOW IT WORKS:
/// After every session save, LocalDataManager calls FirebaseManager.
/// FirebaseManager converts the data to JSON and sends it to Firestore.
/// The dashboard reads from Firestore automatically.
///
/// FIRESTORE STRUCTURE:
///   players/
///     {playerName}/           ← one document per child
///       sessions/             ← subcollection
///         game1_L0_A1/        ← one document per session attempt
///         game1_L1_A1/
///         game2_L0_A1/
///
/// SETUP:
/// 1. Attach this script to a persistent GameObject (e.g. GameManager)
///    OR call it as a static coroutine from LocalDataManager.
/// 2. Fill in your Firebase project ID below.
/// 3. Make sure your Firestore rules are set to allow read/write in test mode.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;

    // ── Your Firebase project ID ──────────────────────────────────────
    // Found in Firebase Console → Project Settings → General → Project ID
    const string PROJECT_ID = "lumiplay-c871d";

    // Firestore REST base URL
    const string FIRESTORE_URL =
        "https://firestore.googleapis.com/v1/projects/" + PROJECT_ID +
        "/databases/(default)/documents";

    // ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PUBLIC SEND METHODS
    // Called from LocalDataManager after every local save.
    // These are fire-and-forget — if Firebase fails, local save is fine.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this after SaveGame1Session() in LocalDataManager.
    /// </summary>
    public void SendGame1Session(LocalDataManager.Game1SessionData data)
    {
        StartCoroutine(SendGame1Coroutine(data));
    }

    /// <summary>
    /// Call this after SaveGame2Session() in LocalDataManager.
    /// </summary>
    public void SendGame2Session(LocalDataManager.Game2SessionData data)
    {
        StartCoroutine(SendGame2Coroutine(data));
    }

    /// <summary>
    /// Call this after saving Emotion Mirror results.
    /// emResults format: "Happy: correct (87%)|Sad: incorrect → Fear detected (45%)"
    /// </summary>
    public void SendEmotionMirrorResult(
        string playerName,
        int totalAttempts,
        int correct,
        string emResults,
        string improvements)
    {
        StartCoroutine(SendEmotionMirrorCoroutine(
            playerName, totalAttempts, correct, emResults, improvements));
    }

    // ════════════════════════════════════════════════════════════════════
    // COROUTINES — build Firestore document and POST it
    // ════════════════════════════════════════════════════════════════════

    IEnumerator SendGame1Coroutine(LocalDataManager.Game1SessionData data)
    {
        string docName = $"game1_L{data.currentLevel}_A{data.attemptNumber}";
        string url = BuildDocumentUrl(data.playerName, docName);

        // Build rounds as a Firestore array of maps
        var roundsSb = new StringBuilder();
        roundsSb.Append("\"arrayValue\":{\"values\":[");
        if (data.rounds != null && data.rounds.Count > 0)
        {
            for (int i = 0; i < data.rounds.Count; i++)
            {
                var r = data.rounds[i];
                roundsSb.Append("{\"mapValue\":{\"fields\":{");
                roundsSb.Append($"\"roundNumber\":{{\"integerValue\":\"{r.roundNumber}\"}},");
                roundsSb.Append($"\"fruitsShown\":{{\"integerValue\":\"{r.fruitsShown}\"}},");
                roundsSb.Append($"\"orderRequired\":{{\"booleanValue\":{(r.orderRequired ? "true" : "false")}}},");
                roundsSb.Append($"\"correctTaps\":{{\"integerValue\":\"{r.correctTaps}\"}},");
                roundsSb.Append($"\"wrongTaps\":{{\"integerValue\":\"{r.wrongTaps}\"}},");
                roundsSb.Append($"\"hintsUsed\":{{\"integerValue\":\"{r.hintsUsed}\"}},");
                roundsSb.Append($"\"hint1Used\":{{\"integerValue\":\"{r.hint1Used}\"}},");
                roundsSb.Append($"\"hint2Used\":{{\"integerValue\":\"{r.hint2Used}\"}},");
                roundsSb.Append($"\"passed\":{{\"booleanValue\":{(r.passed ? "true" : "false")}}},");
                roundsSb.Append($"\"responseTimeSec\":{{\"doubleValue\":{r.responseTimeSec.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}}}");
                roundsSb.Append("}}}");
                if (i < data.rounds.Count - 1) roundsSb.Append(",");
            }
        }
        roundsSb.Append("]}");

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", data.playerName); sb.Append(",");
        AddStr(sb, "sessionId", data.sessionId); sb.Append(",");
        AddStr(sb, "timestamp", data.timestamp); sb.Append(",");
        AddInt(sb, "currentLevel", data.currentLevel); sb.Append(",");
        AddInt(sb, "attemptNumber", data.attemptNumber); sb.Append(",");
        AddInt(sb, "totalCorrect", data.totalCorrect); sb.Append(",");
        AddInt(sb, "totalWrong", data.totalWrong); sb.Append(",");
        AddDbl(sb, "avgResponseTimeSec", data.avgResponseTimeSec); sb.Append(",");
        AddInt(sb, "hintsUsed", data.hintsUsed); sb.Append(",");
        AddInt(sb, "hint1Used", data.hint1Used); sb.Append(",");
        AddInt(sb, "hint2Used", data.hint2Used); sb.Append(",");
        AddBool(sb, "levelPassed", data.levelPassed); sb.Append(",");
        AddInt(sb, "starsEarned", data.starsEarned); sb.Append(",");
        AddDbl(sb, "finalDelay", data.finalDelay); sb.Append(",");
        AddStr(sb, "finalGrid", data.finalGrid ?? ""); sb.Append(",");
        AddInt(sb, "finalFruitsR0", data.finalFruitsR0); sb.Append(",");
        AddInt(sb, "finalFruitsR1", data.finalFruitsR1); sb.Append(",");
        AddStr(sb, "difficultyMode", data.difficultyMode ?? "adaptive"); sb.Append(",");
        AddStr(sb, "adaptiveChanges", data.adaptiveChanges ?? ""); sb.Append(",");
        sb.Append($"\"rounds\":{{{roundsSb}}}");
        sb.Append("}}");

        yield return EnsurePlayerDocument(data.playerName);
        yield return PatchDocument(url, sb.ToString(), "Game1 L" + data.currentLevel);
    }

    IEnumerator SendGame2Coroutine(LocalDataManager.Game2SessionData data)
    {
        string docName = $"game2_L{data.currentLevel}_A{data.attemptNumber}";
        string url = BuildDocumentUrl(data.playerName, docName);

        // Build confusion pairs as a Firestore array
        var confusionSb = new StringBuilder();
        confusionSb.Append("\"arrayValue\":{\"values\":[");
        if (data.confusionPairs != null && data.confusionPairs.Count > 0)
        {
            for (int i = 0; i < data.confusionPairs.Count; i++)
            {
                var p = data.confusionPairs[i];
                confusionSb.Append("{\"mapValue\":{\"fields\":{");
                confusionSb.Append($"\"correctEmotion\":{{\"stringValue\":\"{Escape(p.correctEmotion)}\"}},");
                confusionSb.Append($"\"selectedEmotion\":{{\"stringValue\":\"{Escape(p.selectedEmotion)}\"}},");
                confusionSb.Append($"\"count\":{{\"integerValue\":\"{p.count}\"}}");
                confusionSb.Append("}}}");
                if (i < data.confusionPairs.Count - 1) confusionSb.Append(",");
            }
        }
        confusionSb.Append("]}");

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", data.playerName); sb.Append(",");
        AddStr(sb, "sessionId", data.sessionId); sb.Append(",");
        AddStr(sb, "timestamp", data.timestamp); sb.Append(",");
        AddInt(sb, "currentLevel", data.currentLevel); sb.Append(",");
        AddInt(sb, "attemptNumber", data.attemptNumber); sb.Append(",");
        AddStr(sb, "emotionTested", data.emotionTested ?? ""); sb.Append(",");
        AddInt(sb, "correctAnswers", data.correctAnswers); sb.Append(",");
        AddInt(sb, "wrongAnswers", data.wrongAnswers); sb.Append(",");
        AddDbl(sb, "avgResponseTimeSec", data.avgResponseTimeSec); sb.Append(",");
        AddInt(sb, "hintsUsed", data.hintsUsed); sb.Append(",");
        AddInt(sb, "hint1Used", data.hint1Used); sb.Append(",");
        AddInt(sb, "hint2Used", data.hint2Used); sb.Append(",");
        AddBool(sb, "levelPassed", data.levelPassed); sb.Append(",");
        AddInt(sb, "starsEarned", data.starsEarned); sb.Append(",");
        // Confusion pairs array
        sb.Append($"\"confusionPairs\":{{{confusionSb}}}");
        sb.Append("}}");

        yield return EnsurePlayerDocument(data.playerName);
        yield return PatchDocument(url, sb.ToString(), "Game2 L" + data.currentLevel);
    }

    IEnumerator SendEmotionMirrorCoroutine(
        string playerName, int totalAttempts, int correct,
        string emResults, string improvements)
    {
        // Ensure player document exists first — critical for new accounts
        // that have not played Game 1 or 2 yet
        yield return EnsurePlayerDocument(playerName);

        // updateMask: only update EM fields, never touch gender or other fields
        string url = $"{FIRESTORE_URL}/players/{Uri.EscapeDataString(SafeId(playerName))}"
                   + "?updateMask.fieldPaths=emTotalAttempts&updateMask.fieldPaths=emCorrect"
                   + "&updateMask.fieldPaths=emResults&updateMask.fieldPaths=emImprovements";

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", playerName);
        sb.Append(",");
        AddInt(sb, "emTotalAttempts", totalAttempts);
        sb.Append(",");
        AddInt(sb, "emCorrect", correct);
        sb.Append(",");
        AddStr(sb, "emResults", emResults ?? "");
        sb.Append(",");
        AddStr(sb, "emImprovements", improvements ?? "");
        sb.Append("}}");

        yield return PatchDocument(url, sb.ToString(), "EmotionMirror");
    }

    // ════════════════════════════════════════════════════════════════════
    // ALSO: Register player name in the players collection
    // This lets the dashboard know which children exist.
    // Call this once when a new child is created in LaunchScene.
    // ════════════════════════════════════════════════════════════════════

    public void RegisterPlayer(string playerName, string gender)
    {
        StartCoroutine(RegisterPlayerCoroutine(playerName, gender));
    }

    IEnumerator RegisterPlayerCoroutine(string playerName, string gender)
    {
        // updateMask ensures we only write these fields — never wipe existing ones
        string url = $"{FIRESTORE_URL}/players/{Uri.EscapeDataString(SafeId(playerName))}"
                   + "?updateMask.fieldPaths=playerName&updateMask.fieldPaths=gender&updateMask.fieldPaths=registeredAt";

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", playerName);
        sb.Append(",");
        AddStr(sb, "gender", gender);
        sb.Append(",");
        AddStr(sb, "registeredAt",
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append("}}");

        yield return PatchDocument(url, sb.ToString(), "RegisterPlayer");
    }

    // ════════════════════════════════════════════════════════════════════
    // HTTP PATCH — creates or updates a Firestore document
    // PATCH is used because it creates the document if it doesn't exist
    // and updates it if it does — exactly what we want.
    // ════════════════════════════════════════════════════════════════════

    IEnumerator PatchDocument(string url, string jsonBody, string label)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        using var req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[Firebase] ✓ Saved {label} to Firestore");
        }
        else
        {
            // Non-fatal — local save already succeeded
            Debug.LogWarning($"[Firebase] ✗ Failed to save {label}: {req.error}\n{req.downloadHandler.text}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // EMOTION MIRROR SESSION
    // ════════════════════════════════════════════════════════════════════

    public void SendEmotionMirrorSession(
        string playerName, string sessionId,
        string targetEmotion, string detectedEmotion,
        bool matched, float confidence)
    {
        StartCoroutine(SendEmotionMirrorSessionCoroutine(
            playerName, sessionId, targetEmotion, detectedEmotion, matched, confidence));
    }

    IEnumerator SendEmotionMirrorSessionCoroutine(
        string playerName, string sessionId,
        string targetEmotion, string detectedEmotion,
        bool matched, float confidence)
    {
        string docName = "em_" + sessionId;
        string url = BuildDocumentUrl(playerName, docName);

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", playerName); sb.Append(",");
        AddStr(sb, "sessionId", sessionId); sb.Append(",");
        AddStr(sb, "timestamp", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); sb.Append(",");
        AddStr(sb, "type", "emotionMirror"); sb.Append(",");
        AddStr(sb, "targetEmotion", targetEmotion); sb.Append(",");
        AddStr(sb, "detectedEmotion", detectedEmotion ?? "none"); sb.Append(",");
        AddBool(sb, "matched", matched); sb.Append(",");
        AddDbl(sb, "confidence", confidence);
        sb.Append("}}");

        yield return EnsurePlayerDocument(playerName);
        yield return PatchDocument(url, sb.ToString(), "EmotionMirror session");
    }

    // ════════════════════════════════════════════════════════════════════
    // ENSURE PLAYER DOCUMENT EXISTS
    // Guarantees the parent player document has fields so queries find it.
    // Called after every session save as a safety guarantee.
    // ════════════════════════════════════════════════════════════════════

    IEnumerator EnsurePlayerDocument(string playerName)
    {
        string url = $"{FIRESTORE_URL}/players/{Uri.EscapeDataString(SafeId(playerName))}";

        var sb = new StringBuilder();
        sb.Append("{\"fields\":{");
        AddStr(sb, "playerName", playerName); sb.Append(",");
        AddStr(sb, "registeredAt", System.DateTime.Now.ToString("yyyy-MM-dd"));
        sb.Append("}}");

        yield return PatchDocument(url, sb.ToString(), "EnsurePlayer");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS — build Firestore field value JSON
    // ════════════════════════════════════════════════════════════════════

    string BuildDocumentUrl(string playerName, string docName)
    {
        return $"{FIRESTORE_URL}/players/{Uri.EscapeDataString(SafeId(playerName))}" +
               $"/sessions/{Uri.EscapeDataString(docName)}";
    }

    // SafeId — converts display name to a consistent document ID.
    // "Ahmed", "ahmed", "AHMED" → "ahmed" (same document every time).
    // The original typed name is always stored as a field for display.
    string SafeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        return name.Trim().ToLower().Replace(" ", "_");
    }

    // Firestore requires typed field values
    void AddStr(StringBuilder sb, string key, string val)
        => sb.Append($"\"{key}\":{{\"stringValue\":\"{Escape(val)}\"}}");

    void AddInt(StringBuilder sb, string key, int val)
        => sb.Append($"\"{key}\":{{\"integerValue\":\"{val}\"}}");

    void AddDbl(StringBuilder sb, string key, double val)
        => sb.Append($"\"{key}\":{{\"doubleValue\":{val.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");

    void AddBool(StringBuilder sb, string key, bool val)
        => sb.Append($"\"{key}\":{{\"booleanValue\":{(val ? "true" : "false")}}}");

    // Escape special characters in JSON strings
    string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }
}