using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;

    const string PROJECT_ID = "lumiplay-c871d";

    const string FIRESTORE_URL =
        "https://firestore.googleapis.com/v1/projects/" + PROJECT_ID +
        "/databases/(default)/documents";

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

    public void SendGame1Session(LocalDataManager.Game1SessionData data)
    {
        StartCoroutine(SendGame1Coroutine(data));
    }

    public void SendGame2Session(LocalDataManager.Game2SessionData data)
    {
        StartCoroutine(SendGame2Coroutine(data));
    }

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

    IEnumerator SendGame1Coroutine(LocalDataManager.Game1SessionData data)
    {
        string docName = $"game1_L{data.currentLevel}_A{data.attemptNumber}";
        string url = BuildDocumentUrl(data.playerName, docName);

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

        sb.Append($"\"confusionPairs\":{{{confusionSb}}}");
        sb.Append("}}");

        yield return EnsurePlayerDocument(data.playerName);
        yield return PatchDocument(url, sb.ToString(), "Game2 L" + data.currentLevel);
    }

    IEnumerator SendEmotionMirrorCoroutine(
        string playerName, int totalAttempts, int correct,
        string emResults, string improvements)
    {

        yield return EnsurePlayerDocument(playerName);

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

    public void RegisterPlayer(string playerName, string gender)
    {
        StartCoroutine(RegisterPlayerCoroutine(playerName, gender));
    }

    IEnumerator RegisterPlayerCoroutine(string playerName, string gender)
    {

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

            Debug.LogWarning($"[Firebase] ✗ Failed to save {label}: {req.error}\n{req.downloadHandler.text}");
        }
    }

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

    string BuildDocumentUrl(string playerName, string docName)
    {
        return $"{FIRESTORE_URL}/players/{Uri.EscapeDataString(SafeId(playerName))}" +
               $"/sessions/{Uri.EscapeDataString(docName)}";
    }

    string SafeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        string clean = name.Trim().ToLower().Replace(" ", "_");

        string reg = PlayerPrefs.GetString("RegisteredAt_" + name.Trim(), "");
        if (!string.IsNullOrEmpty(reg))
        {

            string suffix = reg.Replace("-", "");
            if (suffix.Length >= 8) suffix = suffix.Substring(0, 8);
            return clean + "_" + suffix;
        }
        return clean;
    }

    void AddStr(StringBuilder sb, string key, string val)
        => sb.Append($"\"{key}\":{{\"stringValue\":\"{Escape(val)}\"}}");

    void AddInt(StringBuilder sb, string key, int val)
        => sb.Append($"\"{key}\":{{\"integerValue\":\"{val}\"}}");

    void AddDbl(StringBuilder sb, string key, double val)
        => sb.Append($"\"{key}\":{{\"doubleValue\":{val.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}}}");

    void AddBool(StringBuilder sb, string key, bool val)
        => sb.Append($"\"{key}\":{{\"booleanValue\":{(val ? "true" : "false")}}}");

    string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }
}