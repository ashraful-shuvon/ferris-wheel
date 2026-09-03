using UnityEngine;
using TMPro;

public class TodaysWinTracker : MonoBehaviour
{
    [Header("UI - Coins")]
    public TMP_Text todaysWinText;
    public string prefix = "Today's Win  ";

    [Header("UI - Win Count")]
    [Tooltip("Text component to show HOW MANY TIMES the player won today.")]
    public TMP_Text todaysWinCountText;

    // PlayerPrefs keys
    const string KeyAmount    = "TodaysWin_Amount";
    const string KeyCount     = "TodaysWin_Count"; 
    const string KeyTimestamp = "TodaysWin_Timestamp";

    long todaysWin = 0;
    int todaysWinCount = 0; 

    public long TodaysWin => todaysWin;

    void Awake()
    {
        LoadAndCheckReset();
        RefreshUI();
    }

    public void AddWin(long amount)
    {
        if (amount <= 0) return;

        LoadAndCheckReset();
        
        todaysWin += amount;
        todaysWinCount++; 
        
        Save();
        RefreshUI();
    }

    public void SetServerValue(long amount)
    {
        todaysWin = amount;
        RefreshUI();
    }

    // --- NEW: Manually increase the win count (used for Server Mode) ---
    public void IncrementWinCount()
    {
        LoadAndCheckReset();
        todaysWinCount++; 
        Save();
        RefreshUI();
    }

    void LoadAndCheckReset()
    {
        string savedTime = PlayerPrefs.GetString(KeyTimestamp, "");

        if (!string.IsNullOrEmpty(savedTime) &&
            System.DateTime.TryParse(savedTime, out System.DateTime lastSave))
        {
            System.DateTime now   = System.DateTime.Now;
            System.DateTime saved = lastSave;

            bool sameDay = now.Year  == saved.Year &&
                           now.Month == saved.Month &&
                           now.Day   == saved.Day;

            if (!sameDay)
            {
                todaysWin = 0;
                todaysWinCount = 0;
                Save();
                return;
            }
        }
        else
        {
            todaysWin = 0;
            todaysWinCount = 0;
            Save();
            return;
        }

        todaysWin = (long)PlayerPrefs.GetFloat(KeyAmount, 0f);
        todaysWinCount = PlayerPrefs.GetInt(KeyCount, 0); 
    }

    void Save()
    {
        PlayerPrefs.SetFloat(KeyTimestamp, 0); 
        PlayerPrefs.SetFloat(KeyAmount, todaysWin);
        PlayerPrefs.SetInt(KeyCount, todaysWinCount); 
        PlayerPrefs.SetString(KeyTimestamp, System.DateTime.Now.ToString("o"));
        PlayerPrefs.Save();
    }

    void RefreshUI()
    {
        if (todaysWinText != null)
            todaysWinText.text = prefix + FormatCoins(todaysWin);
            
        if (todaysWinCountText != null)
            todaysWinCountText.text = todaysWinCount.ToString();
    }

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000)     return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}