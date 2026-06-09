using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class RoundManagerMG2 : MonoBehaviour
{
    [Header("Level Settings")]
    public int currentLevel = 0;

    [Header("Avatar")]
    public Animator emotionAnimator;

    [Header("Aura")]
    public Image auraImage;

    [Header("UI References")]
    public GameObject watchPrompt;
    public GameObject questionPrompt;
    public GameObject answerContainer;
    public GameObject lumiCorner;
    public GameObject speechBubble;
    public TextMeshProUGUI speechText;

    [Header("HUD")]
    public Image[] heartImages;
    public Sprite heartFull;
    public Sprite heartEmpty;
    public TextMeshProUGUI coinText;

    [Header("Emoji Sprites")]
    public Sprite[] emojiSprites;

    [Header("Answer Button Prefab")]
    public GameObject answerButtonPrefab;

    private string[] emotionNames = {
        "Happy", "Sad", "Angry", "Surprised",
        "Nervous", "Fearful", "Proud", "Bored", "Calm"
    };

    private string[] animTriggers = {
        "HappyTrigger", "SadTrigger", "AngryTrigger",
        "SurprisedTrigger", "NervousTrigger",
        "FearfulTrigger", "ProudTrigger",
        "BoredTrigger", "CalmTrigger"
    };

    private Color[] auraColors = {
        new Color(1f, 0.85f, 0f),
        new Color(0.2f, 0.4f, 1f),
        new Color(1f, 0.3f, 0f),
        new Color(1f, 1f, 1f),
        new Color(0.6f, 0.8f, 0.2f),
        new Color(0.5f, 0f, 0.8f),
        new Color(1f, 0.8f, 0f),
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.4f, 0.9f, 0.8f)
    };

    private float[] auraAlphas =
        { 0.8f, 0.55f, 0.30f, 0.10f };

    private int[][] levelEmotions = {
        new int[] { 0, 1 },
        new int[] { 2, 3, 4 },
        new int[] { 5, 6 },
        new int[] { 7, 8 }
    };

    private int[] roundsPerLevel = { 2, 3, 3, 3 };
    private int[] optionsPerLevel = { 2, 4, 3, 3 };

    private int currentRound = 0;
    private int totalRounds = 3;
    private int passRequired = 2;
    private int roundsCorrect = 0;
    private int attemptCount = 0;
    private int currentEmotionIndex = 0;
    private bool roundActive = false;
    private List<int> usedEmotionsThisLevel =
        new List<int>();

    void Start()
    {
        if (GameManager.Instance != null)
            currentLevel = GameManager.Instance
                .currentLevel;

        totalRounds = roundsPerLevel[currentLevel];
        passRequired = 2;

        if (auraImage != null)
        {
            auraImage.type = Image.Type.Filled;
            auraImage.fillMethod =
                Image.FillMethod.Radial360;
            auraImage.fillAmount = 1f;
            auraImage.color = new Color(0, 0, 0, 0);
            auraImage.raycastTarget = false;
        }

        if (lumiCorner != null)
            lumiCorner.SetActive(false);
        if (watchPrompt != null)
            watchPrompt.SetActive(false);
        if (questionPrompt != null)
            questionPrompt.SetActive(false);
        if (answerContainer != null)
            answerContainer.SetActive(false);
        if (speechBubble != null)
            speechBubble.SetActive(false);

        UpdateHUD();
        StartCoroutine(StartTutorial());
    }

    void UpdateHUD()
    {
        int hearts = HeartManager.Instance != null ?
            HeartManager.Instance.GetHearts(2) : 3;
        for (int i = 0; i < heartImages.Length; i++)
            heartImages[i].sprite = i < hearts ?
                heartFull : heartEmpty;

        if (coinText != null &&
            GameManager.Instance != null)
            coinText.text = GameManager.Instance
                .tempCoins.ToString();
    }

    IEnumerator StartTutorial()
    {
        yield return new WaitForSeconds(0.5f);
        ShowLumi("Watch the character and " +
            "guess how they feel!");
        yield return new WaitForSeconds(3f);
        HideLumi();
        yield return new WaitForSeconds(0.5f);
        StartRound();
    }

    void StartRound()
    {
        attemptCount = 0;
        roundActive = false;

        int[] pool = levelEmotions[currentLevel];
        List<int> available = new List<int>(pool);
        foreach (int used in usedEmotionsThisLevel)
            available.Remove(used);

        if (available.Count == 0)
        {
            usedEmotionsThisLevel.Clear();
            available = new List<int>(pool);
        }

        currentEmotionIndex =
            available[Random.Range(0, available.Count)];
        usedEmotionsThisLevel.Add(currentEmotionIndex);

        StartCoroutine(PlayEmotionSequence());
    }

    IEnumerator PlayEmotionSequence()
    {
        if (auraImage != null)
            auraImage.color = new Color(0, 0, 0, 0);

        if (answerContainer != null)
            answerContainer.SetActive(false);
        if (watchPrompt != null)
            watchPrompt.SetActive(true);
        if (questionPrompt != null)
            questionPrompt.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        Color auraColor =
            auraColors[currentEmotionIndex];
        auraColor.a = auraAlphas[currentLevel];
        if (auraImage != null)
            auraImage.DOColor(auraColor, 0.5f);

        if (emotionAnimator != null)
        {
            emotionAnimator.SetTrigger(
                animTriggers[currentEmotionIndex]);
            Debug.Log("Playing: " +
                emotionNames[currentEmotionIndex]);
        }

        yield return new WaitForSeconds(4f);

        if (watchPrompt != null)
            watchPrompt.SetActive(false);
        if (questionPrompt != null)
            questionPrompt.SetActive(true);

        ShowAnswerOptions();
        roundActive = true;
    }
    void ShowAnswerOptions()
    {
        if (answerContainer == null ||
            answerButtonPrefab == null) return;

        foreach (Transform child in
            answerContainer.transform)
            Destroy(child.gameObject);

        answerContainer.SetActive(true);
        List<int> options = BuildOptions();

        for (int i = 0; i < options.Count; i++)
        {
            int emotionIdx = options[i];
            GameObject btn = Instantiate(
                answerButtonPrefab,
                answerContainer.transform);

            if (currentLevel <= 1)
            {
                Image[] images =
                    btn.GetComponentsInChildren<Image>();
                foreach (Image img in images)
                {
                    if (img.gameObject != btn.gameObject)
                    {
                        if (emotionIdx < emojiSprites.Length
                            && emojiSprites[emotionIdx] != null)
                        {
                            img.sprite =
                                emojiSprites[emotionIdx];
                            img.preserveAspect = true;
                        }
                        break;
                    }
                }
            }
            else
            {
                TextMeshProUGUI btnText =
                    btn.GetComponentInChildren
                    <TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = currentLevel == 2 ?
                        emotionNames[emotionIdx] : "";
            }

            int captured = emotionIdx;
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null)
                btnComp.onClick.AddListener(
                    () => OnAnswerSelected(captured));

            btn.transform.localScale = Vector3.zero;
            btn.transform.DOScale(1f, 0.2f)
                .SetDelay(i * 0.1f);
        }
    }

    List<int> BuildOptions()
    {
        List<int> options = new List<int>();
        options.Add(currentEmotionIndex);

        int[] pool = levelEmotions[currentLevel];
        List<int> distractorPool = new List<int>(pool);
        distractorPool.Remove(currentEmotionIndex);

        if (currentLevel >= 2)
        {
            for (int i = 0; i < currentLevel; i++)
            {
                foreach (int e in levelEmotions[i])
                {
                    if (!distractorPool.Contains(e) &&
                        e != currentEmotionIndex)
                        distractorPool.Add(e);
                }
            }
        }

        int needed = optionsPerLevel[currentLevel];
        while (options.Count < needed &&
               distractorPool.Count > 0)
        {
            int pick = Random.Range(0, distractorPool.Count);
            options.Add(distractorPool[pick]);
            distractorPool.RemoveAt(pick);
        }

        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = options[i];
            options[i] = options[j];
            options[j] = temp;
        }

        return options;
    }

    void OnAnswerSelected(int emotionIdx)
    {
        if (!roundActive) return;

        if (emotionIdx == currentEmotionIndex)
        {
            roundActive = false;
            roundsCorrect++;

            GameManager.Instance?.AddTempCoins(
                attemptCount == 0 ? 20 : 12);
            UpdateHUD();

            if (auraImage != null)
            {
                Color bright =
                    auraColors[currentEmotionIndex];
                bright.a = 0.9f;
                auraImage.DOColor(bright, 0.2f)
                    .SetLoops(2, LoopType.Yoyo);
            }

            ShowLumi("Amazing! You got it!");
            Invoke(nameof(NextRound), 2f);
        }
        else
        {
            attemptCount++;
            HandleWrongAnswer();
        }
    }

    void HandleWrongAnswer()
    {
        if (HeartManager.Instance != null &&
            HeartManager.Instance.GetHearts(2) <= 0)
        {
            roundActive = false;
            StartCoroutine(RedirectToWait());
            return;
        }

        if (attemptCount == 1)
            ShowLumi("Try again! Look closely.");
        else if (attemptCount == 2)
            StartCoroutine(ReplayHint());
        else
        {
            roundActive = false;
            StartCoroutine(RevealAndContinue());
        }
    }

    IEnumerator ReplayHint()
    {
        ShowLumi("Let me show you again!");

        if (answerContainer != null)
            answerContainer.SetActive(false);
        if (watchPrompt != null)
            watchPrompt.SetActive(true);
        if (questionPrompt != null)
            questionPrompt.SetActive(false);

        if (emotionAnimator != null)
            emotionAnimator.SetTrigger(
                animTriggers[currentEmotionIndex]);

        if (auraImage != null)
        {
            Color bright =
                auraColors[currentEmotionIndex];
            bright.a = 0.7f;
            auraImage.DOColor(bright, 0.3f);
        }

        yield return new WaitForSeconds(4f);

        HideLumi();
        if (watchPrompt != null)
            watchPrompt.SetActive(false);
        if (answerContainer != null)
            answerContainer.SetActive(true);

        if (auraImage != null)
        {
            Color restored =
                auraColors[currentEmotionIndex];
            restored.a = auraAlphas[currentLevel];
            auraImage.DOColor(restored, 0.3f);
        }
    }

    IEnumerator RevealAndContinue()
    {
        foreach (Transform child in
            answerContainer.transform)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        if (auraImage != null)
        {
            Color full = auraColors[currentEmotionIndex];
            full.a = 0.8f;
            auraImage.DOColor(full, 0.5f);
        }

        ShowLumi("This is " +
            emotionNames[currentEmotionIndex] + "!");

        yield return new WaitForSeconds(2.5f);

        HideLumi();
        HeartManager.Instance?.LoseHeart(2);
        UpdateHUD();

        if (HeartManager.Instance != null &&
            HeartManager.Instance.GetHearts(2) <= 0)
        {
            yield return new WaitForSeconds(0.5f);
            SceneManager.LoadScene("WaitScene");
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
        NextRound();
    }

    IEnumerator RedirectToWait()
    {
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("WaitScene");
    }

    void NextRound()
    {
        currentRound++;
        HideLumi();

        if (answerContainer != null)
            answerContainer.SetActive(false);
        if (questionPrompt != null)
            questionPrompt.SetActive(false);

        if (auraImage != null)
            auraImage.DOColor(
                new Color(0, 0, 0, 0), 0.5f);

        if (currentRound >= totalRounds)
            StartCoroutine(EndLevel());
        else
            Invoke(nameof(StartRound), 1f);
    }

    IEnumerator EndLevel()
    {
        usedEmotionsThisLevel.Clear();
        yield return new WaitForSeconds(0.5f);

        bool passed = roundsCorrect >= passRequired;

        if (passed)
        {
            int stars = roundsCorrect == totalRounds ?
                3 : roundsCorrect == 2 ? 2 : 1;

            if (GameManager.Instance != null)
            {
                if (stars > GameManager.Instance
                        .mg2Stars[currentLevel])
                    GameManager.Instance
                        .mg2Stars[currentLevel] = stars;
                GameManager.Instance.CommitCoins();
                GameManager.Instance
                    .UnlockNextLevel(2, currentLevel);
            }

            ShowLumi("Wonderful job!");
            yield return new WaitForSeconds(1.5f);
            SceneManager.LoadScene("LevelCompleteScene");
        }
        else
        {
            GameManager.Instance?.DiscardCoins();
            HeartManager.Instance?.LoseHeart(2);
            UpdateHUD();

            ShowLumi("We will try again!");
            yield return new WaitForSeconds(1.5f);

            if (HeartManager.Instance != null &&
                HeartManager.Instance.GetHearts(2) <= 0)
                SceneManager.LoadScene("WaitScene");
            else
                SceneManager.LoadScene("LevelSelectMG2");
        }
    }

    public void ShowLumi(string message)
    {
        if (lumiCorner == null) return;
        lumiCorner.SetActive(true);
        if (speechBubble != null)
            speechBubble.SetActive(true);
        if (speechText != null)
            speechText.text = message;

        if (speechBubble != null)
        {
            speechBubble.transform.localScale =
                Vector3.zero;
            speechBubble.transform
                .DOScale(1f, 0.2f)
                .SetEase(Ease.OutBack);
        }
    }

    public void HideLumi()
    {
        if (speechBubble != null)
        {
            speechBubble.transform
                .DOScale(0f, 0.15f)
                .OnComplete(() =>
                {
                    if (speechBubble != null)
                        speechBubble.SetActive(false);
                    if (lumiCorner != null)
                        lumiCorner.SetActive(false);
                });
        }
        else if (lumiCorner != null)
        {
            lumiCorner.SetActive(false);
        }
    }
}