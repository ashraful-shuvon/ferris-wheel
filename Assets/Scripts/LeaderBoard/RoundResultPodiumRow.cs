using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One top-3 slot in RoundResultPanel's "This Round's Ranking" row: an avatar
/// inside an ornate rank frame, the player's name, and their total win amount
/// for this round. No rank number is shown -- rank is conveyed by frame art
/// and size only (biggest for 1st, down to smallest for 3rd).
/// </summary>
public class RoundResultPodiumRow : MonoBehaviour
{
    [Header("Frame + Avatar")]
    [Tooltip("The ornate rank-frame image. Assign your own frame sprites below -- left blank for now.")]
    public Image frameImage;
    public RectTransform frameRect;
    public Image avatarImage;
    public Sprite defaultAvatarSprite;

    [Header("Text")]
    public Text usernameText;
    public Text winAmountText;

    [Header("Rank Frame Art (assign your own art -- currently unassigned)")]
    public Sprite firstPlaceFrame;
    public Sprite secondPlaceFrame;
    public Sprite thirdPlaceFrame;
    public Sprite normalFrame;

    [Header("Rank Frame Sizing")]
    public Vector2 firstPlaceSize  = new Vector2(100f, 100f);
    public Vector2 secondPlaceSize = new Vector2(88f, 88f);
    public Vector2 thirdPlaceSize  = new Vector2(78f, 78f);

    [Header("Fallback Tint (used only while no frame sprite is assigned)")]
    public Color firstPlaceColor  = new Color(1f, 0.84f, 0f, 1f);   // Gold
    public Color secondPlaceColor = new Color(0.75f, 0.75f, 0.75f, 1f); // Silver
    public Color thirdPlaceColor  = new Color(0.8f, 0.5f, 0.2f, 1f);    // Bronze
    public Color normalColor      = new Color(1f, 1f, 1f, 0.1f);

    private Coroutine downloadCoroutine;

    public void SetData(int rank, string username, long wonAmount, string avatarUrlOrName = null)
    {
        if (usernameText != null) usernameText.text = username;
        if (winAmountText != null) winAmountText.text = FormatCoins(wonAmount);

        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);
        if (defaultAvatarSprite != null && avatarImage != null) avatarImage.sprite = defaultAvatarSprite;

        if (avatarImage != null && !string.IsNullOrEmpty(avatarUrlOrName))
        {
            if (avatarUrlOrName.StartsWith("http"))
            {
                downloadCoroutine = StartCoroutine(DownloadAvatarRoutine(avatarUrlOrName));
            }
            else
            {
                Sprite loaded = Resources.Load<Sprite>(avatarUrlOrName);
                if (loaded != null) avatarImage.sprite = loaded;
            }
        }

        Sprite frameSprite = normalFrame;
        Color fallbackColor = normalColor;
        Vector2 size = thirdPlaceSize;

        if (rank == 1) { frameSprite = firstPlaceFrame; fallbackColor = firstPlaceColor; size = firstPlaceSize; }
        else if (rank == 2) { frameSprite = secondPlaceFrame; fallbackColor = secondPlaceColor; size = secondPlaceSize; }
        else if (rank == 3) { frameSprite = thirdPlaceFrame; fallbackColor = thirdPlaceColor; size = thirdPlaceSize; }

        if (frameImage != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.color = frameSprite != null ? Color.white : fallbackColor;
        }
        if (frameRect != null) frameRect.sizeDelta = size;
    }

    private System.Collections.IEnumerator DownloadAvatarRoutine(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null && avatarImage != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    avatarImage.sprite = sprite;
                }
            }
        }
    }

    public void Clear()
    {
        if (usernameText != null) usernameText.text = "";
        if (winAmountText != null) winAmountText.text = "";
    }

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000) return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}
