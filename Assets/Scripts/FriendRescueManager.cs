using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;

public class FriendRescueManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI friendNameText;
    public Image friendImage;
    public GameObject celebrationPanel;

    [Header("Friend Data")]
    public Sprite[] friendSprites;
    public string[] friendNames = {
        "Beri", "Pip", "Zara", "Finn"
    };
    public string[] friendMessages = {
        "Beri has joined Sunvale!",
        "Pip has joined Sunvale!",
        "Zara has joined Sunvale!",
        "Finn has joined Sunvale!"
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
        celebrationPanel.transform.localScale = Vector3.zero;
        StartCoroutine(PlayRescueSequence());
    }

    IEnumerator PlayRescueSequence()
    {
        yield return new WaitForSeconds(0.5f);

        // Get which friend was rescued
        int friendIndex = 0;
        if (GameManager.Instance != null)
        {
            int level = GameManager.Instance.currentLevel;
            friendIndex = Mathf.Clamp(level, 0, 3);
        }

        // Set friend name
        friendNameText.text = friendMessages[friendIndex];

        // Assign friend sprite
        if (friendIndex < friendSprites.Length &&
            friendSprites[friendIndex] != null)
            friendImage.sprite = friendSprites[friendIndex];

        // Start as shadow
        friendImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // Show panel
        celebrationPanel.SetActive(true);
        celebrationPanel.transform
            .DOScale(1f, 0.4f)
            .SetEase(Ease.OutBack);

        yield return new WaitForSeconds(1f);

        // Animate friend from shadow to color
        friendImage.transform.DOScale(1.1f, 0.3f)
            .SetLoops(2, LoopType.Yoyo);

        friendImage.DOColor(Color.white, 1f)
            .SetEase(Ease.OutQuad);

        yield return new WaitForSeconds(1f);

        // Bounce friend image
        friendImage.transform
            .DOPunchScale(Vector3.one * 0.2f, 0.5f, 5);

        // Flash title
        titleText.transform
            .DOPunchScale(Vector3.one * 0.15f, 0.4f, 4);
    }

    public void OnContinuePressed()
    {
        if (GameManager.Instance == null)
        {
            SceneManager.LoadScene("HubScene");
            return;
        }

        string scene = GameManager.Instance.currentGame == 1
            ? "LevelSelectMG1"
            : "LevelSelectMG2";

        SceneManager.LoadScene(scene);
    }
}