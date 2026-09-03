using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundHistoryTracker : MonoBehaviour
{
    private const int MaxHistoryItems = 20;

    [Header("UI Slots (Assign 20 Image elements, newest to oldest)")]
    [Tooltip("The first 8 slots are visible initially; scroll vertically to reveal slots 9-20.")]
    public List<Image> historySlots = new List<Image>();

    // Internal tracker keeping record of the food sprites won in the last 20 rounds.
    private List<Sprite> winningHistory = new List<Sprite>();
    private ScrollRect historyScrollRect;

    void Awake()
    {
        historyScrollRect = GetComponent<ScrollRect>();
        ClearHistoryVisuals();
    }

    /// <summary>
    /// Clears the history display on game startup.
    /// </summary>
    public void ClearHistoryVisuals()
    {
        foreach (var img in historySlots)
        {
            if (img != null)
            {
                img.sprite = null;
                SetSlotActive(img, false); // Hide the slot if there's no history yet
            }
        }
    }

    /// <summary>
    /// Overwrites the history with a list of winning sprites (e.g. on game startup from server).
    /// </summary>
    public void SetHistory(List<Sprite> sprites)
    {
        winningHistory = new List<Sprite>(sprites);
        if (winningHistory.Count > MaxHistoryItems)
        {
            winningHistory.RemoveRange(MaxHistoryItems, winningHistory.Count - MaxHistoryItems);
        }
        UpdateHistoryUI();
    }

    /// <summary>
    /// Adds a new winning food image to the list and updates the 20 UI slots.
    /// </summary>
    public void AddRoundResult(Sprite winningFoodSprite)
    {
        if (winningFoodSprite == null) return;

        // Insert new win at index 0 so newest appears first.
        // Change to winningHistory.Add(winningFoodSprite) if you prefer newest at the end!
        winningHistory.Insert(0, winningFoodSprite);

        // Cap the history depth at 12 records.
        if (winningHistory.Count > MaxHistoryItems)
        {
            winningHistory.RemoveAt(winningHistory.Count - 1);
        }

        // Refresh the visible slots
        UpdateHistoryUI();
    }

    void UpdateHistoryUI()
    {
        for (int i = 0; i < historySlots.Count; i++)
        {
            if (historySlots[i] == null) continue;

            if (i < winningHistory.Count)
            {
                historySlots[i].sprite = winningHistory[i];
                SetSlotActive(historySlots[i], true);
            }
            else
            {
                historySlots[i].sprite = null;
                SetSlotActive(historySlots[i], false);
            }
        }

        // Newly inserted results are at the top, so always keep the newest eight visible.
        if (historyScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            historyScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// Shows/hides a whole slot. The icon sits inside a container alongside its
    /// decorative Frame (icon on top so the frame doesn't cover it) -- toggling
    /// the container hides both together, same as toggling the icon used to
    /// before the frame existed.
    /// </summary>
    void SetSlotActive(Image icon, bool active)
    {
        GameObject target = icon.transform.parent != null ? icon.transform.parent.gameObject : icon.gameObject;
        target.SetActive(active);
    }
}
