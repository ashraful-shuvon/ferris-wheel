using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Zimo.Net;

/// <summary>
/// Small on-screen widget showing the global #1 leaderboard player's avatar,
/// name and coin amount. The click -> RankingJackpotPanel wiring is done via
/// the Button's OnClick() list in the Inspector, not here.
/// </summary>
public class GlobalTopWidget : MonoBehaviour
{
    [Header("UI Component References")]
    public Image avatarImage;
    public Text usernameText;
    public Text coinsText;
    public Button button;

    [Header("Visual Customization")]
    public Sprite defaultAvatarSprite;

    private Coroutine downloadCoroutine;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.api == null) return;

        gm.api.GetLeaderboard(entries =>
        {
            if (entries != null && entries.Length > 0)
                SetData(entries[0]);
        });
    }

    private void SetData(LeaderboardEntryDto top)
    {
        if (usernameText != null) usernameText.text = top.username;
        if (coinsText != null) coinsText.text = top.wonAmount.ToString();

        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);
        if (defaultAvatarSprite != null && avatarImage != null) avatarImage.sprite = defaultAvatarSprite;

        if (avatarImage != null && !string.IsNullOrEmpty(top.avatar) && top.avatar.StartsWith("http"))
        {
            downloadCoroutine = StartCoroutine(DownloadAvatarRoutine(top.avatar));
        }
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

    [ContextMenu("Build Global Top UI")]
    public void BuildGlobalTopUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Global Top UI");
        }
#endif

        bool isNew;
        Font defaultFont = GetBuiltinFontSafe();

        if (button == null) button = GetOrAddComponentSafe<Button>(gameObject, "GlobalTop Button", out isNew);

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null) rootRect.sizeDelta = new Vector2(90f, 90f);

        // Avatar (fills the button, sits behind name/coins)
        Transform avatarTrans = transform.Find("Avatar");
        GameObject avatarObj = avatarTrans != null ? avatarTrans.gameObject : CreateGameObjectSafe("Avatar", transform);
        RectTransform avRect = GetOrAddComponentSafe<RectTransform>(avatarObj, "Avatar RectTransform", out isNew);
        avRect.anchorMin = new Vector2(0.5f, 1f);
        avRect.anchorMax = new Vector2(0.5f, 1f);
        avRect.pivot = new Vector2(0.5f, 1f);
        avRect.sizeDelta = new Vector2(56f, 56f);
        avRect.anchoredPosition = new Vector2(0f, -4f);
        avatarImage = GetOrAddComponentSafe<Image>(avatarObj, "Avatar Image", out isNew);
        avatarImage.preserveAspect = true;
        if (defaultAvatarSprite != null) avatarImage.sprite = defaultAvatarSprite;

        // Username
        Transform userTrans = transform.Find("Username");
        GameObject userObj = userTrans != null ? userTrans.gameObject : CreateGameObjectSafe("Username", transform);
        RectTransform userRect = GetOrAddComponentSafe<RectTransform>(userObj, "Username RectTransform", out isNew);
        userRect.anchorMin = new Vector2(0.5f, 1f);
        userRect.anchorMax = new Vector2(0.5f, 1f);
        userRect.pivot = new Vector2(0.5f, 1f);
        userRect.sizeDelta = new Vector2(110f, 20f);
        userRect.anchoredPosition = new Vector2(0f, -62f);
        usernameText = GetOrAddComponentSafe<Text>(userObj, "Username Text", out isNew);
        usernameText.font = defaultFont;
        usernameText.fontSize = 14;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.alignment = TextAnchor.MiddleCenter;
        usernameText.color = Color.white;
        usernameText.text = "Top Player";

        // Coins
        Transform coinsTrans = transform.Find("Coins");
        GameObject coinsObj = coinsTrans != null ? coinsTrans.gameObject : CreateGameObjectSafe("Coins", transform);
        RectTransform coinsRect = GetOrAddComponentSafe<RectTransform>(coinsObj, "Coins RectTransform", out isNew);
        coinsRect.anchorMin = new Vector2(0.5f, 1f);
        coinsRect.anchorMax = new Vector2(0.5f, 1f);
        coinsRect.pivot = new Vector2(0.5f, 1f);
        coinsRect.sizeDelta = new Vector2(110f, 18f);
        coinsRect.anchoredPosition = new Vector2(0f, -82f);
        coinsText = GetOrAddComponentSafe<Text>(coinsObj, "Coins Text", out isNew);
        coinsText.font = defaultFont;
        coinsText.fontSize = 13;
        coinsText.fontStyle = FontStyle.Bold;
        coinsText.alignment = TextAnchor.MiddleCenter;
        coinsText.color = new Color(1f, 0.84f, 0f, 1f); // Gold
        coinsText.text = "0";

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private Font GetBuiltinFontSafe()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
        if (font != null) return font;

        try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
        if (font != null) return font;

        Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
        if (fonts != null && fonts.Length > 0) return fonts[0];

        return null;
    }

    private T GetOrAddComponentSafe<T>(GameObject target, string name, out bool isNew) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null)
        {
            isNew = true;
#if UNITY_EDITOR
            if (!Application.isPlaying) comp = UnityEditor.Undo.AddComponent<T>(target);
            else comp = target.AddComponent<T>();
#else
            comp = target.AddComponent<T>();
#endif
        }
        else
        {
            isNew = false;
        }

        if (comp is Text textComp)
        {
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(comp, "Modify " + name);
#endif
        return comp;
    }

    private GameObject CreateGameObjectSafe(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif
        return go;
    }
}
