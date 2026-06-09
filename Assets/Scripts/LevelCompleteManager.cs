using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;

public class LevelCompleteManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public Image[] stars;
    public Sprite starFilled;
    public Sprite starEmpty;
    public TextMeshProUGUI coinsEarnedText;
    public GameObject wonPanel;

    [Header("Celebration")]
    public GameObject confettiPrefab;

    [Header("Lumi")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    [Header("Audio")]
    public AudioSource sfxAudio;
    public AudioClip celebrateSfx;

    void Awake()
    {
        if (GameManager.Instance == null)
        {
            GameObject gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();
        }
        if (HeartManager.Instance == null)
        {
            GameObject hm = new GameObject("HeartManager");
            hm.AddComponent<HeartManager>();
        }

        // Hide both Lumi objects immediately in Awake so there is
        // zero chance of a one-frame flash before ShowLumi() fires.
       
    }

    void Start()
    {
        // ── FIX: create AudioSource at runtime if not assigned ──────────────
        // This is why SFX never played — sfxAudio was None in the Inspector.
        // We create one here as a fallback so the celebrate sound always works.
        if (sfxAudio == null)
            sfxAudio = gameObject.AddComponent<AudioSource>();

        if (wonPanel != null)
            wonPanel.transform.localScale = Vector3.zero;

        StartCoroutine(ShowWonResults());
    }

    // ── FIX: ShowLumi now defaults to male if GameManager is null ────────────
    // Previously: isBoy = Instance != null && gender == 0
    //   → if Instance is null, isBoy = false → shows female even for boys
    // Now: isBoy = Instance == null (default male) || gender == 0
    //   → if Instance is null, defaults to male safely


    IEnumerator ShowWonResults()
    {
        yield return new WaitForSeconds(0.3f);

        if (GameManager.Instance == null) yield break;

        int game = GameManager.Instance.currentGame;
        int level = GameManager.Instance.currentLevel;

        int starsEarned = game == 1
            ? GameManager.Instance.mg1Stars[level]
            : GameManager.Instance.mg2Stars[level];

        int coins = GameManager.Instance.lastSessionCoins;

        // Animate panel in — show Lumi only after panel fully visible
        wonPanel.transform
            .DOScale(1f, 0.4f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                
                // ── FIX: play SFX here — inside OnComplete — so it fires
                // exactly when the panel finishes animating in, not 2s later.
                // Previously it played after stars + confetti which felt very late.
                if (sfxAudio != null && celebrateSfx != null)
                    sfxAudio.PlayOneShot(celebrateSfx);
            });

        // Show coins earned
        if (coinsEarnedText != null)
        {
            coinsEarnedText.text = "+" + coins.ToString();
            coinsEarnedText.transform
                .DOScale(1.2f, 0.3f)
                .SetLoops(2, LoopType.Yoyo);
        }

        yield return new WaitForSeconds(0.5f);

        // Animate stars one by one
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;

            if (i < starsEarned)
            {
                stars[i].sprite = starFilled;
                stars[i].color = new Color(0.96f, 0.89f, 0.63f, 1f);
                stars[i].transform
                    .DOScale(1.3f, 0.2f)
                    .SetLoops(2, LoopType.Yoyo);
            }
            else
            {
                stars[i].sprite = starEmpty;
                stars[i].color = new Color(0.71f, 0.70f, 0.66f, 1f);
            }

            yield return new WaitForSeconds(0.3f);
        }

        if (titleText != null)
            titleText.text = starsEarned == 3 ? "Perfect!" : "Well Done!";

        yield return new WaitForSeconds(0.7f);

        if (confettiPrefab != null)
        {
            Vector3 spawnPos = Camera.main.transform.position + new Vector3(0, 0, 5f);
            Instantiate(confettiPrefab, spawnPos, Quaternion.identity);
        }
    }

    public void OnContinuePressed()
    {
        DOTween.KillAll();

        if (GameManager.Instance == null)
        {
            SceneManager.LoadScene("HubScene");
            return;
        }

        int game = GameManager.Instance.currentGame;
        int level = GameManager.Instance.currentLevel;

        bool canRescue = level < GameManager.Instance.mg1Stars.Length
                      && level < GameManager.Instance.mg2Stars.Length
                      && level < GameManager.Instance.friendsRescued.Length;

        if (canRescue)
        {
            bool mg1Done = GameManager.Instance.mg1Stars[level] > 0;
            bool mg2Done = GameManager.Instance.mg2Stars[level] > 0;
            bool alreadyRescued = GameManager.Instance.friendsRescued[level];

            if (mg1Done && mg2Done && !alreadyRescued)
            {
                GameManager.Instance.friendsRescued[level] = true;
                GameManager.Instance.SaveData();
                SceneManager.LoadScene("FriendRescueScene");
                return;
            }
        }

        string nextScene = game == 1 ? "LevelSelectMG1" : "LevelSelectMG2";
        SceneManager.LoadScene(nextScene);
    }

    public void OnReplayPressed()
    {
        DOTween.KillAll();

        if (GameManager.Instance == null)
        {
            SceneManager.LoadScene("HubScene");
            return;
        }

        int game = GameManager.Instance.currentGame;
        string sceneName = game == 1 ? "MG1Scene" : "MG2Scene(2)";
        SceneManager.LoadScene(sceneName);
    }
}