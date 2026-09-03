using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Top-bar widget combining the player's profile picture, current coin
/// amount, and an Add (top-up) button in one object. The coin text itself
/// is kept live by GameManager.coinBalanceText (unchanged) — this script
/// only owns the avatar download. The Add button's click -> OnAddClicked
/// wiring is done via the Button's OnClick() list in the Inspector, not here.
/// </summary>
public class ProfileCoinsBar : MonoBehaviour
{
    [Header("UI Component References")]
    public Image avatarImage;
    public TextMeshProUGUI coinsText;
    public Button addButton;

    [Header("Visual Customization")]
    public Sprite defaultAvatarSprite;

    private Coroutine downloadCoroutine;

    private void Start()
    {
        RefreshAvatar();
    }

    public void RefreshAvatar()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.api == null) return;

        gm.api.GetBalance(balance =>
        {
            if (balance != null && !string.IsNullOrEmpty(balance.avatar))
                LoadAvatar(balance.avatar);
        });
    }

    private void LoadAvatar(string url)
    {
        if (avatarImage == null || string.IsNullOrEmpty(url) || !url.StartsWith("http")) return;
        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);
        downloadCoroutine = StartCoroutine(DownloadAvatarRoutine(url));
    }

    private IEnumerator DownloadAvatarRoutine(string url)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null && avatarImage != null)
                {
                    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    avatarImage.sprite = sprite;
                }
            }
        }
    }

    public void OnAddClicked()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.api == null) return;

        gm.api.PostAddCoins(
            onOk: _ => Debug.Log("[ProfileCoinsBar] Add coins request succeeded."),
            onErr: err => Debug.LogWarning("[ProfileCoinsBar] Add coins request failed: " + err));
    }
}
