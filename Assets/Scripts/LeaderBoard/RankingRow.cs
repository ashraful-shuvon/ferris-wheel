using UnityEngine;
using UnityEngine.UI;

public class RankingRow : MonoBehaviour
{
    [Header("UI Component References")]
    public Image backgroundImage;
    public Image rankBadgeImage;
    public Text rankText;
    public Image avatarImage;
    public Text usernameText;
    public Image coinIcon;
    public Text coinsText;

    [Header("Visual Customization")]
    public Sprite defaultAvatarSprite;
    
    [Header("Rank Badge Sprites")]
    public Sprite rank1Sprite;
    public Sprite rank2Sprite;
    public Sprite rank3Sprite;
    public Sprite rankNormalSprite;

    private Coroutine downloadCoroutine;

    private void Awake()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
    }

    public void SetData(int rank, string username, long coins, Sprite avatar = null, string avatarUrlOrName = null)
    {
        if (usernameText != null) usernameText.text = username;
        if (coinsText != null) coinsText.text = FormatCoins(coins);

        if (downloadCoroutine != null) StopCoroutine(downloadCoroutine);
        if (defaultAvatarSprite != null && avatarImage != null) avatarImage.sprite = defaultAvatarSprite;

        if (avatarImage != null)
        {
            if (avatar != null)
            {
                avatarImage.sprite = avatar;
            }
            else if (!string.IsNullOrEmpty(avatarUrlOrName))
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
        }

        // Apply badge sprite & dynamic text visibility based on rank
        if (rankBadgeImage != null)
        {
            if (rank <= 0)
            {
                rankBadgeImage.gameObject.SetActive(false);
                if (rankText != null) rankText.gameObject.SetActive(false);
            }
            else
            {
                rankBadgeImage.gameObject.SetActive(true);
                rankBadgeImage.color = Color.white; // Keep sprite untinted

                if (rank == 1)
                {
                    rankBadgeImage.sprite = rank1Sprite != null ? rank1Sprite : GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
                    if (rankText != null) rankText.gameObject.SetActive(false); // Gold sprite has '1' embedded
                }
                else if (rank == 2)
                {
                    rankBadgeImage.sprite = rank2Sprite != null ? rank2Sprite : GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
                    if (rankText != null) rankText.gameObject.SetActive(false); // Silver sprite has '2' embedded
                }
                else if (rank == 3)
                {
                    rankBadgeImage.sprite = rank3Sprite != null ? rank3Sprite : GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
                    if (rankText != null) rankText.gameObject.SetActive(false); // Bronze sprite has '3' embedded
                }
                else
                {
                    rankBadgeImage.sprite = rankNormalSprite != null ? rankNormalSprite : GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
                    if (rankText != null)
                    {
                        rankText.gameObject.SetActive(true); // Normal sprite is blank, needs dynamic text
                        rankText.text = rank.ToString();
                    }
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
                }
            }
        }
    }

    private string FormatCoins(long val)
    {
        return val.ToString();
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
        Sprite knobSprite = GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
        Font defaultFont = GetBuiltinFontSafe();

        // 1. Setup RectTransform on this object
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) rect = GetOrAddComponentSafe<RectTransform>(gameObject, "RectTransform", out isNew);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 55f);

        LayoutElement le = GetOrAddComponentSafe<LayoutElement>(gameObject, "LayoutElement", out isNew);
        le.minHeight = 55f;
        le.preferredHeight = 55f;

        // 2. Setup Background Image
        backgroundImage = GetComponent<Image>();
        if (backgroundImage == null) backgroundImage = GetOrAddComponentSafe<Image>(gameObject, "Background Image", out isNew);
        backgroundImage.color = new Color(0.08f, 0.15f, 0.3f, 0.9f); // Sleek semi-transparent dark blue-gray

        // Add a Horizontal Layout Group
        HorizontalLayoutGroup layout = GetOrAddComponentSafe<HorizontalLayoutGroup>(gameObject, "HorizontalLayoutGroup", out isNew);
        layout.padding = new RectOffset(12, 12, 5, 5);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (knobSprite != null)
        {
            if (rank1Sprite == null) rank1Sprite = knobSprite;
            if (rank2Sprite == null) rank2Sprite = knobSprite;
            if (rank3Sprite == null) rank3Sprite = knobSprite;
            if (rankNormalSprite == null) rankNormalSprite = knobSprite;
        }

        // 3. Create Rank Badge (Circle Image + Center Text)
        Transform badgeTrans = transform.Find("RankBadge");
        if (badgeTrans == null)
        {
            GameObject badgeObj = CreateGameObjectSafe("RankBadge", transform);
            badgeTrans = badgeObj.transform;

            RectTransform badgeRect = GetOrAddComponentSafe<RectTransform>(badgeObj, "Badge RectTransform", out isNew);
            badgeRect.sizeDelta = new Vector2(36f, 36f);

            rankBadgeImage = GetOrAddComponentSafe<Image>(badgeObj, "Badge Image", out isNew);
            rankBadgeImage.color = Color.white;
            if (knobSprite != null) rankBadgeImage.sprite = knobSprite;

            GameObject textObj = CreateGameObjectSafe("Text", badgeTrans);
            RectTransform txtRect = GetOrAddComponentSafe<RectTransform>(textObj, "Badge Text RectTransform", out isNew);
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            rankText = GetOrAddComponentSafe<Text>(textObj, "Badge Text", out isNew);
            rankText.text = "1";
            rankText.font = defaultFont;
            rankText.fontSize = 16;
            rankText.fontStyle = FontStyle.Bold;
            rankText.alignment = TextAnchor.MiddleCenter;
            rankText.color = Color.white;
        }
        else
        {
            rankBadgeImage = badgeTrans.GetComponent<Image>();
            Transform t = badgeTrans.Find("Text");
            if (t != null) rankText = t.GetComponent<Text>();
        }

        // 4. Create Avatar image
        Transform avatarTrans = transform.Find("Avatar");
        if (avatarTrans == null)
        {
            GameObject avatarObj = CreateGameObjectSafe("Avatar", transform);
            avatarTrans = avatarObj.transform;

            RectTransform avRect = GetOrAddComponentSafe<RectTransform>(avatarObj, "Avatar RectTransform", out isNew);
            avRect.sizeDelta = new Vector2(36f, 36f);

            avatarImage = GetOrAddComponentSafe<Image>(avatarObj, "Avatar Image", out isNew);
            avatarImage.color = Color.white;
            if (knobSprite != null) avatarImage.sprite = knobSprite;
        }
        else
        {
            avatarImage = avatarTrans.GetComponent<Image>();
        }

        // 5. Create Username Text
        Transform userTrans = transform.Find("UsernameText");
        if (userTrans == null)
        {
            GameObject userObj = CreateGameObjectSafe("UsernameText", transform);
            userTrans = userObj.transform;

            RectTransform uRect = GetOrAddComponentSafe<RectTransform>(userObj, "User RectTransform", out isNew);
            uRect.sizeDelta = new Vector2(130f, 30f);

            usernameText = GetOrAddComponentSafe<Text>(userObj, "User Text", out isNew);
            usernameText.text = "Username";
            usernameText.font = defaultFont;
            usernameText.fontSize = 16;
            usernameText.fontStyle = FontStyle.Normal;
            usernameText.alignment = TextAnchor.MiddleLeft;
            usernameText.color = Color.white;
        }
        else
        {
            usernameText = userTrans.GetComponent<Text>();
        }

        // 6. Create Flexible Spacer to push coins to the right
        Transform spacerTrans = transform.Find("Spacer");
        if (spacerTrans == null)
        {
            GameObject spacerObj = CreateGameObjectSafe("Spacer", transform);
            spacerTrans = spacerObj.transform;
            GetOrAddComponentSafe<RectTransform>(spacerObj, "Spacer RectTransform", out isNew);
            LayoutElement spacerLE = GetOrAddComponentSafe<LayoutElement>(spacerObj, "Spacer LayoutElement", out isNew);
            spacerLE.flexibleWidth = 1f;
        }

        // 7. Create Coins Play display
        Transform coinsTrans = transform.Find("CoinsPlay");
        if (coinsTrans == null)
        {
            GameObject coinsObj = CreateGameObjectSafe("CoinsPlay", transform);
            coinsTrans = coinsObj.transform;

            RectTransform cRect = GetOrAddComponentSafe<RectTransform>(coinsObj, "Coins RectTransform", out isNew);
            cRect.sizeDelta = new Vector2(140f, 30f);

            HorizontalLayoutGroup cLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(coinsObj, "Coins HorizontalLayoutGroup", out isNew);
            cLayout.spacing = 6f;
            cLayout.childAlignment = TextAnchor.MiddleRight;
            cLayout.childControlWidth = false;
            cLayout.childControlHeight = false;
            cLayout.childForceExpandWidth = false;
            cLayout.childForceExpandHeight = false;

            // Coin Icon
            GameObject coinIconObj = CreateGameObjectSafe("CoinIcon", coinsTrans);
            RectTransform iconRect = GetOrAddComponentSafe<RectTransform>(coinIconObj, "CoinIcon RectTransform", out isNew);
            iconRect.sizeDelta = new Vector2(18f, 18f);

            coinIcon = GetOrAddComponentSafe<Image>(coinIconObj, "CoinIcon Image", out isNew);
            coinIcon.color = new Color(1f, 0.85f, 0f, 1f); // Gold coin tint
            if (knobSprite != null) coinIcon.sprite = knobSprite;

            // Coins Text
            GameObject coinsTxtObj = CreateGameObjectSafe("CoinsText", coinsTrans);
            RectTransform txtRect = GetOrAddComponentSafe<RectTransform>(coinsTxtObj, "CoinsText RectTransform", out isNew);
            txtRect.sizeDelta = new Vector2(110f, 30f);

            coinsText = GetOrAddComponentSafe<Text>(coinsTxtObj, "CoinsText Text", out isNew);
            coinsText.text = "0";
            coinsText.font = defaultFont;
            coinsText.fontSize = 16;
            coinsText.fontStyle = FontStyle.Bold;
            coinsText.alignment = TextAnchor.MiddleLeft;
            coinsText.color = new Color(1f, 0.84f, 0f, 1f); // Gold text
        }
        else
        {
            coinIcon = coinsTrans.Find("CoinIcon")?.GetComponent<Image>();
            coinsText = coinsTrans.Find("CoinsText")?.GetComponent<Text>();
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    private Sprite GetBuiltinSpriteSafe(string path)
    {
        try
        {
            return Resources.GetBuiltinResource<Sprite>(path);
        }
        catch
        {
            return null;
        }
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

        if (comp is Text textComp)
        {
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(comp, "Modify " + name);
        }
#endif
        return comp;
    }

    private GameObject CreateGameObjectSafe(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
#endif
        return go;
    }
}
