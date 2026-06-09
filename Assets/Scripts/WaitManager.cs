using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using DG.Tweening;

public class WaitManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI heartsText;
    public TextMeshProUGUI titleText;
    public Image[] heartIcons;
    public Sprite heartFull;
    public Sprite heartEmpty;

    private int currentGame = 1;
    private float restoreMinutes = 2f;

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
        if (GameManager.Instance != null)
            currentGame = GameManager.Instance.currentGame;

        UpdateHeartDisplay();
        UpdateTitle();
    }

    void Update()
    {
        UpdateTimer();
    }

    void UpdateTitle()
    {
        if (titleText == null) return;
        string gameName = currentGame == 1 ?
            "Memory Garden" : "Feelings Forest";
        titleText.text = "Out of Hearts!\n" + gameName;
    }

    void UpdateHeartDisplay()
    {
        if (HeartManager.Instance == null) return;

        int hearts = HeartManager.Instance
            .GetHearts(currentGame);

        for (int i = 0; i < heartIcons.Length; i++)
        {
            heartIcons[i].sprite = i < hearts ?
                heartFull : heartEmpty;
        }
    }

    void UpdateTimer()
    {
        if (HeartManager.Instance == null) return;

        float minutesLeft = HeartManager.Instance
            .GetMinutesUntilNextHeart(currentGame);

        if (minutesLeft <= 0)
        {
            // Heart restored
            HeartManager.Instance.CheckHeartRestore();
            UpdateHeartDisplay();

            int hearts = HeartManager.Instance
                .GetHearts(currentGame);

            if (hearts > 0)
            {
                if (timerText != null)
                {
                    timerText.text = "Ready!";
                    timerText.alignment =
                        TextAlignmentOptions.Center;
                }
                if (heartsText != null)
                {
                    heartsText.text = "Hearts restored!";
                    heartsText.alignment =
                        TextAlignmentOptions.Center;
                }
                Invoke(nameof(GoToLevelSelect), 1.5f);
                return;
            }

            minutesLeft = restoreMinutes;
        }

        // Format as MM:SS
        int minutes = Mathf.FloorToInt(minutesLeft);
        int seconds = Mathf.FloorToInt(
            (minutesLeft - minutes) * 60f);

        if (timerText != null)
            timerText.text = string.Format(
                "{0:00}:{1:00}", minutes, seconds);

        if (heartsText != null)
            heartsText.text = "Next heart in";
    }

    void GoToLevelSelect()
    {
        string sceneName = currentGame == 1 ?
            "LevelSelectMG1" : "LevelSelectMG2";
        SceneManager.LoadScene(sceneName);
    }

    public void OnBackPressed()
    {
        SceneManager.LoadScene("HubScene");
    }
}