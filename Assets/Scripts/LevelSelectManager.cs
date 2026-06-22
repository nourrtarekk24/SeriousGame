using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class LevelSelectManager : MonoBehaviour
{
    public Button[] levelButtons;
    public GameObject[] lockOverlays;
    public Image[] level1Stars;
    public Image[] level2Stars;
    public Image[] level3Stars;
    public Image[] level4Stars;
    public Image[] level5Stars;
    public Image[] level6Stars;

    [Header("Star Sprites")]
    public Sprite starFilled;
    public Sprite starEmpty;

    [Header("Game — 1 for MG1, 2 for MG2")]
    public int gameNumber = 1;

    [Header("Coin Display")]
    public TextMeshProUGUI coinText;

    [Header("MG2 Extra Level Labels")]
    public GameObject extraLabel_L5;
    public GameObject extraLabel_L6;

    [Header("Adaptive Difficulty Toggle (MG1 only)")]
    [Tooltip("Button that toggles adaptive vs fixed difficulty. MG1 only.")]
    public Button adaptiveToggleBtn;
    [Tooltip("TextMeshPro label inside the toggle button.")]
    public TextMeshProUGUI adaptiveToggleLabel;

    [Header("Adaptive Info Popup")]
    [Tooltip("Small '?' button that shows the info panel when pressed.")]
    public Button adaptiveInfoBtn;
    [Tooltip("The info panel GameObject — hidden by default, shown on '?' press.")]
    public GameObject adaptiveInfoPanel;
    [Tooltip("Close button inside the info panel.")]
    public Button adaptiveInfoCloseBtn;
    [Tooltip("Full-screen dark overlay shown behind the info panel. Set alpha to ~0.6 in Inspector.")]
    public GameObject adaptiveInfoOverlay;

    private bool infoPanelVisible = false;

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
        UpdateAllLevels();
        UpdateCoins();
        UpdateAdaptiveToggle();

        if (adaptiveInfoPanel != null) adaptiveInfoPanel.SetActive(false);
        if (adaptiveInfoOverlay != null) adaptiveInfoOverlay.SetActive(false);

        if (gameNumber == 2)
        {
            if (extraLabel_L5 != null) extraLabel_L5.SetActive(true);
            if (extraLabel_L6 != null) extraLabel_L6.SetActive(true);
        }

        if (gameNumber != 1)
        {
            if (adaptiveToggleBtn != null) adaptiveToggleBtn.gameObject.SetActive(false);
            if (adaptiveInfoBtn != null) adaptiveInfoBtn.gameObject.SetActive(false);
        }
    }

    void UpdateCoins()
    {
        if (coinText != null && GameManager.Instance != null)
            coinText.text = GameManager.Instance.totalCoins.ToString();
    }

    void UpdateAdaptiveToggle()
    {
        if (adaptiveToggleLabel == null) return;
        bool adaptive = GameManager.IsAdaptiveEnabled();
        if (GameManager.IsArabic())
        {
            string ar = adaptive
                ? ArabicSupport.ArabicFixer.Fix("الصعوبة: تكيفية")
                : ArabicSupport.ArabicFixer.Fix("الصعوبة: ثابتة");
            adaptiveToggleLabel.text = ar;
        }
        else
        {
            adaptiveToggleLabel.text = adaptive ? "Difficulty: Adaptive" : "Difficulty: Fixed";
        }
    }

    public void OnAdaptiveTogglePressed()
    {
        bool current = GameManager.IsAdaptiveEnabled();
        GameManager.SetAdaptiveEnabled(!current);

        UpdateAdaptiveToggle();
        UpdateAllLevels();

        if (adaptiveToggleBtn != null)
        {
            adaptiveToggleBtn.transform.DOKill();
            adaptiveToggleBtn.transform.localScale = Vector3.one;
            adaptiveToggleBtn.transform
                .DOScale(1.08f, 0.1f).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                    adaptiveToggleBtn.transform.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
        }

        Debug.Log("[Settings] Adaptive: " + (!current ? "ON" : "OFF"));
    }

    public void OnAdaptiveInfoPressed()
    {
        infoPanelVisible = !infoPanelVisible;
        if (adaptiveInfoPanel == null) return;

        if (infoPanelVisible)
        {

            if (adaptiveInfoOverlay != null)
            {
                adaptiveInfoOverlay.SetActive(true);
                var overlayImg = adaptiveInfoOverlay.GetComponent<Image>();
                if (overlayImg != null)
                {
                    overlayImg.color = new Color(0f, 0f, 0f, 0f);
                    overlayImg.DOColor(new Color(0f, 0f, 0f, 0.6f), 0.2f);
                }
            }

            adaptiveInfoPanel.SetActive(true);
            adaptiveInfoPanel.transform.localScale = Vector3.zero;
            adaptiveInfoPanel.transform
                .DOScale(1f, 0.25f)
                .SetEase(Ease.OutBack);
        }
        else
        {
            CloseInfoPanel();
        }
    }

    public void OnAdaptiveInfoClosePressed() => CloseInfoPanel();

    void CloseInfoPanel()
    {
        infoPanelVisible = false;

        if (adaptiveInfoOverlay != null)
        {
            var overlayImg = adaptiveInfoOverlay.GetComponent<Image>();
            if (overlayImg != null)
                overlayImg.DOColor(new Color(0f, 0f, 0f, 0f), 0.2f)
                    .OnComplete(() => adaptiveInfoOverlay.SetActive(false));
            else
                adaptiveInfoOverlay.SetActive(false);
        }

        if (adaptiveInfoPanel != null)
            adaptiveInfoPanel.transform
                .DOScale(0f, 0.15f)
                .OnComplete(() => adaptiveInfoPanel.SetActive(false));
    }

    void UpdateAllLevels()
    {
        if (GameManager.Instance == null) return;

        bool[] unlocked = gameNumber == 1 ?
            GameManager.Instance.mg1LevelsUnlocked :
            GameManager.Instance.mg2LevelsUnlocked;

        int[] stars = gameNumber == 1 ?
            GameManager.Instance.mg1Stars :
            GameManager.Instance.mg2Stars;

        Image[][] allStars = {
            level1Stars, level2Stars,
            level3Stars, level4Stars,
            level5Stars, level6Stars
        };

        int levelCount = gameNumber == 1 ? 4 : 6;

        for (int i = 0; i < levelCount; i++)
        {
            if (i >= levelButtons.Length || levelButtons[i] == null) break;
            if (i >= unlocked.Length) break;

            if (i < lockOverlays.Length && lockOverlays[i] != null)
                lockOverlays[i].SetActive(!unlocked[i]);

            levelButtons[i].interactable = unlocked[i];

            Image btnImg = levelButtons[i].GetComponent<Image>();
            if (btnImg != null)
            {
                if (!unlocked[i])
                    btnImg.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                else if (i < stars.Length && stars[i] > 0)
                    btnImg.color = new Color(0.69f, 0.80f, 0.61f, 1f);
                else
                    btnImg.color = new Color(0.69f, 0.80f, 0.94f, 1f);
            }

            if (i >= allStars.Length || allStars[i] == null) continue;

            for (int s = 0; s < allStars[i].Length; s++)
            {
                if (allStars[i][s] == null) continue;
                bool filled = i < stars.Length && s < stars[i];
                allStars[i][s].sprite = filled ? starFilled : starEmpty;
                allStars[i][s].color = filled
                    ? new Color(0.96f, 0.89f, 0.63f, 1f)
                    : new Color(0.71f, 0.70f, 0.66f, 1f);
            }
        }
    }

    public void OnLevel1Pressed() { SetLevel(0); LoadGameScene(); }
    public void OnLevel2Pressed() { SetLevel(1); LoadGameScene(); }
    public void OnLevel3Pressed() { SetLevel(2); LoadGameScene(); }
    public void OnLevel4Pressed() { SetLevel(3); LoadGameScene(); }
    public void OnLevel5Pressed() { SetLevel(4); LoadGameScene(); }
    public void OnLevel6Pressed() { SetLevel(5); LoadGameScene(); }

    void SetLevel(int level)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.currentLevel = level;
        GameManager.Instance.currentGame = gameNumber;
    }

    void LoadGameScene()
    {
        string sceneName = gameNumber == 1 ? "MG1Scene" : "MG2Scene(2)";
        SceneManager.LoadScene(sceneName);
    }

    public void OnBackPressed()
    {
        SceneManager.LoadScene("HubScene");
    }
}