using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class MusicManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static MusicManager Instance { get; private set; }

    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Music Clip")]
    [Tooltip("Drag your background music clip here")]
    public AudioClip backgroundMusic;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [Tooltip("Normal music volume")]
    public float normalVolume = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("Volume while Lumi is speaking — quiet but still present")]
    public float duckVolume = 0.08f;

    [Tooltip("Fade duration for scene transitions (slower)")]
    public float fadeDuration = 1.2f;

    [Tooltip("Fade duration for speech ducking (faster = more responsive)")]
    public float duckFadeDuration = 0.4f;

    // ── Private ──────────────────────────────────────────────────────────────
    private AudioSource _source;
    private bool _muted = false;
    private bool _playing = false;

    // Scenes where music STOPS entirely
    private static readonly string[] kSilentScenes =
    {
        "LevelCompleteScene",
        "LevelLostScene"
    };

    // ════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = backgroundMusic;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        _source.volume = 0f;
        _source.priority = 128;
    }

    void Start()
    {
        _muted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (!_muted && backgroundMusic != null)
        {
            _playing = true;
            FadeIn();
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SCENE CHANGE
    // ════════════════════════════════════════════════════════════════════════

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _muted = PlayerPrefs.GetInt("MusicMuted", 0) == 1;

        if (_muted) { _playing = false; FadeOut(); return; }

        bool silent = System.Array.IndexOf(kSilentScenes, scene.name) >= 0;
        if (silent) { _playing = false; FadeOut(); }
        else { _playing = true; FadeIn(); }
    }

    // ════════════════════════════════════════════════════════════════════════
    // DUCKING
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call when Lumi starts speaking. Music fades to a quiet background level.
    /// Usage anywhere: MusicManager.Instance?.DuckMusic();
    /// </summary>
    public void DuckMusic()
    {
        if (_muted || !_playing || !_source.isPlaying) return;
        DOTween.Kill(_source);
        DOTween.To(() => _source.volume, x => _source.volume = x,
                   duckVolume, duckFadeDuration);
    }

    /// <summary>
    /// Call when Lumi finishes speaking. Music fades back to normal.
    /// Usage anywhere: MusicManager.Instance?.UnduckMusic();
    /// </summary>
    public void UnduckMusic()
    {
        if (_muted || !_playing || !_source.isPlaying) return;
        DOTween.Kill(_source);
        DOTween.To(() => _source.volume, x => _source.volume = x,
                   normalVolume, duckFadeDuration);
    }

    // ════════════════════════════════════════════════════════════════════════
    // FADE IN / OUT
    // ════════════════════════════════════════════════════════════════════════

    void FadeIn()
    {
        if (backgroundMusic == null) return;
        if (!_source.isPlaying)
        {
            _source.clip = backgroundMusic;
            _source.volume = 0f;
            _source.Play();
        }
        DOTween.Kill(_source);
        DOTween.To(() => _source.volume, x => _source.volume = x,
                   normalVolume, fadeDuration);
    }

    void FadeOut()
    {
        if (!_source.isPlaying) return;
        DOTween.Kill(_source);
        DOTween.To(() => _source.volume, x => _source.volume = x, 0f, fadeDuration)
               .OnComplete(() => { if (_source != null) _source.Stop(); });
    }

    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC
    // ════════════════════════════════════════════════════════════════════════

    public void SetMuted(bool muted)
    {
        _muted = muted;
        if (_muted) { _playing = false; FadeOut(); }
        else { _playing = true; FadeIn(); }
    }
}