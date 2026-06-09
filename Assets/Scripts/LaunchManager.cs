using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// LaunchManager — full flow:
///
///   Scene opens
///       ↓
///   getStartedBtn + welcomeTitle visible (your existing opening screen)
///       ↓
///   Child presses Get Started
///       ↓
///   getStartedBtn hides → WelcomeScreen pops in
///   WelcomeScreen has two buttons: "New Child" and "I'm Back!"
///       ↓                                ↓
///   NewChildPanel                 ReturningChildPanel
///   (name + gender + Start)       (name input + "Let's Play!")
///       ↓                                ↓
///   Creates fresh profile         Loads existing profile
///       ↓                                ↓
///                      HubScene
///
/// ─── Unity hierarchy ─────────────────────────────────────────────────────
///
///   Canvas
///   ├── WelcomeTitle          (your existing title — keep as-is)
///   ├── GetStartedBtn         (your existing button — keep as-is)
///   ├── WelcomeScreen         (NEW — replaces the old welcomePanel)
///   │     ├── NewChildBtn       button → "New Child"
///   │     └── ImBackBtn         button → "I'm Back!"
///   ├── NewChildPanel         (your old welcomePanel renamed)
///   │     ├── NameInput
///   │     ├── BoyBtn
///   │     ├── GirlBtn
///   │     ├── BoyIndicator
///   │     ├── GirlIndicator
///   │     ├── ValidationText    (NEW — empty TMP text, starts hidden)
///   │     ├── StartBtn
///   │     └── BackBtn           (optional)
///   └── ReturningChildPanel   (NEW panel)
///         ├── TitleText         "What's your name?"
///         ├── NameInput
///         ├── LetsPlayBtn
///         ├── FeedbackText      empty TMP — shows welcome or error
///         └── BackBtn
///
/// ─── Inspector wiring ────────────────────────────────────────────────────
/// All panels start INACTIVE in the Inspector.
/// Start() activates only GetStartedBtn and WelcomeTitle.
/// ShowScreen() handles all panel switching with a pop animation.
/// </summary>
public class LaunchManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    int _selectedGender = 0;
    bool _genderSelected = false;

    static readonly Color _green = new Color(0.13f, 0.60f, 0.13f);
    static readonly Color _red = new Color(0.80f, 0.15f, 0.15f);

    // ════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (HeartManager.Instance == null)
            new GameObject("HeartManager").AddComponent<HeartManager>();
    }

    void Start()
    {
        // Only the opening screen is visible at startup.
        // Everything else is hidden until the child interacts.
        if (welcomeScreen != null) welcomeScreen.SetActive(false);
        if (newChildPanel != null) newChildPanel.SetActive(false);
        if (returningChildPanel != null) returningChildPanel.SetActive(false);

        // Clear all message texts
        SetValidation("");
        SetFeedback("", _green);

        // Gender buttons start neutral
        SetIndicator(boyIndicator, boyBtn, false);
        SetIndicator(girlIndicator, girlBtn, false);
    }

    // ════════════════════════════════════════════════════════════════════════
    // OPENING SCREEN — GET STARTED
    // Wire: GetStartedBtn → OnGetStartedPressed
    // ════════════════════════════════════════════════════════════════════════

    // Called by the existing Get Started button.
    // Hides the opening screen and shows the welcome screen with two choices.
    public void OnGetStartedPressed()
    {
        // Hide the opening screen elements
        if (getStartedBtn != null) getStartedBtn.SetActive(false);
        if (welcomeTitle != null) welcomeTitle.SetActive(false);

        // Show the welcome screen (New Child / I'm Back)
        ShowPanel(welcomeScreen);
    }

    // ════════════════════════════════════════════════════════════════════════
    // WELCOME SCREEN — TWO CHOICES
    // Wire: NewChildBtn → OnNewChildPressed
    //       ImBackBtn   → OnImBackPressed
    // ════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════
    // NEW CHILD PANEL
    // Wire: BoyBtn   → OnBoySelected
    //       GirlBtn  → OnGirlSelected
    //       StartBtn → OnStartNewChildPressed
    //       BackBtn  → OnBackToWelcome
    // ════════════════════════════════════════════════════════════════════════

    public void OnBoySelected()
    {
        _selectedGender = 0;
        _genderSelected = true;
        SetIndicator(boyIndicator, boyBtn, true);
        SetIndicator(girlIndicator, girlBtn, false);
        SetValidation(""); // clear message the moment they make a choice
    }

    public void OnGirlSelected()
    {
        _selectedGender = 1;
        _genderSelected = true;
        SetIndicator(girlIndicator, girlBtn, true);
        SetIndicator(boyIndicator, boyBtn, false);
        SetValidation(""); // clear message the moment they make a choice
    }

    public void OnStartNewChildPressed()
    {
        string name = newNameInput != null ? newNameInput.text.Trim() : "";

        // Name is empty
        if (string.IsNullOrEmpty(name))
        {
            newNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            SetValidation("Please tell us your name first!");
            return;
        }

        // Gender not chosen yet
        if (!_genderSelected)
        {
            boyBtn?.transform.DOShakePosition(0.3f, 6f, 12);
            girlBtn?.transform.DOShakePosition(0.3f, 6f, 12);
            SetValidation("Choose a character first!");
            return;
        }

        // Everything valid — clear message and go
        SetValidation("");
        SetIndicator(boyIndicator, boyBtn, false);
        SetIndicator(girlIndicator, girlBtn, false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.lumiName = name;
            GameManager.Instance.lumiGender = _selectedGender;
            // ResetCurrentPlayer resets progress to defaults for a new child
            // and calls SaveData() which registers their name automatically.
            GameManager.Instance.ResetCurrentPlayer();
        }

        // Register this child in Firebase so the dashboard can find them.
        // "boy" or "girl" is stored so the dashboard can show the right avatar.
        string genderLabel = _selectedGender == 0 ? "boy" : "girl";
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.RegisterPlayer(name, genderLabel);

        newChildPanel.transform
            .DOScale(0f, 0.3f)
            .OnComplete(() => SceneManager.LoadScene("HubScene"));
    }

    // Back from NewChildPanel → go back to WelcomeScreen (not all the way to GetStarted)
    public void OnBackToWelcome()
    {
        SetValidation("");
        ShowPanel(welcomeScreen);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RETURNING CHILD PANEL
    // Wire: LetsPlayBtn → OnReturningChildLoginPressed
    //       BackBtn     → OnBackToWelcomeFromReturning
    // ════════════════════════════════════════════════════════════════════════

    public void OnReturningChildLoginPressed()
    {
        string name = returningNameInput != null
            ? returningNameInput.text.Trim()
            : "";

        // Name field is empty
        if (string.IsNullOrEmpty(name))
        {
            returningNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            SetFeedback("Please type your name!", _red);
            return;
        }

        // Check if this name exists on this device
        bool exists = GameManager.GetRegisteredPlayers().Contains(name);

        if (!exists)
        {
            // Name not found — friendly message, no technical language
            SetFeedback(
                "Hmm, we couldn't find that name.\nCheck the spelling or ask your therapist!",
                _red);
            returningNameInput?.transform.DOShakePosition(0.3f, 8f, 15);
            return;
        }

        // Name found — show warm welcome message
        SetFeedback("Welcome back, " + name + "! \u2b50", _green);

        // Load their saved gender
        string genderKey = "LumiGender_" + name.Replace(" ", "_");
        int gender = PlayerPrefs.GetInt(genderKey, 0);

        // Set player and load all their saved progress
        if (GameManager.Instance != null)
        {
            GameManager.Instance.lumiName = name;
            GameManager.Instance.lumiGender = gender;
            GameManager.Instance.LoadData();
        }

        // Small delay so child can read "Welcome back!" then go to hub
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

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    // Shows one panel with a pop-in animation.
    // Hides all other panels first.
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
        // Only the indicator border animates — not the button itself.
        // This prevents the button from becoming transparent on deselect.
        if (indicator != null)
            indicator.DOColor(
                selected ? selectedBorderColor : normalBorderColor,
                0.2f);

        // Button gets a subtle scale to show selection — no color change
        if (btn != null)
            btn.transform.DOScale(selected ? 1.08f : 1f, 0.2f).SetEase(Ease.OutBack);
    }
    // Validation message on NewChildPanel (always red)
    void SetValidation(string message)
    {
        if (newChildValidationText == null) return;
        newChildValidationText.text = message;
        newChildValidationText.color = _red;
    }

    // Feedback message on ReturningChildPanel (green or red depending on outcome)
    void SetFeedback(string message, Color color)
    {
        if (returningFeedbackText == null) return;
        returningFeedbackText.text = message;
        returningFeedbackText.color = color;
    }
}