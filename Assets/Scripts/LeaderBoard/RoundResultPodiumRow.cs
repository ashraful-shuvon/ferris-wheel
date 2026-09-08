using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One ranking row in RoundResultPanel: rank number, circular avatar,
/// gold coin, and the player's win amount for this round.
/// </summary>
public class RoundResultPodiumRow : MonoBehaviour
{
    [Header("Row")]
    public Text rankText;
    public Image avatarImage;
    public Sprite defaultAvatarSprite;
    public Image coinIcon;
    public Text winAmountText;
    [Tooltip("Assigned from the PlayerName child.")]
    public Text usernameText;

    [Header("Unused (kept for serialized compatibility)")]
    public Image frameImage;
    public RectTransform frameRect;
    public Sprite firstPlaceFrame;
    public Sprite secondPlaceFrame;
    public Sprite thirdPlaceFrame;
    public Sprite normalFrame;
    public Vector2 firstPlaceSize  = new Vector2(72f, 72f);
    public Vector2 secondPlaceSize = new Vector2(72f, 72f);
    public Vector2 thirdPlaceSize  = new Vector2(72f, 72f);
    public Color firstPlaceColor  = new Color(1f, 0.84f, 0f, 1f);
    public Color secondPlaceColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color thirdPlaceColor  = new Color(0.8f, 0.5f, 0.2f, 1f);
    public Color normalColor      = new Color(0.62f, 0.62f, 0.62f, 1f);

    static readonly Color InkBrown = new Color(0.36f, 0.16f, 0.07f, 1f);
    static readonly Color AmountBrown = new Color(0.42f, 0.24f, 0.13f, 1f);
    static readonly Color AvatarGrey = new Color(0.62f, 0.62f, 0.62f, 1f);

    const float RowHeight = 96f;
    const float AvatarSize = 72f;

    private Coroutine downloadCoroutine;

    void Awake()
    {
        BindNameText();
    }

    void BindNameText()
    {
        if (usernameText != null) return;
        Transform nameTrans = transform.Find("PlayerName");
        if (nameTrans != null) usernameText = nameTrans.GetComponent<Text>();
    }

    public void SetData(int rank, string username, long wonAmount, string avatarUrlOrName = null)
    {
        BindNameText();
        if (rankText != null) rankText.text = rank.ToString();
        if (usernameText != null)
        {
            usernameText.gameObject.SetActive(true);
            usernameText.text = string.IsNullOrEmpty(username) ? "" : username;
        }
        if (winAmountText != null) winAmountText.text = FormatCoins(wonAmount);

        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);

        if (avatarImage != null)
        {
            if (defaultAvatarSprite != null)
            {
                avatarImage.sprite = defaultAvatarSprite;
                avatarImage.color = Color.white;
            }
            else
            {
                avatarImage.sprite = GetKnobSprite();
                avatarImage.color = AvatarGrey;
            }
        }

        if (avatarImage != null && !string.IsNullOrEmpty(avatarUrlOrName))
        {
            if (avatarUrlOrName.StartsWith("http"))
            {
                downloadCoroutine = StartCoroutine(DownloadAvatarRoutine(avatarUrlOrName));
            }
            else
            {
                Sprite loaded = Resources.Load<Sprite>(avatarUrlOrName);
                if (loaded != null)
                {
                    avatarImage.sprite = loaded;
                    avatarImage.color = Color.white;
                }
            }
        }
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
                    avatarImage.color = Color.white;
                }
            }
        }
    }

    public void Clear()
    {
        if (rankText != null) rankText.text = "";
        if (usernameText != null) usernameText.text = "";
        if (winAmountText != null) winAmountText.text = "";
    }

    static string FormatCoins(long v)
    {
        return v.ToString();
    }

    [ContextMenu("Build Row UI")]
    public void BuildRowUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Row UI");
        }
#endif
        bool isNew;
        Font font = GetBuiltinFont();
        Sprite knob = GetKnobSprite();
        Sprite coinSprite = null;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object[] coinAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Coins/coinImage.png");
            for (int i = 0; i < coinAssets.Length; i++)
            {
                Sprite s = coinAssets[i] as Sprite;
                if (s != null) { coinSprite = s; break; }
            }
        }
#endif
        if (defaultAvatarSprite == null) defaultAvatarSprite = knob;

        RectTransform rect = GetOrAddComponentSafe<RectTransform>(gameObject, "RectTransform", out isNew);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, RowHeight);

        LayoutElement le = GetOrAddComponentSafe<LayoutElement>(gameObject, "LayoutElement", out isNew);
        le.minHeight = RowHeight;
        le.preferredHeight = RowHeight;
        le.flexibleWidth = 1f;

        Image bg = GetComponent<Image>();
        if (bg != null) bg.enabled = false;

        VerticalLayoutGroup oldV = GetComponent<VerticalLayoutGroup>();
        if (oldV != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(oldV);
            else
#endif
            DestroyImmediate(oldV);
        }

        HorizontalLayoutGroup layout = GetOrAddComponentSafe<HorizontalLayoutGroup>(gameObject, "HorizontalLayoutGroup", out isNew);
        layout.padding = new RectOffset(8, 8, 0, 0);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        DestroyChild("Frame");
        DestroyChild("UsernameText");
        DestroyChild("WinAmountText");
        DestroyChild("RankText");
        DestroyChild("Avatar");
        DestroyChild("PlayerName");
        DestroyChild("Spacer");
        DestroyChild("WinnersGroup");

        GameObject rankObj = CreateGameObjectSafe("RankText", transform);
        RectTransform rankRect = GetOrAddComponentSafe<RectTransform>(rankObj, "Rank Rect", out isNew);
        rankRect.sizeDelta = new Vector2(36f, 40f);
        rankText = GetOrAddComponentSafe<Text>(rankObj, "Rank Text", out isNew);
        rankText.text = "1";
        rankText.font = font;
        rankText.fontSize = 26;
        rankText.fontStyle = FontStyle.Bold;
        rankText.alignment = TextAnchor.MiddleCenter;
        rankText.color = InkBrown;
        rankText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rankText.verticalOverflow = VerticalWrapMode.Overflow;
        rankText.raycastTarget = false;

        GameObject avatarObj = CreateGameObjectSafe("Avatar", transform);
        RectTransform avRect = GetOrAddComponentSafe<RectTransform>(avatarObj, "Avatar Rect", out isNew);
        avRect.sizeDelta = new Vector2(AvatarSize, AvatarSize);
        avatarImage = GetOrAddComponentSafe<Image>(avatarObj, "Avatar Image", out isNew);
        avatarImage.sprite = knob;
        avatarImage.color = AvatarGrey;
        avatarImage.preserveAspect = true;
        avatarImage.raycastTarget = false;
        Mask mask = GetOrAddComponentSafe<Mask>(avatarObj, "Avatar Mask", out isNew);
        mask.showMaskGraphic = true;

        GameObject nameObj = CreateGameObjectSafe("PlayerName", transform);
        RectTransform nameRect = GetOrAddComponentSafe<RectTransform>(nameObj, "PlayerName Rect", out isNew);
        nameRect.sizeDelta = new Vector2(160f, 40f);
        LayoutElement nameLe = GetOrAddComponentSafe<LayoutElement>(nameObj, "PlayerName LE", out isNew);
        nameLe.minWidth = 80f;
        nameLe.preferredWidth = 160f;
        nameLe.flexibleWidth = 0f;
        usernameText = GetOrAddComponentSafe<Text>(nameObj, "PlayerName Text", out isNew);
        usernameText.text = "Player";
        usernameText.font = font;
        usernameText.fontSize = 22;
        usernameText.fontStyle = FontStyle.Bold;
        usernameText.alignment = TextAnchor.MiddleLeft;
        usernameText.color = InkBrown;
        usernameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        usernameText.verticalOverflow = VerticalWrapMode.Overflow;
        usernameText.raycastTarget = false;

        GameObject spacerObj = CreateGameObjectSafe("Spacer", transform);
        GetOrAddComponentSafe<RectTransform>(spacerObj, "Spacer Rect", out isNew);
        LayoutElement spacerLe = GetOrAddComponentSafe<LayoutElement>(spacerObj, "Spacer LE", out isNew);
        spacerLe.flexibleWidth = 1f;
        spacerLe.minWidth = 8f;

        GameObject winnersObj = CreateGameObjectSafe("WinnersGroup", transform);
        RectTransform winRect = GetOrAddComponentSafe<RectTransform>(winnersObj, "Winners Rect", out isNew);
        winRect.sizeDelta = new Vector2(210f, 40f);
        HorizontalLayoutGroup winLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(winnersObj, "Winners Layout", out isNew);
        winLayout.spacing = 8f;
        winLayout.childAlignment = TextAnchor.MiddleLeft;
        winLayout.childControlWidth = false;
        winLayout.childControlHeight = false;
        winLayout.childForceExpandWidth = false;
        winLayout.childForceExpandHeight = false;
        winLayout.padding = new RectOffset(0, 0, 0, 0);

        GameObject coinObj = CreateGameObjectSafe("CoinIcon", winnersObj.transform);
        RectTransform coinRect = GetOrAddComponentSafe<RectTransform>(coinObj, "Coin Rect", out isNew);
        coinRect.sizeDelta = new Vector2(32f, 32f);
        coinIcon = GetOrAddComponentSafe<Image>(coinObj, "Coin Image", out isNew);
        coinIcon.preserveAspect = true;
        coinIcon.raycastTarget = false;
        if (coinSprite != null)
        {
            coinIcon.sprite = coinSprite;
            coinIcon.color = Color.white;
        }
        else if (knob != null)
        {
            coinIcon.sprite = knob;
            coinIcon.color = new Color(1f, 0.78f, 0.12f, 1f);
        }

        GameObject amountObj = CreateGameObjectSafe("WinAmountText", winnersObj.transform);
        RectTransform amountRect = GetOrAddComponentSafe<RectTransform>(amountObj, "Amount Rect", out isNew);
        amountRect.sizeDelta = new Vector2(170f, 40f);
        winAmountText = GetOrAddComponentSafe<Text>(amountObj, "Amount Text", out isNew);
        winAmountText.text = "280000";
        winAmountText.font = font;
        winAmountText.fontSize = 26;
        winAmountText.fontStyle = FontStyle.Bold;
        winAmountText.alignment = TextAnchor.MiddleLeft;
        winAmountText.color = AmountBrown;
        winAmountText.horizontalOverflow = HorizontalWrapMode.Overflow;
        winAmountText.verticalOverflow = VerticalWrapMode.Overflow;
        winAmountText.raycastTarget = false;

        frameImage = null;
        frameRect = null;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    private void DestroyChild(string name)
    {
        Transform child = transform.Find(name);
        if (child == null) return;
        DestroyObjectSafe(child.gameObject);
    }

    private void DestroyObjectSafe(GameObject go)
    {
        if (go == null) return;
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(go);
#endif
        }
        else
        {
            DestroyImmediate(go);
        }
    }

    private Sprite GetKnobSprite()
    {
#if UNITY_EDITOR
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Border/UICircle.png");
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite s = assets[i] as Sprite;
            if (s != null) return s;
        }
#endif
        Sprite circle = Resources.Load<Sprite>("UICircle");
        if (circle != null) return circle;
        try { return Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    private Font GetBuiltinFont()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
        }
        return font;
    }

    private T GetOrAddComponentSafe<T>(GameObject target, string name, out bool isNew) where T : Component
    {
        T comp = target.GetComponent<T>();
        if (comp == null)
        {
            isNew = true;
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                comp = UnityEditor.Undo.AddComponent<T>(target);
#endif
            }
            else
            {
                comp = target.AddComponent<T>();
            }
        }
        else
        {
            isNew = false;
        }

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(comp, "Modify " + name);
#endif
        }
        return comp;
    }

    private GameObject CreateGameObjectSafe(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.layer = parent != null ? parent.gameObject.layer : 5;
        go.transform.SetParent(parent, false);
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif
        }
        return go;
    }
}
