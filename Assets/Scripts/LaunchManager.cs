using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class LaunchManager : MonoBehaviour
{

    [Header("── Opening Screen ─────────────────────────────────")]
    [Tooltip("Your existing title object — shown at startup, hidden after Get Started")]
    public GameObject welcomeTitle;
    [Tooltip("Your existing Get Started button")]
    public GameObject getStartedBtn;

    [Header("── Welcome Screen (appears after Get Started) ──────")]
    [Tooltip("Panel with two buttons: New Child and I'm Back. This is your old welcomePanel renamed.")]
    public GameObject welcomeScreen;

    [Header("── New Child Panel ────────────────────────────────")]
    [Tooltip("Panel with name input and gender selection")]
    public GameObject newChildPanel;
    public TMP_InputField newNameInput;
    public Button boyBtn;
    public Button girlBtn;
    public Image boyIndicator;
    public Image girlIndicator;
    [Tooltip("TMP text shown when child tries to start without filling everything in. Starts empty.")]
    public TextMeshProUGUI newChildValidationText;

    [Header("── Returning Child Panel ──────────────────────────")]
    [Tooltip("Panel with a single name input and welcome-back flow")]
    public GameObject returningChildPanel;
    [Tooltip("Name input on the returning child screen")]
    public TMP_InputField returningNameInput;
    [Tooltip("TMP text below the input — shows welcome message or error. Starts empty.")]
    public TextMeshProUGUI returningFeedbackText;

    [Header("── Colors ──────────────────────────────────────────")]
    public Color selectedBorderColor = new Color(0.24f, 0.65f, 0.24f, 1f);
    public Color normalBorderColor = new Color(0f, 0f, 0f, 0f);
    public Color selectedBtnColor = new Color(0.78f, 0.95f, 0.78f, 1f);
    public Color normalBtnColor = new Color(1f, 1f, 1f, 1f);

    int _selectedGender = 0;
    bool _genderSelected = false;

    static readonly Color _green = new Color(0.13f, 0.60f, 0.13f);
    static readonly Color _red = new Color(0.80f, 0.15f, 0.15f);

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (HeartManager.Instance == null)
            new GameObject("HeartManager").AddComponent<HeartManager>();
    }

    void Start()
    {

        if (welcomeScreen != null) welcomeScreen.SetActive(false);
        if (newChildPanel != null) newChildPanel.SetActive(false);
        if (returningChildPanel != null) returningChildPanel.SetActive(false);

        SetValidation("");
        SetFeedback("", _green);

        SetIndicator(boyIndicator, boyBtn, false);
        SetIndicator(girlIndicator, girlBtn, false);
    }

    public void OnGetStartedPressed()
    {

        if (getStartedBtn != null) getStartedBtn.SetActive(false);
        if (welcomeTitle != null) welcomeTitle.SetActive(false);

        ShowPanel(welcomeScreen);
    }

    public void OnNewChildPressed()
    {
        if (newNameInput != null) newNameInput.text = "";
        _genderSelected = false;
        SetIndicator(boyIndicator, boyBtn, false);
        SetIndicator(girlIndicator, girlBtn, false);
        SetValidation("");
        ShowPanel(newChildPanel);
    }

    public void OnImBackPressed()
    {
        if (returningNameInput != null) returningNameInput.text = "";
        SetFeedback("", _green);
        ShowPanel(returningChildPanel);
    }

    public void OnBoySelected()
    {
        _selectedGender = 0;
        _genderSelected = true;
        SetIndicator(boyIndicator, boyBtn, true);
        SetIndicator(girlIndicator, girlBtn, false);
        SetValidation("");
    }

    public void OnGirlSelected()
    {
        _selectedGender = 1;
        _genderSelected = true;
        SetIndicator(girlIndicator, girlBtn, true);
        SetIndicator(boyIndicator, boyBtn, false);
        SetValidation("");
    }

    public void OnStartNewChildPressed()
    {
        string name = newNameInput != null ? newNameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            newNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            SetValidation("Please tell us your name first!");
            return;
        }

        if (!_genderSelected)
        {
            boyBtn?.transform.DOShakePosition(0.3f, 6f, 12);
            girlBtn?.transform.DOShakePosition(0.3f, 6f, 12);
            SetValidation("Choose a character first!");
            return;
        }

        SetValidation("");
        SetIndicator(boyIndicator, boyBtn, false);
        SetIndicator(girlIndicator, girlBtn, false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.lumiName = name;
            GameManager.Instance.lumiGender = _selectedGender;
            GameManager.Instance.ResetCurrentPlayer();
        }

        string registeredAt = System.DateTime.Now.ToString("yyyy-MM-dd");
        PlayerPrefs.SetString("RegisteredAt_" + name.Trim(), registeredAt);
        PlayerPrefs.Save();

        string genderLabel = _selectedGender == 0 ? "boy" : "girl";
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.RegisterPlayer(name, genderLabel);

        newChildPanel.transform
            .DOScale(0f, 0.3f)
            .OnComplete(() => SceneManager.LoadScene("HubScene"));
    }

    public void OnBackToWelcome()
    {
        SetValidation("");
        ShowPanel(welcomeScreen);
    }

    public void OnReturningChildLoginPressed()
    {
        string name = returningNameInput != null
            ? returningNameInput.text.Trim()
            : "";

        if (string.IsNullOrEmpty(name))
        {
            returningNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            SetFeedback("Please type your name!", _red);
            return;
        }

        bool exists = GameManager.GetRegisteredPlayers().Contains(name);

        if (!exists)
        {

            SetFeedback(
                "Hmm, we couldn't find that name.\nCheck the spelling or ask your therapist!",
                _red);
            returningNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            return;
        }

        SetFeedback("Welcome back, " + name + "! \u2b50", _green);

        string genderKey = "LumiGender_" + name.Replace(" ", "_");
        int gender = PlayerPrefs.GetInt(genderKey, 0);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.lumiName = name;
            GameManager.Instance.lumiGender = gender;
            GameManager.Instance.LoadData();
        }

        DOVirtual.DelayedCall(1.2f, () =>
        {
            returningChildPanel.transform
                .DOScale(0f, 0.3f)
                .OnComplete(() => SceneManager.LoadScene("HubScene"));
        });
    }

    public void OnBackToWelcomeFromReturning()
    {
        SetFeedback("", _green);
        ShowPanel(welcomeScreen);
    }

    void ShowPanel(GameObject target)
    {
        if (welcomeScreen != null) welcomeScreen.SetActive(false);
        if (newChildPanel != null) newChildPanel.SetActive(false);
        if (returningChildPanel != null) returningChildPanel.SetActive(false);

        if (target == null) return;
        target.SetActive(true);
        target.transform.localScale = Vector3.zero;
        target.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
    }

    void SetIndicator(Image indicator, Button btn, bool selected)
    {

        if (indicator != null)
            indicator.DOColor(
                selected ? selectedBorderColor : normalBorderColor,
                0.2f);

        if (btn != null)
            btn.transform.DOScale(selected ? 1.08f : 1f, 0.2f).SetEase(Ease.OutBack);
    }

    void SetValidation(string message)
    {
        if (newChildValidationText == null) return;
        newChildValidationText.text = message;
        newChildValidationText.color = _red;
    }

    void SetFeedback(string message, Color color)
    {
        if (returningFeedbackText == null) return;
        returningFeedbackText.text = message;
        returningFeedbackText.color = color;
    }
}