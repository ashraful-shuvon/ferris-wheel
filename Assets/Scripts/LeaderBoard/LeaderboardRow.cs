using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRow : MonoBehaviour
{
    public Text rankText;
    public Text usernameText;
    public Text winAmountText;
    public Text todaysWinText;

    [Header("Visual Customization")]
    public Image backgroundImage;
    public Sprite firstPlaceBackground;
    public Sprite secondPlaceBackground;
    public Sprite thirdPlaceBackground;
    public Sprite normalBackground;

    [Header("Visual Customization - Fallback Colors")]
    public Color firstPlaceColor = new Color(1f, 0.84f, 0f, 1f);       // Gold
    public Color secondPlaceColor = new Color(0.75f, 0.75f, 0.75f, 1f); // Silver
    public Color thirdPlaceColor = new Color(0.8f, 0.5f, 0.2f, 1f);     // Bronze
    public Color normalColor = new Color(1f, 1f, 1f, 0.1f);             // Default transparent tint

    void Awake()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }

    public void SetData(int rank, string username, long wonAmount, long todaysWin = 0)
    {
        rankText.text      = rank.ToString();
        usernameText.text  = username;
        winAmountText.text = FormatCoins(wonAmount);
        if (todaysWinText != null)
        {
            todaysWinText.text = FormatCoins(todaysWin);
        }

        if (backgroundImage != null)
        {
            if (rank == 1)
            {
                if (firstPlaceBackground != null)
                {
                    backgroundImage.sprite = firstPlaceBackground;
                    backgroundImage.color  = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color  = firstPlaceColor;
                }
            }
            else if (rank == 2)
            {
                if (secondPlaceBackground != null)
                {
                    backgroundImage.sprite = secondPlaceBackground;
                    backgroundImage.color  = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color  = secondPlaceColor;
                }
            }
            else if (rank == 3)
            {
                if (thirdPlaceBackground != null)
                {
                    backgroundImage.sprite = thirdPlaceBackground;
                    backgroundImage.color  = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color  = thirdPlaceColor;
                }
            }
            else
            {
                if (normalBackground != null)
                {
                    backgroundImage.sprite = normalBackground;
                    backgroundImage.color  = Color.white;
                }
                else
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color  = normalColor;
                }
            }
        }
    }

    public void Clear()
    {
        rankText.text      = "";
        usernameText.text  = "";
        winAmountText.text = "";
        if (todaysWinText != null)
        {
            todaysWinText.text = "";
        }
    }

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000)     return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}
