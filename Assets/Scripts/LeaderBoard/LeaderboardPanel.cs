using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Pure leaderboard panel — shows a ranked list of top winners.
/// Win / Lose result images live in RoundResultPanel, not here.
/// </summary>
public class LeaderboardPanel : MonoBehaviour
{
    [Header("Dynamic Row Creation")]
    public LeaderboardRow rowPrefab;
    public Transform rowsContainer;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Timing")]
    public float autoCloseDuration = 4.5f;

    [Header("Animation")]
    public float openDuration  = 0.4f;
    public Ease  openEase      = Ease.OutBack;
    public float closeDuration = 0.3f;
    public Ease  closeEase     = Ease.InBack;

    [Header("Dynamic Script Layout Configuration")]
    public Vector2 leaderboardSize = new Vector2(500f, 700f);
    public Vector2 leaderboardPosition = Vector2.zero;
    public Sprite panelBackgroundSprite;
    public Color panelBackgroundColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);

    [Header("Dynamic Script Title Configuration")]
    public string titleString = "LEADERBOARD";
    public int titleFontSize = 34;
    public Color titleColor = new Color(1f, 0.84f, 0f, 1f); // Gold

    [Header("Dynamic Script Close Button Configuration")]
    public Vector2 closeButtonSize = new Vector2(40f, 40f);
    public Color closeButtonColor = new Color(0.8f, 0.25f, 0.25f, 1f); // Reddish

    CanvasGroup   canvasGroup;
    RectTransform panelRect;
    bool          isShowing = false;

    // ── Unity Lifecycle ──────────────────────────────────────────────────────

    void InitializeIfNeeded()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Awake()
    {
        InitializeIfNeeded();
        // Commented out to prevent the script from overriding manual layout/design tweaks at runtime.
        // BuildLeaderboardUI();
 
        // Start fully hidden but GameObject stays active so coroutines run.
        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
        if (panelRect != null) panelRect.localScale = Vector3.zero;
 
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        gameObject.SetActive(false);
    }

    [ContextMenu("Build Leaderboard UI")]
    public void BuildLeaderboardUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Leaderboard UI");
        }
#endif

        bool isNew;

        // 1. Setup the Panel's RectTransform
        panelRect = GetComponent<RectTransform>();
        if (panelRect == null)
        {
            panelRect = GetOrAddComponentSafe<RectTransform>(gameObject, "Panel RectTransform", out isNew);
            panelRect.anchoredPosition = leaderboardPosition;
            panelRect.sizeDelta = leaderboardSize;
            
            // Also ensure anchors are centered/centered
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObject(panelRect, "Modify RectTransform");
#endif
        }

        // 2. Setup Background Image
        Transform childBg = transform.Find("Background");
        if (childBg == null)
        {
            Image bgImage = GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = GetOrAddComponentSafe<Image>(gameObject, "Panel Image", out isNew);
            }
            else
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RecordObject(bgImage, "Modify Image");
#endif
            }
            bgImage.enabled = true;
            bgImage.sprite = panelBackgroundSprite;
            bgImage.color = panelBackgroundColor;
            if (panelBackgroundSprite != null)
            {
                bgImage.type = Image.Type.Sliced;
            }
        }
        else
        {
            Image bgImage = GetComponent<Image>();
            if (bgImage != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.Undo.RecordObject(bgImage, "Modify Image");
#endif
                bgImage.enabled = false;
            }
        }

        // 3. Create Title Text (if it doesn't exist and no custom Logo is present)
        Transform titleTransform = transform.Find("TitleText");
        Transform logoTransform = transform.Find("Logo");
        if (titleTransform == null && logoTransform == null)
        {
            GameObject titleObj = CreateGameObjectSafe("TitleText", transform);
            titleTransform = titleObj.transform;

            RectTransform titleRect = GetOrAddComponentSafe<RectTransform>(titleObj, "TitleText RectTransform", out isNew);
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(panelRect.rect.width - 100f, 60f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);

            Text textComp = GetOrAddComponentSafe<Text>(titleObj, "TitleText Text", out isNew);
            textComp.text = titleString;
            textComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComp.fontSize = titleFontSize;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = titleColor;
            
            // Add shadow
            Shadow shadow = GetOrAddComponentSafe<Shadow>(titleObj, "TitleText Shadow", out isNew);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        // 4. Create ScrollRect and Container (if rowsContainer is null)
        if (rowsContainer == null)
        {
            Transform existingScroll = transform.Find("ScrollView");
            if (existingScroll != null)
            {
                Transform viewport = existingScroll.Find("Viewport");
                if (viewport != null)
                {
                    Transform content = viewport.Find("Content");
                    if (content != null)
                    {
                        rowsContainer = content;
                    }
                }
            }
        }

        if (rowsContainer == null)
        {
            // ScrollView GameObject
            GameObject scrollViewObj = CreateGameObjectSafe("ScrollView", transform);
            RectTransform scrollRectTransform = GetOrAddComponentSafe<RectTransform>(scrollViewObj, "ScrollView RectTransform", out isNew);
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(25f, 30f);
            scrollRectTransform.offsetMax = new Vector2(-25f, -90f);

            ScrollRect scrollRect = GetOrAddComponentSafe<ScrollRect>(scrollViewObj, "ScrollView ScrollRect", out isNew);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            Image scrollBg = GetOrAddComponentSafe<Image>(scrollViewObj, "ScrollView Image", out isNew);
            scrollBg.color = new Color(0f, 0f, 0f, 0.2f);

            // Viewport GameObject
            GameObject viewportObj = CreateGameObjectSafe("Viewport", scrollViewObj.transform);
            RectTransform viewportRect = GetOrAddComponentSafe<RectTransform>(viewportObj, "Viewport RectTransform", out isNew);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = GetOrAddComponentSafe<Image>(viewportObj, "Viewport Image", out isNew);
            viewportImage.color = new Color(1f, 1f, 1f, 0.005f);
            Mask mask = GetOrAddComponentSafe<Mask>(viewportObj, "Viewport Mask", out isNew);
            mask.showMaskGraphic = false;

            // Content GameObject
            GameObject contentObj = CreateGameObjectSafe("Content", viewportObj.transform);
            RectTransform contentRect = GetOrAddComponentSafe<RectTransform>(contentObj, "Content RectTransform", out isNew);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);

            // Add VerticalLayoutGroup
            VerticalLayoutGroup vLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(contentObj, "Content VerticalLayoutGroup", out isNew);
            vLayout.spacing = 8f;
            vLayout.padding = new RectOffset(10, 10, 10, 10);
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Add ContentSizeFitter
            ContentSizeFitter sizeFitter = GetOrAddComponentSafe<ContentSizeFitter>(contentObj, "Content ContentSizeFitter", out isNew);
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            rowsContainer = contentRect.transform;
        }

        // 5. Create Close Button (if closeButton is null)
        if (closeButton == null)
        {
            Transform existingClose = transform.Find("closeButton");
            if (existingClose == null) existingClose = transform.Find("CloseButton");
            if (existingClose != null)
            {
                closeButton = existingClose.GetComponent<Button>();
            }
        }

        if (closeButton == null)
        {
            GameObject closeBtnObj = CreateGameObjectSafe("CloseButton", transform);
            RectTransform btnRect = GetOrAddComponentSafe<RectTransform>(closeBtnObj, "CloseButton RectTransform", out isNew);
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.sizeDelta = closeButtonSize;
            btnRect.anchoredPosition = new Vector2(-15f, -15f);

            Image btnImage = GetOrAddComponentSafe<Image>(closeBtnObj, "CloseButton Image", out isNew);
            btnImage.color = closeButtonColor;
            
            Button btn = GetOrAddComponentSafe<Button>(closeBtnObj, "CloseButton Button", out isNew);
            closeButton = btn;

            GameObject btnTextObj = CreateGameObjectSafe("Text", closeBtnObj.transform);
            RectTransform txtRect = GetOrAddComponentSafe<RectTransform>(btnTextObj, "Text RectTransform", out isNew);
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            Text txt = GetOrAddComponentSafe<Text>(btnTextObj, "Text Text", out isNew);
            txt.text = "X";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(gameObject);
        if (!Application.isPlaying)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prefabStage.scene);
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
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
        go.transform.SetParent(parent, false);
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif
        }
        return go;
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

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the leaderboard and waits until it auto-closes or the player
    /// taps the close button.  yield return this from GameManager.
    /// </summary>
    public Coroutine ShowAndWait(List<LeaderboardEntry> entries)
    {
        gameObject.SetActive(true);
        return StartCoroutine(ShowSequence(entries));
    }

    /// <summary>
    /// Backward-compatible overload — the win/lose parameters are ignored
    /// because result UI now lives in RoundResultPanel.
    /// </summary>
    public Coroutine ShowAndWait(List<LeaderboardEntry> entries, bool playerWon,
                                 int winnerIndex, bool isCombo)
    {
        gameObject.SetActive(true);
        return StartCoroutine(ShowSequence(entries));
    }

    public void Hide()
    {
        isShowing = false;
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    IEnumerator ShowSequence(List<LeaderboardEntry> entries)
    {
        InitializeIfNeeded();
        isShowing = true;

        // ── Fill rows ─────────────────────────────────────────────────────────
        if (rowsContainer != null)
        {
            foreach (Transform child in rowsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (rowPrefab != null && rowsContainer != null && entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardRow rowInstance = Instantiate(rowPrefab, rowsContainer);
                rowInstance.SetData(entries[i].rank, entries[i].username, entries[i].wonAmount, entries[i].todaysWin);
            }
        }

        // ── Animate in ────────────────────────────────────────────────────────
        panelRect.DOKill();
        canvasGroup.DOKill();
        panelRect.localScale = Vector3.zero;
        canvasGroup.alpha    = 0f;

        panelRect.DOScale(Vector3.one, openDuration).SetEase(openEase);
        canvasGroup.DOFade(1f, openDuration * 0.5f)
                   .OnComplete(() =>
                   {
                       canvasGroup.interactable   = true;
                       canvasGroup.blocksRaycasts = true;
                   });

        // ── Wait for auto-close or manual close ───────────────────────────────
        float elapsed = 0f;
        while (isShowing && elapsed < autoCloseDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Animate out ───────────────────────────────────────────────────────
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        panelRect.DOKill();
        canvasGroup.DOKill();

        panelRect.DOScale(Vector3.zero, closeDuration).SetEase(closeEase);
        canvasGroup.DOFade(0f, closeDuration);

        yield return new WaitForSeconds(closeDuration);

        canvasGroup.alpha    = 0f;
        panelRect.localScale = Vector3.zero;
        isShowing            = false;
        gameObject.SetActive(false);
    }
}

[System.Serializable]
public class LeaderboardEntry
{
    public int    rank;
    public string username;
    public long   wonAmount;
    public long   todaysWin;
    public string avatar;
}
