using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the "My History" table: Bet Time | Bet Details | Reward Details.
/// Bet Details is a yellow pill that grows/shrinks vertically with however many
/// items were bet that round; Bet Time and Reward Details stay vertically
/// centered against whatever height that produces.
/// </summary>
public class GameRecordCard : MonoBehaviour
{
    [Header("Column 1 - Bet Time")]
    public Text dateText;
    public Text roundText;

    [Header("Column 2 - Bet Details")]
    public Image betDetailsBackground;
    public Transform betEntriesContainer;
    public GameObject betEntryTemplate; // first child under betEntriesContainer, used as a stamp

    [Header("Column 3 - Reward Details")]
    public Transform rewardIconsContainer;
    public GameObject rewardIconTemplate;
    public Image rewardCoinIcon;
    public Text rewardCoinText;

    [Header("Shared Icons")]
    public Sprite coinSprite;

    [Header("Layout Tuning")]
    public float column1Width = 200f;
    public float column3Width = 180f;
    public float rowSpacing = 14f;
    public Vector2 rowPadding = new Vector2(18f, 12f); // left/right, top/bottom

    [Header("Colors")]
    public Color textColor = new Color(0.42f, 0.20f, 0.06f, 1f); // dark brown
    public Color dividerColor = new Color(0.85f, 0.55f, 0.25f, 0.5f);

    public void SetData(GameRecord record)
    {
        if (record == null) return;

        if (dateText != null) dateText.text = ExtractDate(record.timestamp);
        if (roundText != null) roundText.text = $"Round:{record.roundNumber}";

        PopulateBetEntries(record.selectedFood);
        PopulateReward(record.winningFruits, record.winCoins);
    }

    static string ExtractDate(string timestamp)
    {
        if (string.IsNullOrEmpty(timestamp)) return "";
        int cut = timestamp.IndexOfAny(new[] { 'T', ' ' });
        return cut > 0 ? timestamp.Substring(0, cut) : timestamp;
    }

    void PopulateBetEntries(List<BetSelection> selectedFood)
    {
        if (betEntriesContainer == null) return;

        for (int i = betEntriesContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = betEntriesContainer.GetChild(i);
            if (child.gameObject != betEntryTemplate) Destroy(child.gameObject);
        }

        if (betEntryTemplate != null) betEntryTemplate.SetActive(false);
        if (selectedFood == null) return;

        foreach (var bet in selectedFood)
        {
            GameObject entry = betEntryTemplate != null
                ? Instantiate(betEntryTemplate, betEntriesContainer)
                : null;
            if (entry == null) continue;
            entry.SetActive(true);

            Image iconImg = entry.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null) iconImg.sprite = GetFoodSprite(bet.key);

            Image coinImg = entry.transform.Find("CoinIcon")?.GetComponent<Image>();
            if (coinImg != null && coinSprite != null) coinImg.sprite = coinSprite;

            Text amountTxt = entry.transform.Find("Amount")?.GetComponent<Text>();
            if (amountTxt != null) amountTxt.text = FormatCoins(bet.amount);
        }
    }

    void PopulateReward(List<string> winningFruits, long winCoins)
    {
        if (rewardIconsContainer != null)
        {
            for (int i = rewardIconsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = rewardIconsContainer.GetChild(i);
                if (child.gameObject != rewardIconTemplate) Destroy(child.gameObject);
            }

            if (rewardIconTemplate != null) rewardIconTemplate.SetActive(false);

            GameManager gm = GameManager.Instance;
            Sprite comboSprite = gm != null ? gm.GetComboRewardSprite(winningFruits) : null;

            if (comboSprite != null)
            {
                // Combo win: one combined salad/pizza icon, same as RoundResultPanel.
                if (rewardIconTemplate != null)
                {
                    GameObject iconObj = Instantiate(rewardIconTemplate, rewardIconsContainer);
                    iconObj.SetActive(true);
                    Image img = iconObj.GetComponent<Image>();
                    if (img != null) img.sprite = comboSprite;
                }
            }
            else if (winningFruits != null)
            {
                // Single win: that one item's own icon.
                foreach (var fruitKey in winningFruits)
                {
                    Sprite fruitSprite = GetFoodSprite(fruitKey);
                    if (fruitSprite == null || rewardIconTemplate == null) continue;

                    GameObject iconObj = Instantiate(rewardIconTemplate, rewardIconsContainer);
                    iconObj.SetActive(true);
                    Image img = iconObj.GetComponent<Image>();
                    if (img != null) img.sprite = fruitSprite;
                }
            }
        }

        if (rewardCoinIcon != null && coinSprite != null) rewardCoinIcon.sprite = coinSprite;
        if (rewardCoinText != null) rewardCoinText.text = FormatCoins(winCoins);
    }

    Sprite GetFoodSprite(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return GameManager.Instance != null ? GameManager.Instance.GetFoodSpriteByID(id) : null;
    }

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000) return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}

[System.Serializable]
public class GameRecord
{
    public int roundNumber;
    public string timestamp;
    public List<BetSelection> selectedFood;
    public List<string> winningFruits;
    public long winCoins;
    public long balanceBefore;
    public long balanceAfter;
}

[System.Serializable]
public class BetSelection
{
    public string key;
    public long amount;
}
