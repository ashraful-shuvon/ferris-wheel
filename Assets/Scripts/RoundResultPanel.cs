using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>Which slot order the top-3 leaderboard rows are instantiated in.</summary>
public enum LeaderboardRowOrder
{
    Podium,     // 2nd, 1st, 3rd (current rank-1 centered)
    Ascending,  // 1st, 2nd, 3rd (left to right)
    Descending, // 3rd, 2nd, 1st (left to right)
}

public class RoundResultPanel : MonoBehaviour
{
    [Header("Win / Lose Images")]
    public GameObject youWinImage;
    public GameObject youLoseImage;

    [Header("Won Food Image")]
    public Image wonFoodImage;
    
    [Header("Title Result Food Image")]
    [Tooltip("Small food icon next to the Round Results text.")]
    public Image titleFoodImage;
    
    [Header("Won Food Background Animation")]
    [Tooltip("Background Image behind the won-food icon that will be animated.")]
    public Image wonFoodBgImage;
    [Tooltip("Sprites list for the background rotation/glowing animation.")]
    public Sprite[] wonFoodBgSprites;
    [Tooltip("Frame rate of the background animation.")]
    public float bgAnimationFps = 12f;

    [Header("Earnings Amount")]
    [Tooltip("Text field showing the player's total earnings in this round.")]
    public Text wonAmountText;

    [Header("Bet Amount")]
    [Tooltip("Text field showing the player's total bet in this round.")]
    public Text betAmountText;

    [Header("Round Number")]
    [Tooltip("Text component showing the round number.")]
    public Text roundNumberText;



    // --- NEW: Leaderboard Rows ---
    [Header("Leaderboard Rows")]
    public RoundResultPodiumRow rowPrefab;
    public Transform rowsContainer;
    public LeaderboardRowOrder rowOrder = LeaderboardRowOrder.Podium;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Close Timer")]
    [Tooltip("Countdown shown in place of the close button. Auto-created over the close button when left empty.")]
    public Text closeTimerText;
    [Tooltip("Format for the remaining seconds, e.g. \"{0}S\" or \"({0}S)\".")]
    public string closeTimerFormat = "{0}S";
    [Tooltip("Font size used when the countdown text is auto-created.")]
    public int closeTimerFontSize = 22;
    [Tooltip("Colour used when the countdown text is auto-created.")]
    public Color closeTimerColor = Color.white;

    [Header("Timing")]
    public float holdDuration  = 2.5f;

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

    [Header("Dynamic Script Close Button Configuration")]
    public Vector2 closeButtonSize = new Vector2(40f, 40f);
    public Color closeButtonColor = new Color(0.8f, 0.25f, 0.25f, 1f); // Reddish

    [Header("Win / Lose Verdict Sprites")]
    public Sprite winVerdictSprite;
    public Sprite loseVerdictSprite;

    [Header("Highlighted Row Configuration")]
    public Sprite highlightedRowSprite;
    public Color highlightedRowColor = new Color(0.1f, 0.22f, 0.45f, 1f);

    CanvasGroup   canvasGroup;
    RectTransform panelRect;
    bool          isShowing = false;
    Coroutine     activeSequence;
    Coroutine     bgAnimRoutine;

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
        // BuildWinLoseUI();
 
        // Start fully hidden but GameObject stays active so coroutines run.
        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
        if (panelRect != null) panelRect.localScale = Vector3.zero;
 
        EnsureCloseTimer();

        HideResultImages();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// The panel auto-closes on a timer, so the close button is replaced by a countdown label.
    /// The label is created as a sibling of the button (not a child) so hiding the button keeps it visible.
    /// </summary>
    void EnsureCloseTimer()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.gameObject.SetActive(false);
        }

        if (closeTimerText != null) return;

        Transform parent = (closeButton != null) ? closeButton.transform.parent : transform;
        Transform existing = FindAliveChild(parent, "CloseTimerText");
        if (existing != null)
        {
            closeTimerText = existing.GetComponent<Text>();
            if (closeTimerText != null) return;
        }

        GameObject timerObj = new GameObject("CloseTimerText");
        timerObj.transform.SetParent(parent, false);

        RectTransform r = timerObj.AddComponent<RectTransform>();
        RectTransform src = (closeButton != null) ? closeButton.GetComponent<RectTransform>() : null;
        if (src != null)
        {
            r.anchorMin        = src.anchorMin;
            r.anchorMax        = src.anchorMax;
            r.pivot            = src.pivot;
            r.sizeDelta        = src.sizeDelta;
            r.anchoredPosition = src.anchoredPosition;
        }
        else
        {
            r.anchorMin        = new Vector2(1f, 1f);
            r.anchorMax        = new Vector2(1f, 1f);
            r.pivot            = new Vector2(1f, 1f);
            r.sizeDelta        = closeButtonSize;
            r.anchoredPosition = new Vector2(-40f, -40f);
        }

        LayoutElement le = timerObj.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        closeTimerText = timerObj.AddComponent<Text>();
        closeTimerText.font      = GetBuiltinFont();
        closeTimerText.fontSize  = closeTimerFontSize;
        closeTimerText.fontStyle = FontStyle.Bold;
        closeTimerText.alignment = TextAnchor.MiddleCenter;
        closeTimerText.color     = closeTimerColor;
        closeTimerText.raycastTarget = false;
        closeTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        closeTimerText.verticalOverflow   = VerticalWrapMode.Overflow;
        closeTimerText.text = "";

        Shadow shadow = timerObj.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);
    }

    void SetCloseTimerSeconds(float secondsRemaining)
    {
        if (closeTimerText == null) return;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        closeTimerText.text = string.Format(closeTimerFormat, seconds);
    }

    [ContextMenu("Build Round Result UI")]
    public void BuildRoundResultUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Round Result UI");
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
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
        }

        // 2. Setup Background Image
        Image bgImage = GetComponent<Image>();
        if (bgImage == null)
        {
            bgImage = GetOrAddComponentSafe<Image>(gameObject, "Panel Image", out isNew);
        }
        bgImage.enabled = true;
        bgImage.sprite = panelBackgroundSprite;
        bgImage.color = panelBackgroundColor;
        if (panelBackgroundSprite != null)
        {
            bgImage.type = Image.Type.Sliced;
        }

        // 3. Create Close Button (direct child of panel)
        Transform closeBtnTrans = FindAliveChild(transform, "CloseButton");
        if (closeBtnTrans == null) closeBtnTrans = FindAliveChild(transform, "closeButton");
        if (closeBtnTrans == null)
        {
            GameObject closeBtnObj = CreateGameObjectSafe("CloseButton", transform);
            closeBtnTrans = closeBtnObj.transform;
            RectTransform btnRect = GetOrAddComponentSafe<RectTransform>(closeBtnObj, "CloseButton RectTransform", out isNew);
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.sizeDelta = closeButtonSize;
            btnRect.anchoredPosition = new Vector2(-15f, -15f);

            Image btnImage = GetOrAddComponentSafe<Image>(closeBtnObj, "CloseButton Image", out isNew);
            btnImage.color = closeButtonColor;
            
            closeButton = GetOrAddComponentSafe<Button>(closeBtnObj, "CloseButton Button", out isNew);

            GameObject btnTextObj = CreateGameObjectSafe("Text", closeBtnObj.transform);
            RectTransform txtRect = GetOrAddComponentSafe<RectTransform>(btnTextObj, "Text RectTransform", out isNew);
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            Text txt = GetOrAddComponentSafe<Text>(btnTextObj, "Text Text", out isNew);
            txt.text = "X";
            txt.font = GetBuiltinFont();
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }
        else
        {
            closeButton = closeBtnTrans.GetComponent<Button>();
            if (closeButton == null) closeButton = GetOrAddComponentSafe<Button>(closeBtnTrans.gameObject, "CloseButton Button", out isNew);
        }

        // 4. Create Overall(Vertical) container
        Transform overallTrans = FindAliveChild(transform, "Overall(Vertical)");
        if (overallTrans == null) overallTrans = FindAliveChild(transform, "Overall");
        if (overallTrans == null)
        {
            GameObject overallObj = CreateGameObjectSafe("Overall(Vertical)", transform);
            overallTrans = overallObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(overallObj, "Overall RectTransform", out isNew);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(25f, 25f);
            r.offsetMax = new Vector2(-25f, -25f);
        }

        VerticalLayoutGroup overallLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(overallTrans.gameObject, "Overall Layout", out isNew);
        overallLayout.spacing = 15f;
        overallLayout.childControlWidth = true;
        overallLayout.childControlHeight = false;
        overallLayout.childForceExpandWidth = true;
        overallLayout.childForceExpandHeight = false;

        // 5. Create Top(Horizontal) inside Overall(Vertical)
        Transform topTrans = FindAliveChild(overallTrans, "Top(Horizontal)");
        if (topTrans == null) topTrans = FindAliveChild(overallTrans, "Top");
        if (topTrans == null)
        {
            GameObject topObj = CreateGameObjectSafe("Top(Horizontal)", overallTrans);
            topTrans = topObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(topObj, "Top RectTransform", out isNew);
            r.sizeDelta = new Vector2(0f, 120f); // preferred height
        }

        HorizontalLayoutGroup topLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(topTrans.gameObject, "Top Layout", out isNew);
        topLayout.spacing = 15f;
        topLayout.childControlWidth = false;
        topLayout.childControlHeight = true;
        topLayout.childForceExpandWidth = false;
        topLayout.childForceExpandHeight = true;

        LayoutElement topLayoutElement = GetOrAddComponentSafe<LayoutElement>(topTrans.gameObject, "Top LayoutElement", out isNew);
        topLayoutElement.preferredHeight = 120f;

        // 5a. Create WonFoodBgImage inside Top(Horizontal)
        Transform bgImgTrans = FindAliveChild(topTrans, "WonFoodBgImage");
        if (bgImgTrans == null)
        {
            GameObject bgObj = CreateGameObjectSafe("WonFoodBgImage", topTrans);
            bgImgTrans = bgObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(bgObj, "WonFoodBgImage RectTransform", out isNew);
            r.sizeDelta = new Vector2(90f, 90f);

            wonFoodBgImage = GetOrAddComponentSafe<Image>(bgObj, "WonFoodBgImage Image", out isNew);
            wonFoodBgImage.preserveAspect = true;

            LayoutElement le = GetOrAddComponentSafe<LayoutElement>(bgObj, "WonFoodBgImage LayoutElement", out isNew);
            le.ignoreLayout = true; // overlapping behind WonFoodImage
        }
        else
        {
            wonFoodBgImage = bgImgTrans.GetComponent<Image>();
        }

        // 5b. Create WonFoodImage inside Top(Horizontal)
        Transform foodImgTrans = FindAliveChild(topTrans, "WonFoodImage");
        if (foodImgTrans == null)
        {
            GameObject foodObj = CreateGameObjectSafe("WonFoodImage", topTrans);
            foodImgTrans = foodObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(foodObj, "WonFoodImage RectTransform", out isNew);
            r.sizeDelta = new Vector2(70f, 70f);

            wonFoodImage = GetOrAddComponentSafe<Image>(foodObj, "WonFoodImage Image", out isNew);
            wonFoodImage.preserveAspect = true;
        }
        else
        {
            wonFoodImage = foodImgTrans.GetComponent<Image>();
        }

        // 5c. Create HighlightedRow(Vertical) inside Top(Horizontal)
        Transform hrTrans = FindAliveChild(topTrans, "HighlightedRow(Vertical)");
        if (hrTrans == null) hrTrans = FindAliveChild(topTrans, "HighlightedRow");
        if (hrTrans == null)
        {
            GameObject hrObj = CreateGameObjectSafe("HighlightedRow(Vertical)", topTrans);
            hrTrans = hrObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(hrObj, "HighlightedRow RectTransform", out isNew);
            r.sizeDelta = new Vector2(250f, 100f);
        }

        VerticalLayoutGroup hrLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(hrTrans.gameObject, "HighlightedRow Layout", out isNew);
        hrLayout.spacing = 6f;
        hrLayout.childControlWidth = true;
        hrLayout.childControlHeight = true;
        hrLayout.childForceExpandWidth = true;
        hrLayout.childForceExpandHeight = true;

        // 5c1. Create RoundNumberText inside HighlightedRow(Vertical)
        Transform roundTxtTrans = FindAliveChild(hrTrans, "RoundNumberText");
        if (roundTxtTrans == null)
        {
            GameObject roundObj = CreateGameObjectSafe("RoundNumberText", hrTrans);
            roundTxtTrans = roundObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(roundObj, "RoundNumberText RectTransform", out isNew);
            r.sizeDelta = new Vector2(0f, 30f);

            roundNumberText = GetOrAddComponentSafe<Text>(roundObj, "RoundNumberText Text", out isNew);
            roundNumberText.text = "Round 0 Results:";
            roundNumberText.font = GetBuiltinFont();
            roundNumberText.fontSize = 20;
            roundNumberText.fontStyle = FontStyle.Bold;
            roundNumberText.alignment = TextAnchor.MiddleLeft;
            roundNumberText.color = Color.white;

            Shadow shadow = GetOrAddComponentSafe<Shadow>(roundObj, "RoundNumberText Shadow", out isNew);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
        else
        {
            roundNumberText = roundTxtTrans.GetComponent<Text>();
        }

        // 5c1a. Create TitleFoodImage inside RoundNumberText (centered right side)
        Transform titleFoodImgTrans = FindAliveChild(roundTxtTrans, "TitleFoodImage");
        if (titleFoodImgTrans == null)
        {
            GameObject titleFoodObj = CreateGameObjectSafe("TitleFoodImage", roundTxtTrans);
            titleFoodImgTrans = titleFoodObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(titleFoodObj, "TitleFoodImage RectTransform", out isNew);
            r.anchorMin = new Vector2(1f, 0.5f);
            r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(24f, 24f);
            r.anchoredPosition = new Vector2(10f, 0f); // offset to the right of text

            titleFoodImage = GetOrAddComponentSafe<Image>(titleFoodObj, "TitleFoodImage Image", out isNew);
            titleFoodImage.preserveAspect = true;
        }
        else
        {
            titleFoodImage = titleFoodImgTrans.GetComponent<Image>();
        }

        // 5c2. Create WonAmountText inside HighlightedRow(Vertical)
        Transform amountTxtTrans = FindAliveChild(hrTrans, "WonAmountText");
        if (amountTxtTrans == null)
        {
            GameObject amountObj = CreateGameObjectSafe("WonAmountText", hrTrans);
            amountTxtTrans = amountObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(amountObj, "WonAmountText RectTransform", out isNew);
            r.sizeDelta = new Vector2(0f, 25f);

            wonAmountText = GetOrAddComponentSafe<Text>(amountObj, "WonAmountText Text", out isNew);
            wonAmountText.font = GetBuiltinFont();
            wonAmountText.fontSize = 18;
            wonAmountText.fontStyle = FontStyle.Bold;
            wonAmountText.alignment = TextAnchor.MiddleLeft;
            wonAmountText.color = new Color(1f, 0.84f, 0f, 1f);
            wonAmountText.text = "0";

            Shadow shadow = GetOrAddComponentSafe<Shadow>(amountObj, "WonAmountText Shadow", out isNew);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
        else
        {
            wonAmountText = amountTxtTrans.GetComponent<Text>();
        }

        // 5c3. Create BetAmountText inside HighlightedRow(Vertical)
        Transform betTxtTrans = FindAliveChild(hrTrans, "BetAmountText");
        if (betTxtTrans == null)
        {
            GameObject betObj = CreateGameObjectSafe("BetAmountText", hrTrans);
            betTxtTrans = betObj.transform;
            RectTransform r = GetOrAddComponentSafe<RectTransform>(betObj, "BetAmountText RectTransform", out isNew);
            r.sizeDelta = new Vector2(0f, 25f);

            betAmountText = GetOrAddComponentSafe<Text>(betObj, "BetAmountText Text", out isNew);
            betAmountText.font = GetBuiltinFont();
            betAmountText.fontSize = 18;
            betAmountText.fontStyle = FontStyle.Bold;
            betAmountText.alignment = TextAnchor.MiddleLeft;
            betAmountText.color = new Color(1f, 0.84f, 0f, 1f);
            betAmountText.text = "0";

            Shadow shadow = GetOrAddComponentSafe<Shadow>(betObj, "BetAmountText Shadow", out isNew);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
        else
        {
            betAmountText = betTxtTrans.GetComponent<Text>();
        }

        // 6. Create ScrollView inside Overall(Vertical)
        Transform scrollTrans = FindAliveChild(overallTrans, "ScrollView");
        if (scrollTrans == null)
        {
            // ScrollView GameObject
            GameObject scrollViewObj = CreateGameObjectSafe("ScrollView", overallTrans);
            scrollTrans = scrollViewObj.transform;
            RectTransform scrollRectTransform = GetOrAddComponentSafe<RectTransform>(scrollViewObj, "ScrollView RectTransform", out isNew);
            scrollRectTransform.sizeDelta = new Vector2(0f, 300f); // preferred list height

            ScrollRect scrollRect = GetOrAddComponentSafe<ScrollRect>(scrollViewObj, "ScrollView ScrollRect", out isNew);
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            Image scrollBg = GetOrAddComponentSafe<Image>(scrollViewObj, "ScrollView Image", out isNew);
            scrollBg.color = new Color(0f, 0f, 0f, 0.2f);

            // Viewport GameObject
            GameObject viewportObj = CreateGameObjectSafe("Viewport", scrollTrans);
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
            GameObject contentObj = CreateGameObjectSafe("Content", viewportRect.transform);
            RectTransform contentRect = GetOrAddComponentSafe<RectTransform>(contentObj, "Content RectTransform", out isNew);
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup hLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(contentObj, "Content HorizontalLayoutGroup", out isNew);
            hLayout.spacing = 15f;
            hLayout.padding = new RectOffset(10, 10, 10, 10);
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;

            ContentSizeFitter sizeFitter = GetOrAddComponentSafe<ContentSizeFitter>(contentObj, "Content ContentSizeFitter", out isNew);
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            rowsContainer = contentRect.transform;
        }
        else
        {
            Transform viewport = FindAliveChild(scrollTrans, "Viewport");
            if (viewport != null)
            {
                Transform content = FindAliveChild(viewport, "Content");
                if (content != null)
                {
                    rowsContainer = content;
                }
            }
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

    // playerWon/wonAmount only matter when `participated` is true (the player
    // placed a bet). When `participated` is false the board still shows the
    // round's winner + leaderboard, but no YOU WIN / YOU LOSE verdict.
    public Coroutine ShowAndWait(bool playerWon, int winnerIndex = -1, bool isCombo = false, List<LeaderboardEntry> entries = null, long wonAmount = 0, bool participated = true, long betAmount = 0, int roundNumber = 0)
    {
        gameObject.SetActive(true);

        if (activeSequence != null)
            StopCoroutine(activeSequence);

        activeSequence = StartCoroutine(ShowSequence(playerWon, winnerIndex, isCombo, entries, wonAmount, participated, betAmount, roundNumber));
        return activeSequence;
    }

    public void Hide()
    {
        isShowing = false;
    }

    IEnumerator ShowSequence(bool playerWon, int winnerIndex, bool isCombo, List<LeaderboardEntry> entries, long wonAmount, bool participated, long betAmount, int roundNumber)
    {
        InitializeIfNeeded();
        EnsureCloseTimer();
        isShowing = true;

        SetCloseTimerSeconds(holdDuration);
        if (closeTimerText != null) closeTimerText.gameObject.SetActive(true);

        if (bgAnimRoutine != null) StopCoroutine(bgAnimRoutine);
        bgAnimRoutine = StartCoroutine(AnimateWonFoodBg());

        if (roundNumberText != null)
        {
            roundNumberText.text = $"Round {roundNumber} Results:";
        }

        // Keep win/lose verdict overlays disabled — using a single unified panel
        if (youWinImage  != null) youWinImage.SetActive(false);
        if (youLoseImage != null) youLoseImage.SetActive(false);

        // Earnings Text (wonAmountText):
        if (wonAmountText != null)
        {
            if (participated && playerWon && wonAmount > 0)
            {
                wonAmountText.text = FormatCoins(wonAmount);
                wonAmountText.color = new Color(1f, 0.84f, 0f, 1f); // Gold
            }
            else
            {
                wonAmountText.text = "0";
                wonAmountText.color = new Color(0.85f, 0.85f, 0.85f, 1f); // Greyish white
            }
        }

        // Bet Text (betAmountText):
        if (betAmountText != null)
        {
            if (participated && betAmount > 0)
            {
                betAmountText.text = FormatCoins(betAmount);
                betAmountText.color = new Color(0.85f, 0.85f, 0.85f, 1f); // Greyish white
            }
            else
            {
                betAmountText.text = "0";
                betAmountText.color = new Color(0.85f, 0.85f, 0.85f, 1f); // Greyish white
            }
        }

        if (wonFoodImage != null)
        {
            Sprite foodSprite = null;

            if (isCombo)
            {
                if (GameManager.Instance != null)
                {
                    foodSprite = GameManager.Instance.GetComboSpriteByIndex(winnerIndex);
                }
            }
            else
            {
                if (GameManager.Instance != null)
                {
                    foodSprite = GameManager.Instance.GetFoodSpriteByIndex(winnerIndex);
                }
            }

            if (foodSprite != null)
            {
                wonFoodImage.sprite = foodSprite;
                wonFoodImage.gameObject.SetActive(true);
                if (titleFoodImage != null)
                {
                    titleFoodImage.sprite = foodSprite;
                    titleFoodImage.gameObject.SetActive(true);
                }
            }
            else
            {
                wonFoodImage.gameObject.SetActive(false);
                if (titleFoodImage != null)
                {
                    titleFoodImage.gameObject.SetActive(false);
                }
            }
        }

        if (rowsContainer != null)
        {
            foreach (Transform child in rowsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (rowPrefab != null && rowsContainer != null && entries != null && entries.Count > 0)
        {
            List<LeaderboardEntry> displayOrder = BuildDisplayOrder(entries);

            foreach (var entry in displayOrder)
            {
                RoundResultPodiumRow rowInstance = Instantiate(rowPrefab, rowsContainer);
                rowInstance.SetData(entry.rank, entry.username, entry.wonAmount, entry.avatar);
            }
        }

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

        yield return new WaitForSeconds(openDuration);

        float elapsed = 0f;
        while (isShowing && elapsed < holdDuration)
        {
            SetCloseTimerSeconds(holdDuration - elapsed);
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetCloseTimerSeconds(0f);

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
        activeSequence       = null;

        HideResultImages();
        gameObject.SetActive(false);
    }

    /// <summary>Arranges the top-3 entries left-to-right per `rowOrder` (only the first 3 are shown).</summary>
    List<LeaderboardEntry> BuildDisplayOrder(List<LeaderboardEntry> entries)
    {
        var result = new List<LeaderboardEntry>();
        switch (rowOrder)
        {
            case LeaderboardRowOrder.Ascending:
                for (int i = 0; i < entries.Count && i < 3; i++) result.Add(entries[i]);
                break;

            case LeaderboardRowOrder.Descending:
                for (int i = Mathf.Min(2, entries.Count - 1); i >= 0; i--) result.Add(entries[i]);
                break;

            case LeaderboardRowOrder.Podium:
            default:
                if (entries.Count >= 2) result.Add(entries[1]); // 2nd
                result.Add(entries[0]);                          // 1st
                if (entries.Count >= 3) result.Add(entries[2]); // 3rd
                break;
        }
        return result;
    }

    void HideResultImages()
    {
        if (bgAnimRoutine != null)
        {
            StopCoroutine(bgAnimRoutine);
            bgAnimRoutine = null;
        }
        if (wonFoodBgImage != null) wonFoodBgImage.gameObject.SetActive(false);
        if (youWinImage  != null) youWinImage.SetActive(false);
        if (youLoseImage != null) youLoseImage.SetActive(false);
        if (wonFoodImage != null) wonFoodImage.gameObject.SetActive(false);
        if (titleFoodImage != null) titleFoodImage.gameObject.SetActive(false);
        if (wonAmountText != null) wonAmountText.text = "";
        if (betAmountText != null) betAmountText.text = "";
        if (roundNumberText != null) roundNumberText.text = "";
        if (closeTimerText != null) closeTimerText.text = "";
    }

    IEnumerator AnimateWonFoodBg()
    {
        if (wonFoodBgImage == null || wonFoodBgSprites == null || wonFoodBgSprites.Length == 0)
        {
            if (wonFoodBgImage != null) wonFoodBgImage.gameObject.SetActive(false);
            yield break;
        }

        wonFoodBgImage.gameObject.SetActive(true);
        int frame = 0;
        float delay = (bgAnimationFps > 0f) ? (1f / bgAnimationFps) : 0.083f; // Default to ~12 FPS if 0

        while (isShowing)
        {
            wonFoodBgImage.sprite = wonFoodBgSprites[frame];
            frame = (frame + 1) % wonFoodBgSprites.Length;
            yield return new WaitForSeconds(delay);
        }
    }

    private Transform FindAliveChild(Transform parent, string name)
    {
        if (parent == null || !parent) return null;
        try
        {
            var pGo = parent.gameObject;
            if (pGo == null || !pGo) return null;
        }
        catch
        {
            return null;
        }

        Transform child = parent.Find(name);
        if (child == null || !child) return null;
        try
        {
            var cGo = child.gameObject;
            if (cGo == null || !cGo) return null;
        }
        catch
        {
            return null;
        }
        return child;
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

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000)     return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}