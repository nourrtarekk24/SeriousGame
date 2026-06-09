// ═══════════════════════════════════════════════════════════════════════════
// TTS INTEGRATION PATCH
// ═══════════════════════════════════════════════════════════════════════════
//
// This file shows EXACTLY what to change in both RoundManagerMG1 and
// RoundManager2 to wire TTS into every Lumi message.
//
// STEP 1 — Replace ShowLumi() in BOTH managers with this version:
//
//    void ShowLumi(string message)
//    {
//        if (lumiCorner  == null) return;
//        lumiCorner.SetActive(true);
//        if (speechBubble == null) return;
//
//        DOTween.Kill(speechBubble.transform);
//        speechBubble.SetActive(true);
//
//        if (speechText != null)
//        {
//            speechText.gameObject.SetActive(true);
//            speechText.text = message;
//            speechText.ForceMeshUpdate();
//        }
//
//        speechBubble.transform.localScale = Vector3.zero;
//        speechBubble.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
//
//        // ── TTS: speak the message aloud ──────────────────────────────────
//        // Text is shown immediately above. Audio plays when it arrives.
//        // Stops any currently playing Lumi voice first to avoid overlap.
//        if (LLMService.Instance != null)
//        {
//            LLMService.Instance.StopSpeaking(lumiAudio);
//            LLMService.Instance.SpeakOnSource(message, lumiAudio);
//        }
//    }
//
// That is the ONLY change needed in both managers.
// Everything else (hints, motivation, demo lines) goes through ShowLumi()
// so TTS works automatically everywhere.
//
// ─────────────────────────────────────────────────────────────────────────
// STEP 2 — Add prewarm calls in Start() of each manager:
//
// In RoundManagerMG1.Start(), after LoadAdaptiveState():
//
//    if (LLMService.Instance != null)
//    {
//        LLMService.Instance.PrewarmCache(new[] {
//            "Remember this fruit!",
//            "Get ready! The grid is coming!",
//            "Let me help you! Some fruits are going away!",
//            "Here is a hint! Look at this fruit again!",
//            "Here is a hint! Remember the order!",
//            "You can do it!",
//            "Well done! You found it!"
//        }, lumiAudio);
//    }
//
// In RoundManager2.Start(), after UpdateHUD():
//
//    if (LLMService.Instance != null)
//    {
//        LLMService.Instance.PrewarmCache(new[] {
//            "Hmm, let me give you a clue...",
//            "Here is another clue! Listen carefully too!",
//            "Take your time!",
//            "Look at this face carefully.",
//            "How does this friend feel?"
//        }, lumiAudio);
//    }
//
// ─────────────────────────────────────────────────────────────────────────
// STEP 3 — Add LLMService null-check Awake() in both managers
// (already done in RoundManager2, add to RoundManagerMG1 Awake):
//
//    if (LLMService.Instance == null)
//        new GameObject("LLMService").AddComponent<LLMService>();
//
// ─────────────────────────────────────────────────────────────────────────
// WHY THIS DESIGN:
//
// • ShowLumi() is the single point where ALL Lumi messages appear —
//   demo lines, hints, motivation, instructions, correct feedback.
//   Patching one method means TTS works everywhere automatically.
//
// • StopSpeaking() before SpeakOnSource() prevents audio overlap
//   when Lumi switches from one message to another quickly.
//
// • PrewarmCache() runs during Start() so fixed phrases are fetched
//   in the background while the demo plays or the round loads.
//   By the time Lumi first speaks, those clips are cached and play instantly.
//
// • SpeakOnSource() checks VoiceMuted PlayerPref — if the therapist
//   muted voice in settings, TTS is silently skipped. No code change needed.
//
// ─────────────────────────────────────────────────────────────────────────
// IMPORTANT NOTE ON lumiAudio:
//
// The lumiAudio AudioSource in both managers was previously used only for
// pre-recorded demo voice clips (lumiDemoIntro, lumiDemoLook, etc.).
// With TTS enabled:
//   • TTS plays through lumiAudio (replacing or supplementing demo clips)
//   • Pre-recorded demo clips can remain in the Inspector — they play via
//     PlayVoice(clip) which calls lumiAudio.PlayOneShot(clip)
//   • TTS uses lumiAudio.clip + lumiAudio.Play() — so they DON'T overlap
//     because StopSpeaking() is called first
//   • If you want demo clips to remain and TTS to only play for LLM messages,
//     remove the TTS call from ShowLumi() and instead call SpeakOnSource()
//     only inside Attempt1LLMHint and Attempt2LLMHintPlusSound callbacks.
// ═══════════════════════════════════════════════════════════════════════════