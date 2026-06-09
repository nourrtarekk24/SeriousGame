using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;

public class LevelLostManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public Image[] stars;
    public Sprite starEmpty;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI coinsEarnedText;
    public GameObject lostPanel;

    [Header("Audio")]
    public AudioSource sfxAudio;
    public AudioClip failSfx;

    [Header("Lumi")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    private string[] comfortMessages = {
        "Do not worry! Let us try again!",
        "Almost! You are getting better!",
        "Keep going! You can do it!",
        "Every try makes you stronger!"
    };

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
    }

    void Start()
    {
        // Create AudioSource at runtime if not assigned in Inspector
        if (sfxAudio == null)
            sfxAudio = gameObject.AddComponent<AudioSource>();

        // Hide Lumi at start — will appear after panel animates in
        if (lumiMale != null) lumiMale.SetActive(false);
        if (lumiFemale != null) lumiFemale.SetActive(false);

        if (lostPanel != null)
            lostPanel.transform.localScale = Vector3.zero;

        StartCoroutine(ShowLostResults());
    }

    IEnumerator ShowLostResults()
    {
        yield return new WaitForSeconds(0.3f);

        // Animate panel in
        lostPanel.transform
            .DOScale(1f, 0.4f)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                // Show correct Lumi gender — default to male if GameManager missing
                bool isBoy = GameManager.Instance == null ||
                             GameManager.Instance.lumiGender == 0;
                if (isBoy) { if (lumiMale != null) lumiMale.SetActive(true); }
                else { if (lumiFemale != null) lumiFemale.SetActive(true); }

                // Play SFX immediately when panel finishes animating in
                // (not after stars — that felt very late)
                if (sfxAudio != null && failSfx != null)
                    sfxAudio.PlayOneShot(failSfx);
            });

        yield return new WaitForSeconds(0.5f);

        // Show base coins earned — always 10
        if (coinsEarnedText != null)
        {
            coinsEarnedText.text = "+10";
            coinsEarnedText.transform
                .DOScale(1.1f, 0.3f)
                .SetLoops(2, LoopType.Yoyo);
        }

        yield return new WaitForSeconds(0.3f);

        // All stars grey
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            stars[i].sprite = starEmpty;
            stars[i].color = new Color(0.71f, 0.70f, 0.66f, 1f);
            stars[i].transform
                .DOScale(0.85f, 0.3f)
                .SetLoops(2, LoopType.Yoyo);
            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(0.2f);

        // Random comfort message
        if (messageText != null)
            messageText.text = comfortMessages[
                Random.Range(0, comfortMessages.Length)];
    }

    public void OnTryAgainPressed()
    {
        DOTween.KillAll();
        if (GameManager.Instance == null)
        {
            SceneManager.LoadScene("MG1Scene");
            return;
        }

        int game = GameManager.Instance.currentGame;
        string sceneName = game == 1 ? "MG1Scene" : "MG2Scene(2)";
        SceneManager.LoadScene(sceneName);
    }

    public void OnBackPressed()
    {
        DOTween.KillAll();
        if (GameManager.Instance == null)
        {
            SceneManager.LoadScene("HubScene");
            return;
        }

        int game = GameManager.Instance.currentGame;
        string sceneName = game == 1 ? "LevelSelectMG1" : "LevelSelectMG2";
        SceneManager.LoadScene(sceneName);
    }
}