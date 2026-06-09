using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

/// <summary>
/// LLMService — Singleton wrapping Groq, Gemini, and ElevenLabs TTS.
///
/// ── LLM APIs ─────────────────────────────────────────────────────────────
/// Groq   → real-time hints + motivational messages  (llama-3.3-70b-versatile)
/// Gemini → therapist report                         (gemini-1.5-flash)
///
/// ── TTS ──────────────────────────────────────────────────────────────────
/// ElevenLabs TTS → speaks every Lumi message aloud
/// Voice:  one consistent child-friendly voice for Lumi across all scenes
/// Format: pcm_16000 (raw PCM, 16kHz, mono) — Unity AudioClip compatible
/// Cache:  audio is cached on disk (TTSCache) so fixed phrases are only
///         synthesised once and never re-requested even across app restarts
///
/// ── How TTS integrates with gameplay ─────────────────────────────────────
/// Every ShowLumi() call in both managers calls LLMService.Instance.Speak().
/// Text appears immediately in the speech bubble.
/// Audio plays when it arrives (usually < 1 second for short phrases).
/// If TTS fails or voice is muted, the game continues silently — never blocked.
///
/// ── Voice mute ───────────────────────────────────────────────────────────
/// Respects PlayerPrefs "VoiceMuted" (set in HubManager settings).
/// TTS is not requested at all when muted — saves API credits.
///
/// ── Setup ────────────────────────────────────────────────────────────────
/// 1. Add this component to your DontDestroyOnLoad GameObject
/// 2. Set groqApiKey, geminiApiKey, elevenLabsApiKey in the Inspector
/// 3. Set lumiVoiceId to the ElevenLabs voice ID you want for Lumi
/// 4. Assign lumiAudioSource — the AudioSource Lumi's voice plays through
/// </summary>
public class LLMService : MonoBehaviour
{
    public static LLMService Instance { get; private set; }

    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

    [Header("LLM API Keys")]
    [Tooltip("Groq API key — real-time hints and motivation (free at console.groq.com)")]
    public string groqApiKey = "YOUR_GROQ_API_KEY";
    [Tooltip("Gemini API key — therapist report (free at aistudio.google.com)")]
    public string geminiApiKey = "YOUR_GEMINI_API_KEY";

    [Header("TTS — ElevenLabs")]
    [Tooltip("ElevenLabs API key (elevenlabs.io → Profile → API Key)")]
    public string elevenLabsApiKey = "YOUR_ELEVENLABS_API_KEY";

    [Tooltip("ElevenLabs Voice ID for Lumi. Recommended: 'EXAVITQu4vr4xnSDxMaL' (Sarah) " +
             "— warm, clear, child-friendly. Find others at elevenlabs.io/voice-library")]
    public string lumiVoiceId = "EXAVITQu4vr4xnSDxMaL";

    [Tooltip("AudioSource that Lumi's voice plays through. " +
             "Assign the lumiAudio AudioSource from whichever scene is active, " +
             "OR leave blank and SpeakOnSource() will be used per-manager.")]
    public AudioSource lumiAudioSource;

    [Header("TTS Settings")]
    [Tooltip("ElevenLabs model. eleven_turbo_v2 is recommended for free tier.")]
    public string ttsModel = "eleven_turbo_v2";
    [Range(0f, 1f)]
    [Tooltip("Voice stability. 0.75 = calm and consistent. Do not go above 0.85.")]
    public float voiceStability = 0.75f;
    [Range(0f, 1f)]
    [Tooltip("Similarity boost. 0.75 = close to original voice.")]
    public float similarityBoost = 0.75f;
    [Range(0.7f, 1.2f)]
    [Tooltip("Speaking speed. 0.75 = slow and calm, 1.0 = normal. " +
             "Delete TTSCache folder after changing so phrases re-synthesise.")]
    public float speakingSpeed = 0.75f;

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE — API ENDPOINTS & MODELS
    // ════════════════════════════════════════════════════════════════════════

    private const string kGroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string kGroqModel = "llama-3.3-70b-versatile";
    private const string kGeminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
    private const string kElevenLabsBase = "https://api.elevenlabs.io/v1/text-to-speech/";

    private const int kHintTokens = 80;
    private const int kMotivationTokens = 60;
    private const int kReportTokens = 1500;

    // ════════════════════════════════════════════════════════════════════════
    // FALLBACKS  (used when APIs are unavailable)
    // ════════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, string> kFallbackSituational =
        new Dictionary<string, string>
        {
            ["Happy"] = "Think about how you feel on your birthday!",
            ["Sad"] = "Think about how you feel when you miss someone you love.",
            ["Fear"] = "Think about how you feel when you hear a loud scary noise!",
            ["Angry"] = "Think about how you feel when someone takes your favourite toy!",
            ["Surprised"] = "Think about how you feel when someone jumps out and says boo!",
            ["Disgusted"] = "Think about how you feel when you smell something really bad!",
        };

    private static readonly Dictionary<string, string> kFallbackFacial =
        new Dictionary<string, string>
        {
            ["Happy"] = "Have you ever seen someone jump up and down with joy?",
            ["Sad"] = "Have you ever seen someone cry and wipe away their tears?",
            ["Fear"] = "Have you ever seen someone freeze and hold their breath from fright?",
            ["Angry"] = "Have you ever seen someone stomp their feet and clench their fists?",
            ["Surprised"] = "Have you ever seen someone gasp and cover their mouth in shock?",
            ["Disgusted"] = "Have you ever seen someone back away and say yuck?",
        };

    private static readonly string[] kFallbackMotivation =
    {
        "You are doing great! Keep trying!",
        "Every try makes you smarter!",
        "I believe in you — you can do it!",
        "Almost there! Do not give up!",
        "You are getting better every round!",
    };

    // ════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC — LLM API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Request a targeted hint for Game 2. No fallback — LLM only.</summary>
    public void GetHint(HintContext ctx, System.Action<string> callback)
    {
        StartCoroutine(CallGroq(BuildHintPrompt(ctx), kHintTokens, result =>
        {
            Debug.Log("[LLM] Hint " + (string.IsNullOrEmpty(result) ? "EMPTY" : "LLM") +
                      " (" + ctx.correctEmotion + "): " + result);
            if (!string.IsNullOrEmpty(result)) callback(result);
            // If empty, callback is not called — ShowLumi never fires, game moves on silently
        }));
    }

    /// <summary>Request a motivational message. No fallback — LLM only.</summary>
    public void GetMotivation(MotivationContext ctx, System.Action<string> callback)
    {
        StartCoroutine(CallGroq(BuildMotivationPrompt(ctx), kMotivationTokens, result =>
        {
            if (!string.IsNullOrEmpty(result)) callback(result);
            // If empty, callback is not called — no static fallback shown
        }));
    }

    /// <summary>Generate a full therapist report using Gemini.</summary>
    public void GetTherapistReport(ReportContext ctx, System.Action<string> callback)
    {
        StartCoroutine(CallGemini(BuildReportPrompt(ctx), kReportTokens, result =>
            callback(string.IsNullOrEmpty(result) ? BuildFallbackReport(ctx) : result)));
    }

    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC — TTS API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Speaks text through the default lumiAudioSource assigned in the Inspector.
    /// Call this from ShowLumi() in any scene manager.
    ///
    /// Text appears immediately in the UI — audio plays when it arrives.
    /// Cached phrases play instantly on repeat calls.
    /// Silently skipped if voice is muted or API key not set.
    /// </summary>
    public void Speak(string text)
    {
        if (lumiAudioSource == null)
        {
            Debug.LogWarning("[TTS] lumiAudioSource not assigned on LLMService.");
            return;
        }
        SpeakOnSource(text, lumiAudioSource);
    }

    /// <summary>
    /// Speaks text through a specific AudioSource.
    /// Use this when each manager controls its own AudioSource
    /// (e.g. RoundManagerMG1 passes its own lumiAudio).
    ///
    /// This is the recommended pattern — each scene passes its own AudioSource
    /// so TTS audio always plays through the correct mixer channel.
    /// </summary>
    public void SpeakOnSource(string text, AudioSource source)
    {
        if (source == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (PlayerPrefs.GetInt("VoiceMuted", 0) == 1) return;

        // Arabic — use local Edge-TTS server on port 5051
        if (GameManager.IsArabic())
        {
            StartCoroutine(FetchAndPlayArabic(text, source));
            return;
        }

        // English — use ElevenLabs
        if (TTSCache.TryGet(text, lumiVoiceId, out AudioClip cached))
        {
            PlayClip(source, cached);
            return;
        }
        StartCoroutine(FetchAndPlay(text, source));
    }

    /// <summary>
    /// Stops whatever Lumi is currently saying.
    /// Call before showing a new message to prevent overlap.
    /// </summary>
    public void StopSpeaking(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

    /// <summary>
    /// Pre-warms the TTS cache for a list of fixed phrases.
    /// Call this during a loading screen so common phrases are ready instantly.
    /// </summary>
    public void PrewarmCache(string[] phrases, AudioSource source)
    {
        StartCoroutine(PrewarmSequential(phrases));
    }

    public void PrimeCache(string phrase, System.Action onDone)
    {
        if (TTSCache.TryGet(phrase, lumiVoiceId, out _))
        { onDone?.Invoke(); return; }
        StartCoroutine(PrimeCacheCoroutine(phrase, onDone));
    }

    IEnumerator PrimeCacheCoroutine(string phrase, System.Action onDone)
    {
        yield return StartCoroutine(FetchAndCache(phrase));
        onDone?.Invoke();
    }

    IEnumerator PrewarmSequential(string[] phrases)
    {
        foreach (string phrase in phrases)
        {
            // Skip if already cached — no request needed
            if (TTSCache.TryGet(phrase, lumiVoiceId, out _)) continue;

            // Fetch and cache this phrase
            yield return StartCoroutine(FetchAndCache(phrase));

            // Wait 0.4s between requests to avoid rate limiting
            yield return new WaitForSeconds(0.4f);
        }
    }
    // ════════════════════════════════════════════════════════════════════════
    // INTERNAL — TTS FETCH
    // ════════════════════════════════════════════════════════════════════════

    // ════════════════════════════════════════════════════════════════════════
    // ARABIC TTS — Edge-TTS via local Python server on port 5051
    // ════════════════════════════════════════════════════════════════════════

    private const string kArabicTTSUrl = "http://localhost:5051/tts";

    // Arabic voice — female Egyptian: ar-EG-SalmaNeural
    //                male Egyptian:   ar-EG-ShakirNeural
    // Matches Lumi gender set in Inspector
    private string ArabicVoice()
    {
        bool isMale = lumiVoiceId != null && lumiVoiceId.ToLower().Contains("male");
        return isMale ? "ar-EG-ShakirNeural" : "ar-EG-SalmaNeural";
    }

    IEnumerator FetchAndPlayArabic(string text, AudioSource source)
    {
        string cacheKey = "AR_" + ArabicVoice();
        if (TTSCache.TryGet(text, cacheKey, out AudioClip cached))
        {
            PlayClip(source, cached);
            yield break;
        }

        string body = "{\"text\":" + JsonEscape(text) +
                      ",\"voice\":\"" + ArabicVoice() + "\"}";

        using var req = new UnityWebRequest(kArabicTTSUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 12;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[ArabicTTS] Server error: " + req.error);
            yield break;
        }

        byte[] audioBytes = req.downloadHandler.data;
        if (audioBytes == null || audioBytes.Length < 100)
        {
            Debug.LogWarning("[ArabicTTS] Empty or too-small response.");
            yield break;
        }

        // Detect format from response header or magic bytes
        string contentType = req.GetResponseHeader("Content-Type") ?? "";
        bool isWav = contentType.Contains("wav") ||
                     (audioBytes.Length > 4 &&
                      audioBytes[0] == 'R' && audioBytes[1] == 'I' &&
                      audioBytes[2] == 'F' && audioBytes[3] == 'F');
        AudioType audioType = isWav ? AudioType.WAV : AudioType.MPEG;

        string ext = isWav ? ".wav" : ".mp3";
        string tempPath = System.IO.Path.Combine(
            Application.temporaryCachePath,
            "ar_tts_" + System.Guid.NewGuid().ToString("N").Substring(0, 8) + ext);

        System.IO.File.WriteAllBytes(tempPath, audioBytes);

        string fileUri = "file:///" + tempPath.Replace("\\", "/");
        using var audioReq = UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType);
        if (audioType == AudioType.MPEG)
            ((DownloadHandlerAudioClip)audioReq.downloadHandler).streamAudio = false;

        yield return audioReq.SendWebRequest();

        AudioClip clip = null;
        if (audioReq.result == UnityWebRequest.Result.Success)
            clip = DownloadHandlerAudioClip.GetContent(audioReq);
        else
            Debug.LogWarning("[ArabicTTS] Failed to decode audio: " + audioReq.error);

        try { System.IO.File.Delete(tempPath); } catch { }

        if (clip != null && clip.length > 0.05f)
        {
            TTSCache.Store(text, cacheKey, audioBytes);
            PlayClip(source, clip);
            Debug.Log("[ArabicTTS] ✓ " + Truncate(text, 40));
        }
        else
        {
            Debug.LogWarning("[ArabicTTS] Clip was null or too short.");
        }
    }

    /// <summary>Fetches audio from ElevenLabs, caches it, and plays it.</summary>
    IEnumerator FetchAndPlay(string text, AudioSource source)
    {
        AudioClip clip = null;
        yield return StartCoroutine(FetchTTS(text, result => clip = result));

        if (clip != null && source != null)
            PlayClip(source, clip);
    }

    /// <summary>Fetches audio and caches it without playing (for prewarm).</summary>
    IEnumerator FetchAndCache(string text)
    {
        yield return StartCoroutine(FetchTTS(text, _ => { }));
    }

    /// <summary>
    /// Core TTS request to ElevenLabs.
    /// Returns raw PCM bytes → stores in TTSCache → converts to AudioClip.
    /// Uses pcm_16000 output format: raw 16-bit PCM at 16kHz, mono.
    /// Unity can load this directly without any codec — no MP3/OGG decoder needed.
    /// </summary>
    IEnumerator FetchTTS(string text, System.Action<AudioClip> callback)
    {
        if (string.IsNullOrEmpty(elevenLabsApiKey) || elevenLabsApiKey == "YOUR_ELEVENLABS_API_KEY")
            elevenLabsApiKey = "sk_7a61fc97e57f606c9e4ab71a50ee4b2a616ca676d9105a31";

        if (string.IsNullOrEmpty(elevenLabsApiKey))
        {
            Debug.LogWarning("[TTS] ElevenLabs API key not set.");
            callback(null);
            yield break;
        }

        string url = kElevenLabsBase + lumiVoiceId
                   + "?output_format=pcm_16000";

        // Build request JSON
        string body = "{" +
            "\"text\": " + JsonEscape(text) + "," +
            "\"model_id\": \"" + ttsModel + "\"," +
            "\"voice_settings\": {" +
                "\"stability\": " + voiceStability.ToString("F2") + "," +
                "\"similarity_boost\": " + similarityBoost.ToString("F2") + "," +
                "\"speed\": " + speakingSpeed.ToString("F2") +
            "}" +
        "}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("xi-api-key", elevenLabsApiKey);
        req.SetRequestHeader("Accept", "audio/pcm");
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[TTS] ElevenLabs error: " + req.error +
                             " | Code: " + req.responseCode);
            callback(null);
            yield break;
        }

        byte[] pcmBytes = req.downloadHandler.data;
        if (pcmBytes == null || pcmBytes.Length == 0)
        {
            Debug.LogWarning("[TTS] Empty response from ElevenLabs.");
            callback(null);
            yield break;
        }

        AudioClip clip = TTSCache.Store(text, lumiVoiceId, pcmBytes);

        Debug.Log("[TTS] Synthesised '" + Truncate(text, 40) + "'" +
                  " (" + pcmBytes.Length / 1024 + "KB, cached)");

        callback(clip);
    }

    void PlayClip(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.Stop();
        source.clip = clip;
        source.volume = 1f;
        source.Play();
    }


    // ════════════════════════════════════════════════════════════════════════
    // CONTEXT CLASSES
    // ════════════════════════════════════════════════════════════════════════

    public class HintContext
    {
        public string correctEmotion;
        public string selectedEmotion;
        public int attemptNumber;
        public float responseTimeSec;
        public List<string> wrongHistory;
        public string confusionPattern;
        /// <summary>
        /// "situational" = relatable scenario hint (attempt 1)
        /// "facial"      = facial feature hint (attempt 2)
        /// </summary>
        public string hintType = "situational";
    }

    public class MotivationContext
    {
        public int game;
        public int currentLevel;
        public int recentWrongCount;
        public int recentCorrectCount;
        public bool justSucceededAfterStruggle;
        public string situation;
    }

    public class ReportContext
    {
        public string playerName;
        public string sessionDate;
        public int totalSessions;

        // ── Game 1 ────────────────────────────────────────────────────────
        public int g1TotalCorrect;
        public int g1TotalWrong;
        public float g1AvgResponseTimeSec;
        public float g1FirstSessionRT;
        public float g1LastSessionRT;
        public float g1FirstSessionAcc;
        public float g1LastSessionAcc;
        public int g1HintsUsed;
        public int g1Hint1Used;
        public int g1Hint2Used;
        public float g1HintDependencyRatio;
        public int g1LevelsCompleted;
        public int g1LevelsFailed;
        public int g1TimesHarderFired;
        public int g1TimesEasierFired;
        public bool g1DifficultyIncreased;
        public int g1MaxFruitsReached;
        public float g1MaxDelayReached;
        public string g1AdaptiveChanges;
        // Grid + stimuli breakdown: "2x2 1fruit: 90% (10 rounds)"
        public List<string> g1GridPerformance;
        // Level repetition: "Level 2: 3 attempts — 60% → 75% → 85% (improving)"
        public List<string> g1LevelRepetitions;

        // ── Game 2 ────────────────────────────────────────────────────────
        public int g2TotalCorrect;
        public int g2TotalWrong;
        public float g2AvgResponseTimeSec;
        public float g2FirstSessionAcc;
        public float g2LastSessionAcc;
        public int g2HintsUsed;
        public int g2Hint1Used;
        public int g2Hint2Used;
        public float g2HintDependencyRatio;
        public int g2LevelsCompleted;
        public int g2LevelsFailed;
        public List<string> g2PerEmotionAccuracy;
        public List<string> g2ConfusionPairs;
        public List<string> g2StrongEmotions;
        public List<string> g2WeakEmotions;
        // Level repetition for Game 2
        public List<string> g2LevelRepetitions;

        // ── Emotion Mirror ────────────────────────────────────────────────
        public bool emotionMirrorPlayed;
        public int emotionMirrorTotalAttempts;
        public int emotionMirrorCorrect;
        public int emotionMirrorIncorrect;
        // "Happy: correct (87%)", "Angry: incorrect → Sad detected (62%)"
        public List<string> emotionMirrorResults;
        // Emotions where retry improved the result
        public List<string> emotionMirrorImprovements;
    }

    // ════════════════════════════════════════════════════════════════════════
    // PROMPT BUILDERS
    // ════════════════════════════════════════════════════════════════════════

    // Maps English emotion names to Arabic when in Arabic mode
    // so Groq doesn't mix languages in responses
    private static readonly System.Collections.Generic.Dictionary<string, string>
        kEmotionArabicNames = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Happy",     "سَعِيد"      },
            { "Sad",       "حَزِين"      },
            { "Fear",      "خَائِف"      },
            { "Angry",     "غَاضِب"      },
            { "Surprised", "مُتَفَاجِئ"  },
            { "Disgusted", "مُشْمَئِزّ"  },
            { "Neutral",   "مُحَايِد"    },
        };

    string ToArabicEmotionIfNeeded(string emotion)
    {
        if (!GameManager.IsArabic()) return emotion;
        return kEmotionArabicNames.TryGetValue(emotion, out string ar) ? ar : emotion;
    }

    string BuildHintPrompt(HintContext ctx)
    {
        return ctx.hintType == "facial"
            ? BuildSocialObservationHintPrompt(ctx)
            : BuildSituationalHintPrompt(ctx);
    }

    string BuildSituationalHintPrompt(HintContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Lumi, a warm friendly helper in a children's emotion recognition game for ages 4-8.");
        sb.AppendLine("A child got the wrong answer. Give them a situational hint.");
        sb.AppendLine();
        sb.AppendLine("Correct emotion: " + ToArabicEmotionIfNeeded(ctx.correctEmotion));
        if (!string.IsNullOrEmpty(ctx.selectedEmotion))
            sb.AppendLine("Child guessed: " + ToArabicEmotionIfNeeded(ctx.selectedEmotion) + " (wrong)");
        sb.AppendLine();
        sb.AppendLine("Write EXACTLY ONE sentence using this structure:");
        sb.AppendLine("  'If [simple observable situation], how do you think they feel?'");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Always start with 'If'");
        sb.AppendLine("- Always end with 'how do you think they feel?'");
        sb.AppendLine("- The situation must be a simple visible behaviour a 4-8 year old would recognise");
        sb.AppendLine("- Do NOT name the emotion");
        sb.AppendLine("- Do NOT mention facial features or anatomy");
        sb.AppendLine("- Max 14 words total");
        sb.AppendLine("- Will be spoken aloud — no symbols, no abbreviations");
        sb.AppendLine();
        sb.AppendLine("GOOD EXAMPLES:");
        sb.AppendLine("- Happy:     If someone is laughing and clapping their hands, how do you think they feel?");
        sb.AppendLine("- Sad:       If someone is crying and wiping their tears, how do you think they feel?");
        sb.AppendLine("- Fear:      If someone is shaking and hiding behind a door, how do you think they feel?");
        sb.AppendLine("- Angry:     If someone is stomping their feet and shouting, how do you think they feel?");
        sb.AppendLine("- Surprised: If someone just got an unexpected gift and gasped, how do you think they feel?");
        sb.AppendLine("- Disgusted: If someone smelled something bad and backed away, how do you think they feel?");
        sb.AppendLine();
        sb.AppendLine("The situation must clearly point to ONLY the correct emotion — not the wrong one the child chose.");
        sb.AppendLine("Reply with ONLY the one sentence. No preamble.");

        if (GameManager.IsArabic())
        {
            sb.AppendLine();
            sb.AppendLine("CRITICAL: You MUST reply ENTIRELY in Arabic. Do NOT use any English words. Simple Egyptian Arabic. Include full tashkeel (diacritics). Max 14 Arabic words. No English.");
            sb.AppendLine();
            sb.AppendLine("ARABIC EXAMPLES (use these as a guide for the correct emotion only):");
            sb.AppendLine("- سَعِيد:     لَوْ كَانَ شَخْصٌ يَضْحَكُ وَيُصَفِّقُ، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- حَزِين:     لَوْ كَانَ شَخْصٌ يَبْكِي وَيَمْسَحُ دُمُوعَهُ، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- خَائِف:     لَوْ كَانَ شَخْصٌ يَرْتَجِفُ وَيَخْتَبِئُ، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- غَاضِب:     لَوْ كَانَ شَخْصٌ يَدُقُّ قَدَمَهُ وَيَصْرُخُ، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- مُتَفَاجِئ: لَوْ كَانَ شَخْصٌ قَدْ حَصَلَ عَلَى هَدِيَّةٍ مُفَاجِئَة، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine();
            sb.AppendLine("Write a NEW sentence for the EXACT emotion above. Start with لَوْ and end with كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
        }
        return sb.ToString();
    }

    string BuildSocialObservationHintPrompt(HintContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are Lumi, a warm friendly helper in a children's emotion recognition game for ages 4-8.");
        sb.AppendLine("A child got the wrong answer twice. Give them a FACIAL FEATURE hint.");
        sb.AppendLine("This second hint must direct the child to look at specific visible features on the face in the image.");
        sb.AppendLine();
        sb.AppendLine("Correct emotion: " + ToArabicEmotionIfNeeded(ctx.correctEmotion));
        if (!string.IsNullOrEmpty(ctx.selectedEmotion))
            sb.AppendLine("Child guessed: " + ToArabicEmotionIfNeeded(ctx.selectedEmotion) + " (wrong)");
        sb.AppendLine();
        sb.AppendLine("Write EXACTLY ONE sentence using this structure:");
        sb.AppendLine("  'If someone has [specific visible facial feature or expression], how do you think they feel?'");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Always start with 'If someone has' or 'If someone is'");
        sb.AppendLine("- Always end with 'how do you think they feel?'");
        sb.AppendLine("- Describe EXACTLY what the face looks like — eyebrows, eyes, mouth, or cheeks");
        sb.AppendLine("- Use simple body language a child understands — no clinical terms");
        sb.AppendLine("- Do NOT name the emotion");
        sb.AppendLine("- Max 14 words total");
        sb.AppendLine("- Will be spoken aloud — no symbols, no abbreviations");
        sb.AppendLine();
        sb.AppendLine("GOOD EXAMPLES:");
        sb.AppendLine("- Happy:     If someone has a big wide smile and their cheeks are up, how do you think they feel?");
        sb.AppendLine("- Sad:       If someone has their eyebrows going up in the middle and their mouth turned down, how do you think they feel?");
        sb.AppendLine("- Fear:      If someone has very wide open eyes and their mouth open, how do you think they feel?");
        sb.AppendLine("- Angry:     If someone has their eyebrows pushed down and their teeth clenched, how do you think they feel?");
        sb.AppendLine("- Surprised: If someone has their eyes very wide and their mouth open in a big O, how do you think they feel?");
        sb.AppendLine("- Disgusted: If someone has their nose wrinkled up and their lips curled, how do you think they feel?");
        sb.AppendLine();
        sb.AppendLine("The facial description must clearly match ONLY the correct emotion and NOT the wrong emotion the child chose.");
        sb.AppendLine("Reply with ONLY the one sentence. No preamble.");

        if (GameManager.IsArabic())
        {
            sb.AppendLine();
            sb.AppendLine("CRITICAL: You MUST reply ENTIRELY in Arabic. Do NOT use any English words. Simple Egyptian Arabic. Include full tashkeel (diacritics). Max 14 Arabic words. No English.");
            sb.AppendLine();
            sb.AppendLine("ARABIC EXAMPLES (use these as a guide for the correct emotion only):");
            sb.AppendLine("- سَعِيد:     لَوْ كَانَ لِشَخْصٍ ابْتِسَامَةٌ كَبِيرَةٌ وَخُدُودُهُ مُرْتَفِعَة، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- حَزِين:     لَوْ كَانَ شَخْصٌ يُنَكِّسُ رَأْسَهُ وَيَبْدُو فَمُهُ مَقْلُوبًا، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- خَائِف:     لَوْ كَانَتْ عَيْنَا شَخْصٍ مَفْتُوحَتَيْنِ جِدًّا وَفَمُهُ مَفْتُوح، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- غَاضِب:     لَوْ كَانَتْ حَوَاجِبُ شَخْصٍ مُنْخَفِضَةً وَأَسْنَانُهُ مَضْمُومَة، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine("- مُتَفَاجِئ: لَوْ كَانَتْ عَيْنَا شَخْصٍ وَاسِعَتَيْنِ وَفَمُهُ مَفْتُوحٌ عَلَى شَكْلِ حَرْفِ O، كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
            sb.AppendLine();
            sb.AppendLine("Write a NEW sentence for the EXACT emotion above. Start with لَوْ and end with كَيْفَ تَظُنُّ أَنَّهُ يَشْعُر؟");
        }
        return sb.ToString();
    }

    string BuildMotivationPrompt(MotivationContext ctx)
    {
        string[] openers = {
            "Start with 'Amazing'",  "Start with 'Wow'",
            "Start with 'You'",      "Start with 'Keep'",
            "Start with 'Every'",    "Start with the child's effort",
            "Start with a feeling word",
        };
        string opener = openers[Random.Range(0, openers.Length)];

        string gameName = ctx.game == 1
            ? "Memory Garden (remembering fruit sequences)"
            : "Emotion Quest (recognising facial emotions)";

        var sb = new StringBuilder();
        sb.AppendLine("You are Lumi, a warm encouraging character speaking to a child aged 4-8 with ASD.");
        sb.AppendLine("Write EXACTLY ONE short motivational sentence (max 10 words). " + opener + ".");
        sb.AppendLine("The sentence will be spoken aloud — no symbols, no abbreviations, no punctuation except a period.");
        sb.AppendLine("Reply with ONLY the sentence. No quotes. No explanation.");
        sb.AppendLine();
        sb.AppendLine("GAME: " + gameName);
        sb.AppendLine("LEVEL: " + (ctx.currentLevel + 1));
        sb.AppendLine("WRONG attempts before success: " + ctx.recentWrongCount);
        sb.AppendLine("Correct rounds so far: " + ctx.recentCorrectCount);
        sb.AppendLine();

        if (ctx.justSucceededAfterStruggle)
        {
            sb.AppendLine("SITUATION: The child struggled (needed " + ctx.recentWrongCount +
                          " tries) but JUST succeeded. Celebrate this victory warmly.");
            sb.AppendLine("TONE: Very celebratory and proud. Acknowledge the effort it took.");
            sb.AppendLine("AVOID: Generic phrases like 'great job' or 'well done'.");
            sb.AppendLine("EXAMPLE: 'You kept trying and you did it — that is amazing!'");
        }
        else if (ctx.situation == "struggling")
        {
            sb.AppendLine("SITUATION: The child is finding this difficult. They needed " +
                          ctx.recentWrongCount + " attempts.");
            sb.AppendLine("TONE: Gentle, reassuring, encouraging. Never make the child feel bad.");
            sb.AppendLine("AVOID: Pressure, urgency, or any implication they are failing.");
            sb.AppendLine("EXAMPLE: 'You are trying so hard and that is what matters.'");
        }
        else if (ctx.situation == "perfect")
        {
            sb.AppendLine("SITUATION: The child got it right on the FIRST try with zero mistakes.");
            sb.AppendLine("TONE: Enthusiastic, energetic celebration. They earned it!");
            sb.AppendLine("AVOID: Mild or lukewarm praise — this is a big achievement.");
            sb.AppendLine("EXAMPLE: 'First try and perfect — you are incredible!'");
        }
        else // improving
        {
            sb.AppendLine("SITUATION: The child got it right with a small effort. " + ctx.recentCorrectCount +
                          " correct rounds so far.");
            sb.AppendLine("TONE: Warm, positive encouragement. Build their confidence.");
            sb.AppendLine("EXAMPLE: 'Your memory is getting stronger every round!'");
        }

        if (GameManager.IsArabic())
        {
            sb.AppendLine();
            sb.AppendLine("CRITICAL: Reply ENTIRELY in Arabic. No English words at all.");
            sb.AppendLine("Use simple Egyptian Arabic. Max 8 words. Include full tashkeel (diacritics).");
        }
        return sb.ToString();
    }

    string BuildReportPrompt(ReportContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a clinical neuropsychologist writing a structured session report for a therapist.");
        sb.AppendLine("The data below comes from a serious game targeting visual working memory and emotion recognition in children aged 4-8.");
        sb.AppendLine("Write clinically meaningful insights — do NOT just restate the numbers.");
        sb.AppendLine("Interpret what the numbers mean for the child's cognition and what the therapist should do next.");
        sb.AppendLine("Use plain text only. No markdown, no bullet symbols, no asterisks. Use numbered sections exactly as specified.");
        sb.AppendLine();

        sb.AppendLine("PATIENT: " + ctx.playerName);
        sb.AppendLine("DATE: " + ctx.sessionDate);
        sb.AppendLine("TOTAL SESSIONS ON RECORD: " + ctx.totalSessions);
        sb.AppendLine();

        // ── Game 1 ────────────────────────────────────────────────────────
        sb.AppendLine("=== GAME 1: FRUIT FINDER (Visual Working Memory) ===");
        sb.AppendLine("Levels completed: " + ctx.g1LevelsCompleted + " of 4");
        sb.AppendLine("Levels failed: " + ctx.g1LevelsFailed);

        int g1Total = ctx.g1TotalCorrect + ctx.g1TotalWrong;
        float g1Acc = g1Total > 0 ? (float)ctx.g1TotalCorrect / g1Total * 100f : 0f;
        sb.AppendLine("Overall accuracy: " + g1Acc.ToString("F0") + "% (" + ctx.g1TotalCorrect + " correct / " + g1Total + " total)");
        sb.AppendLine("First session accuracy: " + ctx.g1FirstSessionAcc.ToString("F0") + "%");
        sb.AppendLine("Most recent session accuracy: " + ctx.g1LastSessionAcc.ToString("F0") + "%");

        float g1Trend = ctx.g1LastSessionAcc - ctx.g1FirstSessionAcc;
        sb.AppendLine("Accuracy trend: " + (g1Trend > 5f ? "Improving (+" + g1Trend.ToString("F0") + "%)"
                                          : g1Trend < -5f ? "Declining (" + g1Trend.ToString("F0") + "%)"
                                          : "Stable"));

        sb.AppendLine("Average response time: " + ctx.g1AvgResponseTimeSec.ToString("F1") + "s");
        sb.AppendLine("Response time first session: " + ctx.g1FirstSessionRT.ToString("F1") + "s");
        sb.AppendLine("Response time most recent: " + ctx.g1LastSessionRT.ToString("F1") + "s");

        sb.AppendLine("Hints used: " + ctx.g1HintsUsed +
                      " (Hint 1 elimination: " + ctx.g1Hint1Used +
                      " | Hint 2 memory replay: " + ctx.g1Hint2Used + ")");
        sb.AppendLine("Hint dependency ratio: " + ctx.g1HintDependencyRatio.ToString("F2") + " hints per round");
        sb.AppendLine("Max fruits correctly remembered: " + ctx.g1MaxFruitsReached);
        sb.AppendLine("Max delay successfully handled: " + ctx.g1MaxDelayReached.ToString("F1") + "s");
        sb.AppendLine("Difficulty increased: " + ctx.g1TimesHarderFired + " times | Decreased: " + ctx.g1TimesEasierFired + " times");
        sb.AppendLine("Net direction: " + (ctx.g1DifficultyIncreased ? "Increased overall" : "Decreased or stable"));

        if (ctx.g1GridPerformance?.Count > 0)
        {
            sb.AppendLine("Performance breakdown by grid size, delay, and stimuli count:");
            foreach (var g in ctx.g1GridPerformance) sb.AppendLine("  " + g);
            sb.AppendLine("NOTE FOR SECTION 2: Compare these conditions directly.");
            sb.AppendLine("Which grid size did the child perform best at?");
            sb.AppendLine("At which stimuli count did accuracy start dropping?");
            sb.AppendLine("Does a longer delay help or hurt performance?");
            sb.AppendLine("This tells us the child's working memory capacity ceiling.");
        }

        if (ctx.g1LevelRepetitions?.Count > 0)
        {
            sb.AppendLine("Level repetition analysis:");
            foreach (var r in ctx.g1LevelRepetitions) sb.AppendLine("  " + r);
        }
        sb.AppendLine();

        // ── Game 2 ────────────────────────────────────────────────────────
        sb.AppendLine("=== GAME 2: EMOTION QUEST (Facial Emotion Recognition) ===");
        sb.AppendLine("Levels completed: " + ctx.g2LevelsCompleted + " of 6");
        sb.AppendLine("Levels failed: " + ctx.g2LevelsFailed);

        int g2Total = ctx.g2TotalCorrect + ctx.g2TotalWrong;
        float g2Acc = g2Total > 0 ? (float)ctx.g2TotalCorrect / g2Total * 100f : 0f;
        sb.AppendLine("Overall accuracy: " + g2Acc.ToString("F0") + "%");
        sb.AppendLine("First session accuracy: " + ctx.g2FirstSessionAcc.ToString("F0") + "%");
        sb.AppendLine("Most recent session accuracy: " + ctx.g2LastSessionAcc.ToString("F0") + "%");

        float g2Trend = ctx.g2LastSessionAcc - ctx.g2FirstSessionAcc;
        sb.AppendLine("Accuracy trend: " + (g2Trend > 5f ? "Improving (+" + g2Trend.ToString("F0") + "%)"
                                          : g2Trend < -5f ? "Declining (" + g2Trend.ToString("F0") + "%)"
                                          : "Stable"));

        sb.AppendLine("Average response time: " + ctx.g2AvgResponseTimeSec.ToString("F1") + "s");
        sb.AppendLine("Hints used: " + ctx.g2HintsUsed +
                      " (Hint 1: " + ctx.g2Hint1Used +
                      " | Hint 2: " + ctx.g2Hint2Used + ")");
        sb.AppendLine("Hint dependency: " + ctx.g2HintDependencyRatio.ToString("F2") + " per level");

        if (ctx.g2PerEmotionAccuracy?.Count > 0)
        {
            sb.AppendLine("Per-emotion accuracy:");
            foreach (var e in ctx.g2PerEmotionAccuracy) sb.AppendLine("  " + e);
        }
        if (ctx.g2StrongEmotions?.Count > 0)
            sb.AppendLine("Mastered emotions (no hints): " + string.Join(", ", ctx.g2StrongEmotions));
        if (ctx.g2WeakEmotions?.Count > 0)
            sb.AppendLine("Struggling emotions: " + string.Join(", ", ctx.g2WeakEmotions));
        if (ctx.g2ConfusionPairs?.Count > 0)
        {
            sb.AppendLine("Confusion patterns:");
            foreach (var p in ctx.g2ConfusionPairs) sb.AppendLine("  " + p);
        }
        if (ctx.g2LevelRepetitions?.Count > 0)
        {
            sb.AppendLine("Level repetition analysis:");
            foreach (var r in ctx.g2LevelRepetitions) sb.AppendLine("  " + r);
        }
        sb.AppendLine();

        // ── Emotion Mirror ────────────────────────────────────────────────
        if (ctx.emotionMirrorPlayed)
        {
            sb.AppendLine("=== EMOTION MIRROR (Facial Expression Imitation) ===");
            sb.AppendLine("Total attempts: " + ctx.emotionMirrorTotalAttempts);
            sb.AppendLine("Correct imitations: " + ctx.emotionMirrorCorrect);
            sb.AppendLine("Incorrect imitations: " + ctx.emotionMirrorIncorrect);
            float mirrorAcc = ctx.emotionMirrorTotalAttempts > 0
                ? (float)ctx.emotionMirrorCorrect / ctx.emotionMirrorTotalAttempts * 100f : 0f;
            sb.AppendLine("Imitation accuracy: " + mirrorAcc.ToString("F0") + "%");
            if (ctx.emotionMirrorResults?.Count > 0)
            {
                sb.AppendLine("Results per emotion:");
                foreach (var r in ctx.emotionMirrorResults) sb.AppendLine("  " + r);
            }
            if (ctx.emotionMirrorImprovements?.Count > 0)
                sb.AppendLine("Improved after retry: " + string.Join(", ", ctx.emotionMirrorImprovements));
            sb.AppendLine();
        }

        // ── Report instructions ───────────────────────────────────────────
        sb.AppendLine("Write the clinical report using EXACTLY these numbered sections.");
        sb.AppendLine("Each section must interpret the data — not just repeat it.");
        sb.AppendLine();
        sb.AppendLine("SECTION 1 — SESSION OVERVIEW");
        sb.AppendLine("2-3 sentences. Summarise what was played, overall performance, and one key observation.");
        sb.AppendLine();
        sb.AppendLine("SECTION 2 — VISUAL WORKING MEMORY ANALYSIS");
        sb.AppendLine("Interpret Game 1. Address memory capacity (max fruits and delay), accuracy trend, response time trend, hint dependency, and adaptive difficulty trajectory. Include analysis of performance across different grid sizes and stimuli counts — at which load does performance start to drop?");
        sb.AppendLine();
        sb.AppendLine("SECTION 3 — LEVEL PROGRESSION AND REPETITION");
        sb.AppendLine("For any levels attempted multiple times, interpret whether the child improved across attempts, stayed the same, or declined. What does the repetition pattern suggest about learning rate and frustration tolerance?");
        sb.AppendLine();
        sb.AppendLine("SECTION 4 — EMOTION RECOGNITION ANALYSIS");
        sb.AppendLine("Interpret Game 2. Address per-emotion accuracy, confusion patterns and their clinical meaning, hint dependency, and improvement trend.");
        if (ctx.emotionMirrorPlayed)
        {
            sb.AppendLine();
            sb.AppendLine("SECTION 5 — EMOTION MIRROR ANALYSIS");
            sb.AppendLine("Interpret the imitation results. Compare recognition accuracy (Game 2) with imitation accuracy (Emotion Mirror) for the same emotions. Does the child struggle more to recognise or to produce expressions? What does this suggest about their emotional processing?");
        }
        sb.AppendLine();
        string nextSection = ctx.emotionMirrorPlayed ? "SECTION 6" : "SECTION 5";
        sb.AppendLine(nextSection + " — STRENGTHS");
        sb.AppendLine("2-4 specific strengths directly referencing data.");
        sb.AppendLine();
        nextSection = ctx.emotionMirrorPlayed ? "SECTION 7" : "SECTION 6";
        sb.AppendLine(nextSection + " — AREAS FOR DEVELOPMENT");
        sb.AppendLine("2-4 specific areas directly referencing data.");
        sb.AppendLine();
        nextSection = ctx.emotionMirrorPlayed ? "SECTION 8" : "SECTION 7";
        sb.AppendLine(nextSection + " — CLINICAL RECOMMENDATIONS");
        sb.AppendLine("3-5 actionable recommendations. Reference specific confusion pairs, grid sizes, or difficulty parameters where relevant.");
        sb.AppendLine();
        sb.AppendLine("End with: Report generated by Sunvale AI · " + ctx.sessionDate);
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // API CALLS — GROQ
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator CallGroq(string prompt, int maxTokens, System.Action<string> callback)
    {
        if (groqApiKey == "YOUR_GROQ_API_KEY") { callback(null); yield break; }

        string body = "{\"model\":\"" + kGroqModel + "\",\"max_tokens\":" + maxTokens +
                      ",\"temperature\":0.95,\"messages\":[{\"role\":\"user\",\"content\":" +
                      JsonEscape(prompt) + "}]}";

        using var req = new UnityWebRequest(kGroqEndpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + groqApiKey);
        req.timeout = 12;

        yield return req.SendWebRequest();

        callback(req.result != UnityWebRequest.Result.Success
            ? null : ParseOpenAI(req.downloadHandler.text));
    }

    // ════════════════════════════════════════════════════════════════════════
    // API CALLS — GEMINI
    // ════════════════════════════════════════════════════════════════════════

    IEnumerator CallGemini(string prompt, int maxTokens, System.Action<string> callback)
    {
        if (geminiApiKey == "YOUR_GEMINI_API_KEY") { callback(null); yield break; }

        string url = kGeminiEndpoint + "?key=" + geminiApiKey;
        string body = "{\"contents\":[{\"parts\":[{\"text\":" + JsonEscape(prompt) + "}]}]," +
                      "\"generationConfig\":{\"maxOutputTokens\":" + maxTokens + ",\"temperature\":0.4}}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 30;

        yield return req.SendWebRequest();

        callback(req.result != UnityWebRequest.Result.Success
            ? null : ParseGemini(req.downloadHandler.text));
    }

    // ════════════════════════════════════════════════════════════════════════
    // RESPONSE PARSERS
    // ════════════════════════════════════════════════════════════════════════

    string ParseOpenAI(string json)
    {
        try
        {
            int ci = json.IndexOf("\"content\":");
            if (ci < 0) return null;
            int s = json.IndexOf('"', ci + 10) + 1;
            int e = FindEndQuote(json, s);
            return e < 0 ? null : Unescape(json.Substring(s, e - s)).Trim();
        }
        catch { return null; }
    }

    string ParseGemini(string json)
    {
        try
        {
            int ti = json.IndexOf("\"text\":");
            if (ti < 0) return null;
            int s = json.IndexOf('"', ti + 7) + 1;
            int e = FindEndQuote(json, s);
            return e < 0 ? null : Unescape(json.Substring(s, e - s)).Trim();
        }
        catch { return null; }
    }

    int FindEndQuote(string json, int start)
    {
        int e = json.IndexOf('"', start);
        while (e > 0 && json[e - 1] == '\\') e = json.IndexOf('"', e + 1);
        return e;
    }

    // ════════════════════════════════════════════════════════════════════════
    // FALLBACKS
    // ════════════════════════════════════════════════════════════════════════

    string GetFallbackHint(string emotion, int attempt, string hintType = "situational")
    {
        var dict = hintType == "facial" ? kFallbackFacial : kFallbackSituational;
        return dict.TryGetValue(emotion, out var hint)
            ? hint
            : "Look carefully at the eyes and mouth!";
    }

    string BuildFallbackReport(ReportContext ctx) =>
    "REPORT — " + ctx.playerName + " (" + ctx.sessionDate + ")\n\n" +
    "Game 1: " + ctx.g1TotalCorrect + " correct, " + ctx.g1TotalWrong + " wrong, " +
    ctx.g1AvgResponseTimeSec.ToString("F1") + "s avg response time.\n\n" +
    "Game 2: " + ctx.g2TotalCorrect + " correct, " + ctx.g2TotalWrong + " wrong.\n" +
    (ctx.g2ConfusionPairs?.Count > 0 ? "Confusions: " + string.Join(", ", ctx.g2ConfusionPairs) + "\n" : "") +
    "\n(Full AI report unavailable — check API keys in LLMService Inspector.)";

    // ════════════════════════════════════════════════════════════════════════
    // JSON UTILITIES
    // ════════════════════════════════════════════════════════════════════════

    static string JsonEscape(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t") + "\"";

    static string Unescape(string s) =>
        s.Replace("\\n", "\n").Replace("\\r", "").Replace("\\t", "\t")
         .Replace("\\\"", "\"").Replace("\\\\", "\\");

    static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
} // end LLMService