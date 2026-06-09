using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

/// <summary>
/// TTSCache — persists generated TTS audio between sessions.
///
/// WHY THIS EXISTS:
/// ElevenLabs charges per character. Fixed phrases like "Remember this fruit!"
/// or "Get ready! The grid is coming!" are spoken dozens of times across
/// a session. Without caching, every ShowLumi() call costs API credits.
///
/// HOW IT WORKS:
/// • Each phrase is hashed to a filename: SHA256(text+voiceId) → hex.pcm
/// • Raw PCM bytes (16-bit, 16kHz, mono) are stored in Application.persistentDataPath/TTSCache/
/// • On load, cached bytes are converted back to an AudioClip
/// • In-memory dictionary keeps clips alive for the current session
/// • Cache survives app restarts — fixed phrases are only synthesised once ever
///
/// CACHE SIZE:
/// A 5-second clip at pcm_16000 = ~160KB. 50 unique phrases ≈ 8MB.
/// Acceptable for a therapy app.
/// </summary>
public static class TTSCache
{
    // In-memory cache for the current session (text → AudioClip)
    private static readonly Dictionary<string, AudioClip> _memory =
        new Dictionary<string, AudioClip>();

    private static string CacheFolder =>
        Path.Combine(Application.persistentDataPath, "TTSCache");

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Returns true and sets clip if this text is cached on disk or in memory.</summary>
    public static bool TryGet(string text, string voiceId, out AudioClip clip)
    {
        string key = MakeKey(text, voiceId);

        // 1. Check in-memory first (fastest)
        if (_memory.TryGetValue(key, out clip) && clip != null)
            return true;

        // 2. Check disk cache
        string path = Path.Combine(CacheFolder, key + ".pcm");
        if (!File.Exists(path)) { clip = null; return false; }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            clip = BytesToClip(bytes, key);
            if (clip != null)
            {
                _memory[key] = clip;
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TTSCache] Load error: " + e.Message);
        }

        clip = null;
        return false;
    }

    /// <summary>Stores raw PCM bytes on disk and converts to AudioClip in memory.</summary>
    public static AudioClip Store(string text, string voiceId, byte[] pcmBytes)
    {
        string key = MakeKey(text, voiceId);

        // Write to disk
        try
        {
            Directory.CreateDirectory(CacheFolder);
            File.WriteAllBytes(Path.Combine(CacheFolder, key + ".pcm"), pcmBytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TTSCache] Write error: " + e.Message);
        }

        // Convert and store in memory
        AudioClip clip = BytesToClip(pcmBytes, key);
        if (clip != null) _memory[key] = clip;
        return clip;
    }

    /// <summary>Clears in-memory cache (called on scene transitions to free memory).</summary>
    public static void ClearMemory() => _memory.Clear();

    /// <summary>Deletes all cached PCM files from disk (for testing / reset).</summary>
    public static void ClearDisk()
    {
        if (Directory.Exists(CacheFolder))
            Directory.Delete(CacheFolder, recursive: true);
        _memory.Clear();
        Debug.Log("[TTSCache] Disk cache cleared.");
    }

    // ── Internal helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Converts raw PCM bytes (16-bit signed, 16kHz, mono) to an AudioClip.
    /// ElevenLabs pcm_16000 format matches this exactly.
    /// </summary>
    static AudioClip BytesToClip(byte[] pcm, string clipName)
    {
        if (pcm == null || pcm.Length < 2) return null;

        int sampleCount = pcm.Length / 2;           // 2 bytes per 16-bit sample
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // Little-endian 16-bit signed → normalised float [-1, 1]
            short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            samples[i] = s / 32768f;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount,
            channels: 1, frequency: 16000, stream: false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Creates a short stable filename from text + voiceId.
    /// Uses a simple djb2 hash — no crypto libraries needed.
    /// </summary>
    static string MakeKey(string text, string voiceId)
    {
        string combined = voiceId + "|" + text.ToLowerInvariant().Trim();
        uint hash = 5381;
        foreach (char c in combined)
            hash = ((hash << 5) + hash) ^ (uint)c;
        return hash.ToString("x8");   // 8-char hex, e.g. "a3f2c1d0"
    }
}