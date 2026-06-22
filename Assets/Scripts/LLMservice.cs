using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

public class LLMService : MonoBehaviour
{
    public static LLMService Instance { get; private set; }

    [Header("LLM API Keys")]
    [Tooltip("Groq API key — real-time hints and motivation (free at console.groq.com)")]
    public string groqApiKey = "YOUR_GROQ_API_KEY";

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

    private const string kGroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string kGroqModel = "llama-3.3-70b-versatile";
    private const string kElevenLabsBase = "https://api.elevenlabs.io/v1/text-to-speech/";

    private const int kHintTokens = 80;
    private const int kMotivationTokens = 60;

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

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    public void GetHint(HintContext ctx, System.Action<string> callback)
    {
        StartCoroutine(CallGroq(BuildHintPrompt(ctx), kHintTokens, result =>
        {
            bool usedFallback = string.IsNullOrEmpty(result);
            string final = usedFallback ? GetFallbackHint(ctx.correctEmotion, ctx.attemptNumber, ctx.hintType) : result;
            Debug.Log("[LLM] Hint " + (usedFallback ? "FALLBACK" : "Groq") +
                      " (" + ctx.correctEmotion + "): " + final);
            if (!string.IsNullOrEmpty(final)) callback(final);
        }));
    }

    public void GetMotivation(MotivationContext ctx, System.Action<string> callback)
    {
        StartCoroutine(CallGroq(BuildMotivationPrompt(ctx), kMotivationTokens, result =>
        {
            bool usedFallback = string.IsNullOrEmpty(result);
            string final = usedFallback
                ? kFallbackMotivation[Random.Range(0, kFallbackMotivation.Length)]
                : result;
            if (!string.IsNullOrEmpty(final)) callback(final);
        }));
    }

    public void Speak(string text)
    {
        if (lumiAudioSource == null)
        {
            Debug.LogWarning("[TTS] lumiAudioSource not assigned on LLMService.");
            return;
        }
        SpeakOnSource(text, lumiAudioSource);
    }

    public void SpeakOnSource(string text, AudioSource source)
    {
        if (source == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (PlayerPrefs.GetInt("VoiceMuted", 0) == 1) return;

        if (GameManager.IsArabic())
        {
            StartCoroutine(FetchAndPlayArabic(text, source));
            return;
        }

        if (TTSCache.TryGet(text, lumiVoiceId, out AudioClip cached))
        {
            PlayClip(source, cached);
            return;
        }
        StartCoroutine(FetchAndPlay(text, source));
    }

    public void StopSpeaking(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

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

            if (TTSCache.TryGet(phrase, lumiVoiceId, out _)) continue;

            yield return StartCoroutine(FetchAndCache(phrase));

            yield return new WaitForSeconds(0.4f);
        }
    }

    private const string kArabicTTSUrl = "http://localhost:5051/tts";

    private string ArabicVoice()
    {
        bool isMale = lumiVoiceId != null && lumiVoiceId.ToLower().Contains("male");
        return isMale ? "ar-EG-ShakirNeural" : "ar-EG-SalmaNeural";
    }

    IEnumerator FetchAndPlayArabic(string text, AudioSource source)
    {
        string cacheKey = "AR_" + ArabicVoice();
        string cachedPath = ArabicWavCachePath(text, cacheKey);

        if (System.IO.File.Exists(cachedPath))
        {
            yield return StartCoroutine(LoadWavAndPlay(cachedPath, source, deleteAfter: false));
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
        bool isWav = audioBytes != null && audioBytes.Length > 44 &&
                     audioBytes[0] == 'R' && audioBytes[1] == 'I' &&
                     audioBytes[2] == 'F' && audioBytes[3] == 'F' &&
                     audioBytes[8] == 'W' && audioBytes[9] == 'A' &&
                     audioBytes[10] == 'V' && audioBytes[11] == 'E';

        if (!isWav)
        {
            Debug.LogWarning("[ArabicTTS] Server did not return a valid WAV file " +
                "(possibly an error page or crashed server response) — skipping playback. " +
                "Bytes received: " + (audioBytes != null ? audioBytes.Length : 0));
            yield break;
        }

        try
        {
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(cachedPath));
            System.IO.File.WriteAllBytes(cachedPath, audioBytes);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArabicTTS] Could not write WAV cache: " + e.Message);
        }

        yield return StartCoroutine(LoadWavAndPlay(cachedPath, source, deleteAfter: false));
        Debug.Log("[ArabicTTS] ✓ " + Truncate(text, 40));
    }

    string ArabicWavCachePath(string text, string voiceId)
    {
        string combined = voiceId + "|" + text.ToLowerInvariant().Trim();
        uint hash = 5381;
        foreach (char c in combined)
            hash = ((hash << 5) + hash) ^ (uint)c;
        string key = hash.ToString("x8");
        string folder = System.IO.Path.Combine(
            Application.persistentDataPath, "ArabicTTSCache");
        return System.IO.Path.Combine(folder, key + ".wav");
    }

    IEnumerator LoadWavAndPlay(string wavPath, AudioSource source, bool deleteAfter)
    {
        string fileUri = "file:///" + wavPath.Replace("\\", "/");
        using var audioReq = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV);
        yield return audioReq.SendWebRequest();

        AudioClip clip = null;
        if (audioReq.result == UnityWebRequest.Result.Success)
            clip = DownloadHandlerAudioClip.GetContent(audioReq);
        else
            Debug.LogWarning("[ArabicTTS] Failed to decode WAV: " + audioReq.error);

        if (deleteAfter)
        {
            try { System.IO.File.Delete(wavPath); } catch { }
        }

        if (clip != null && clip.length > 0.05f)
            PlayClip(source, clip);
        else
            Debug.LogWarning("[ArabicTTS] Clip was null or too short.");
    }

    IEnumerator FetchAndPlay(string text, AudioSource source)
    {
        AudioClip clip = null;
        yield return StartCoroutine(FetchTTS(text, result => clip = result));

        if (clip != null && source != null)
            PlayClip(source, clip);
    }

    IEnumerator FetchAndCache(string text)
    {
        yield return StartCoroutine(FetchTTS(text, _ => { }));
    }

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
        source.pitch = 1f;
        source.volume = 1f;
        source.Play();
    }

    public class HintContext
    {
        public string correctEmotion;
        public string selectedEmotion;
        public int attemptNumber;
        public float responseTimeSec;
        public List<string> wrongHistory;
        public string confusionPattern;

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
        int seed = Random.Range(1000, 9999);
        var sb = new StringBuilder();
        sb.AppendLine("SEED:" + seed);
        sb.AppendLine();
        sb.AppendLine("You are Lumi, a warm friendly helper in a children's emotion recognition game for ages 4-8.");
        sb.AppendLine("A child got the wrong answer. Give them a NEW situational hint they have NOT heard before.");
        sb.AppendLine();
        sb.AppendLine("Correct emotion: " + ToArabicEmotionIfNeeded(ctx.correctEmotion));
        if (!string.IsNullOrEmpty(ctx.selectedEmotion))
            sb.AppendLine("Child guessed: " + ToArabicEmotionIfNeeded(ctx.selectedEmotion) + " (wrong)");
        sb.AppendLine("Hint attempt: " + ctx.attemptNumber + " — use a DIFFERENT situation from any previous hint.");
        sb.AppendLine();
        sb.AppendLine("Write EXACTLY ONE sentence using ONE of these two structures:");
        sb.AppendLine("  THIRD PERSON: 'If [someone/a friend/a child] [situation], how do you think they feel?'");
        sb.AppendLine("  SECOND PERSON: 'How would you feel if [situation that happened to you]?'");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Third person sentences start with 'If' and end with 'how do you think they feel?'");
        sb.AppendLine("- Second person sentences start with 'How would you feel if'");
        sb.AppendLine("- The situation must be a simple visible behaviour a 4-8 year old recognises");
        sb.AppendLine("- Do NOT name the emotion in the sentence");
        sb.AppendLine("- Do NOT mention facial features or anatomy");
        sb.AppendLine("- The situation must ONLY match the correct emotion — not the wrong guess");
        sb.AppendLine("- Max 14 words total. Spoken aloud — no symbols.");
        sb.AppendLine("- Use a DIFFERENT scenario each call — vary the location, action, and subject");
        sb.AppendLine();
        sb.AppendLine("VARIETY EXAMPLES for " + ctx.correctEmotion + ":");
        sb.AppendLine("  Locations: playground, home, birthday party, school, park, store, bedtime");
        sb.AppendLine("  Third person subjects: a friend, a child, someone, a little girl, a little boy");
        sb.AppendLine("  Second person examples:");
        sb.AppendLine("    'How would you feel if someone brought you a surprise gift?'");
        sb.AppendLine("    'How would you feel if your favourite toy broke?'");
        sb.AppendLine("    'How would you feel if someone took your snack away?'");
        sb.AppendLine("  Mix between the two forms across calls for variety.");
        sb.AppendLine();
        sb.AppendLine("REFERENCE EXAMPLES (do not copy these exactly — create something new):");
        sb.AppendLine("- Happy (third):    If someone is laughing and clapping their hands, how do you think they feel?");
        sb.AppendLine("- Happy (second):   How would you feel if you won a prize at school?");
        sb.AppendLine("- Sad (third):      If someone is crying and wiping their tears, how do you think they feel?");
        sb.AppendLine("- Sad (second):     How would you feel if your best friend moved away?");
        sb.AppendLine("- Fear (third):     If someone is shaking and hiding behind a door, how do you think they feel?");
        sb.AppendLine("- Fear (second):    How would you feel if you heard a very loud scary noise at night?");
        sb.AppendLine("- Angry (third):    If someone is stomping their feet and shouting, how do you think they feel?");
        sb.AppendLine("- Angry (second):   How would you feel if someone broke your drawing on purpose?");
        sb.AppendLine("- Surprised (third):  If someone just got an unexpected gift and gasped, how do you think they feel?");
        sb.AppendLine("- Surprised (second): How would you feel if your friends jumped out and shouted surprise?");
        sb.AppendLine("- Disgusted (third):  If someone smelled something bad and backed away, how do you think they feel?");
        sb.AppendLine("- Disgusted (second): How would you feel if you tasted something really horrible?");
        sb.AppendLine("- Neutral (third):    If someone is sitting quietly and waiting calmly, how do you think they feel?");
        sb.AppendLine("- Neutral (second):   How would you feel if you were just sitting and waiting for nothing special?");
        sb.AppendLine();
        sb.AppendLine("CRITICAL SAFETY RULE:");
        sb.AppendLine("The situation you describe must be IMPOSSIBLE to interpret as: " +
                      (string.IsNullOrEmpty(ctx.selectedEmotion) ? "any other emotion" : ctx.selectedEmotion));
        sb.AppendLine("If it could also match the wrong guess, choose a completely different scenario.");
        sb.AppendLine();
        sb.AppendLine("Reply with ONLY the one sentence. No preamble. No explanation.");

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
        int seed = Random.Range(1000, 9999);
        var sb = new StringBuilder();
        sb.AppendLine("SEED:" + seed);
        sb.AppendLine();
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
        int seed = Random.Range(1000, 9999);

        string situation = ctx.justSucceededAfterStruggle ? "succeeded after struggling"
                         : ctx.situation == "perfect" ? "perfect"
                         : ctx.situation == "struggling" ? "struggling"
                         : "improving";

        string tone = ctx.justSucceededAfterStruggle ? "warm and proud"
                    : ctx.situation == "perfect" ? "enthusiastic"
                    : ctx.situation == "struggling" ? "gentle and reassuring"
                    : "encouraging";

        string gameName = ctx.game == 1 ? "Fruit Finder" : "Emotion Quest";

        var sb = new StringBuilder();
        sb.AppendLine("SEED:" + seed);
        sb.AppendLine("You are Lumi, a warm encouraging character in a children's game (ages 6-10).");
        sb.AppendLine("Write ONE motivational sentence (max 10 words).");
        sb.AppendLine("Situation: " + situation);
        sb.AppendLine("Game: " + gameName);
        sb.AppendLine("Level: " + (ctx.currentLevel + 1));
        sb.AppendLine("Recent wrong: " + ctx.recentWrongCount);
        sb.AppendLine("Recent correct: " + ctx.recentCorrectCount);
        sb.AppendLine("Tone: " + tone);
        sb.AppendLine();

        if (situation == "perfect")
        {
            sb.AppendLine("This child made ZERO mistakes. Be energetic — mention speed, sharpness, or first try.");
            sb.AppendLine("Example: 'First try, zero mistakes, your mind is sharp!'");
        }
        else if (situation == "struggling")
        {
            sb.AppendLine("This child is STILL trying, has not succeeded yet. Be soft and patient — no excitement, no exclamation energy.");
            sb.AppendLine("Example: 'Take your time, you are learning something new.'");
        }
        else if (situation == "succeeded after struggling")
        {
            sb.AppendLine("This child needed " + ctx.recentWrongCount + " tries but just succeeded. Acknowledge the EFFORT, not just the win.");
            sb.AppendLine("Example: 'You did not give up and now you got it.'");
        }
        else
        {
            sb.AppendLine("This child is doing fine with normal effort. Be warm but calm — not overly excited.");
            sb.AppendLine("Example: 'Nice work, you are getting the hang of it.'");
        }

        sb.AppendLine();
        sb.AppendLine("The sentence MUST sound different from the example above — same tone, new wording.");
        sb.AppendLine("Reply with ONLY the sentence. No quotes. No symbols. Spoken aloud.");

        if (GameManager.IsArabic())
        {
            sb.AppendLine();
            sb.AppendLine("CRITICAL: Reply ENTIRELY in Arabic. No English words. Simple Egyptian Arabic. Max 8 words. Full tashkeel.");
        }
        return sb.ToString();
    }

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

    int FindEndQuote(string json, int start)
    {
        int e = json.IndexOf('"', start);
        while (e > 0 && json[e - 1] == '\\') e = json.IndexOf('"', e + 1);
        return e;
    }

    string GetFallbackHint(string emotion, int attempt, string hintType = "situational")
    {
        var dict = hintType == "facial" ? kFallbackFacial : kFallbackSituational;
        return dict.TryGetValue(emotion, out var hint)
            ? hint
            : "Look carefully at the eyes and mouth!";
    }

    static string JsonEscape(string s) =>
    "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t") + "\"";

    static string Unescape(string s) =>
    s.Replace("\\n", "\n").Replace("\\r", "").Replace("\\t", "\t")
     .Replace("\\\"", "\"").Replace("\\\\", "\\");

    static string Truncate(string s, int max) =>
    s.Length <= max ? s : s.Substring(0, max) + "...";
}