using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class HeartManager : MonoBehaviour
{
    public static HeartManager Instance;
    private int mg1Hearts = 3;
    private int mg2Hearts = 3;
    private const int MAX_HEARTS = 3;
    private const float RESTORE_MINUTES = 2f;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start() { CheckHeartRestore(); }

    public int GetHearts(int game)
    {
        return game == 1 ? mg1Hearts : mg2Hearts;
    }

    public void LoseHeart(int game)
    {
        string key = "Game" + game + "_HeartLostTime_";
        if (game == 1 && mg1Hearts > 0)
        {
            mg1Hearts--;
            // Save timestamp for this heart slot
            PlayerPrefs.SetString(key + mg1Hearts, DateTime.Now.ToString());
            PlayerPrefs.Save();
        }
        else if (game == 2 && mg2Hearts > 0)
        {
            mg2Hearts--;
            PlayerPrefs.SetString(key + mg2Hearts, DateTime.Now.ToString());
            PlayerPrefs.Save();
        }
    }

    public void CheckHeartRestore()
    {
        for (int game = 1; game <= 2; game++)
        {
            string key = "Game" + game + "_HeartLostTime_";
            int currentHearts = game == 1 ? mg1Hearts : mg2Hearts;
            for (int i = currentHearts; i < MAX_HEARTS; i++)
            {
                string savedTime = PlayerPrefs.GetString(key + i, "");
                if (savedTime == "") continue;
                DateTime lostTime = DateTime.Parse(savedTime);
                double minutesPassed = (DateTime.Now - lostTime).TotalMinutes;
                if (minutesPassed >= RESTORE_MINUTES)
                {
                    if (game == 1) mg1Hearts++;
                    else mg2Hearts++;
                    PlayerPrefs.DeleteKey(key + i);
                }
            }
        }
    }

    public float GetMinutesUntilNextHeart(int game)
    {
        int hearts = game == 1 ? mg1Hearts : mg2Hearts;
        if (hearts >= MAX_HEARTS) return 0;
        string key = "Game" + game + "_HeartLostTime_" + hearts;
        string savedTime = PlayerPrefs.GetString(key, "");
        if (savedTime == "") return 0;
        DateTime lostTime = DateTime.Parse(savedTime);
        double minutesPassed = (DateTime.Now - lostTime).TotalMinutes;
        return Mathf.Max(0, RESTORE_MINUTES - (float)minutesPassed);
    }
}
