using UnityEngine;

public enum BetCategory
{
    Pizza,
    Vegetable,
}

[CreateAssetMenu(fileName = "BetButtonData", menuName = "BettingGame/BetButtonData")]
public class BetButtonData : ScriptableObject
{
    [Header("Button Identity")]
    public string buttonID;        // e.g. "B1"
    public Sprite buttonIcon;
    
    [Header("Round History Icon")]
    [Tooltip("Small icon shown in the 8 round history slots after this button wins.")]
    public Sprite historyIcon;

    [Header("Category")]
    [Tooltip("Which group this button belongs to. The server picks a winning " +
             "category each round (Pizza or Vegetable) — this must match how " +
             "the backend categorizes the same item key.")]
    public BetCategory category;

    [Header("Win Multiplier")]
    [Tooltip("How much the player wins if this button is selected. E.g. 5 = 5x, 10 = 10x")]
    public float winMultiplier = 5f;
}
