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
    public LeaderboardRowOrder rowOrder = LeaderboardRowOrder.Ascending;

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
    public Vector2 leaderboardSize = new Vector2(780f, 966f);
    public Vector2 leaderboardPosition = Vector2.zero;
    public Sprite panelBackgroundSprite;
    public Color panelBackgroundColor = Color.white;

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
        Color ink = new Color(0.36f, 0.16f, 0.07f, 1f);
        Color headerTan = new Color(0.662f, 0.616f, 0.498f, 1f);
        Font font = GetBuiltinFont();

        panelRect = GetComponent<RectTransform>();
        if (panelRect == null)
            panelRect = GetOrAddComponentSafe<RectTransform>(gameObject, "Panel RectTransform", out isNew);

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = leaderboardSize;
        panelRect.anchoredPosition = leaderboardPosition;
        panelRect.localScale = Vector3.one;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = GetOrAddComponentSafe<CanvasGroup>(gameObject, "CanvasGroup", out isNew);

        Image bgImage = GetComponent<Image>();
        if (bgImage == null) bgImage = GetOrAddComponentSafe<Image>(gameObject, "Panel Image", out isNew);
        bgImage.enabled = true;
#if UNITY_EDITOR
        if (panelBackgroundSprite == null)
        {
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/RoundResultPanel.png");
            for (int i = 0; i < assets.Length; i++)
            {
                Sprite s = assets[i] as Sprite;
                if (s != null) { panelBackgroundSprite = s; break; }
            }
        }
#endif
        bgImage.sprite = panelBackgroundSprite;
        bgImage.color = Color.white;
        bgImage.type = Image.Type.Simple;
        bgImage.preserveAspect = true;
        bgImage.raycastTarget = true;

        DestroyNamedChild(transform, "Overal(Vertical)");
        DestroyNamedChild(transform, "Overall(Vertical)");
        DestroyNamedChild(transform, "Overall");
        DestroyNamedChild(transform, "RibbonTitle");
        DestroyNamedChild(transform, "TitleText");
        DestroyNamedChild(transform, "HeaderRow");
        DestroyNamedChild(transform, "RowsContainer");

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
            btnRect.anchoredPosition = new Vector2(-18f, -18f);

            Image btnImage = GetOrAddComponentSafe<Image>(closeBtnObj, "CloseButton Image", out isNew);
            btnImage.color = closeButtonColor;
            closeButton = GetOrAddComponentSafe<Button>(closeBtnObj, "CloseButton Button", out isNew);
        }
        else
        {
            closeButton = closeBtnTrans.GetComponent<Button>();
            if (closeButton == null) closeButton = GetOrAddComponentSafe<Button>(closeBtnTrans.gameObject, "CloseButton Button", out isNew);
        }
        closeBtnTrans.gameObject.SetActive(false);

        // Title sits on the yellow ribbon baked into the panel sprite.
        GameObject titleObj = CreateGameObjectSafe("RibbonTitle", transform);
        RectTransform titleRect = GetOrAddComponentSafe<RectTransform>(titleObj, "Title Rect", out isNew);
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(560f, 52f);
        titleRect.anchoredPosition = new Vector2(0f, -62f);
        roundNumberText = GetOrAddComponentSafe<Text>(titleObj, "Title Text", out isNew);
        roundNumberText.text = "Ranking in this Round";
        roundNumberText.font = font;
        roundNumberText.fontSize = 28;
        roundNumberText.fontStyle = FontStyle.Bold;
        roundNumberText.alignment = TextAnchor.MiddleCenter;
        roundNumberText.color = ink;
        roundNumberText.horizontalOverflow = HorizontalWrapMode.Overflow;
        roundNumberText.verticalOverflow = VerticalWrapMode.Overflow;
        roundNumberText.raycastTarget = false;

        GameObject overallObj = CreateGameObjectSafe("Overall(Vertical)", transform);
        RectTransform overallRect = GetOrAddComponentSafe<RectTransform>(overallObj, "Overall Rect", out isNew);
        overallRect.anchorMin = Vector2.zero;
        overallRect.anchorMax = Vector2.one;
        overallRect.pivot = new Vector2(0.5f, 0.5f);
        // Inset to the inner beige of RoundResultPanel.png (7% sides, 5.5% bottom, 17.5% top).
        overallRect.offsetMin = new Vector2(leaderboardSize.x * 0.07f, leaderboardSize.y * 0.055f);
        overallRect.offsetMax = new Vector2(-leaderboardSize.x * 0.07f, -leaderboardSize.y * 0.175f);

        VerticalLayoutGroup overallLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(overallObj, "Overall Layout", out isNew);
        overallLayout.padding = new RectOffset(0, 0, 0, 12);
        overallLayout.spacing = 0f;
        overallLayout.childAlignment = TextAnchor.UpperCenter;
        overallLayout.childControlWidth = true;
        overallLayout.childControlHeight = false;
        overallLayout.childForceExpandWidth = true;
        overallLayout.childForceExpandHeight = false;

        GameObject headerObj = CreateGameObjectSafe("HeaderRow", overallObj.transform);
        RectTransform headerRect = GetOrAddComponentSafe<RectTransform>(headerObj, "Header Rect", out isNew);
        headerRect.sizeDelta = new Vector2(0f, 52f);
        LayoutElement headerLe = GetOrAddComponentSafe<LayoutElement>(headerObj, "Header LE", out isNew);
        headerLe.minHeight = 52f;
        headerLe.preferredHeight = 52f;
        Image headerBg = GetOrAddComponentSafe<Image>(headerObj, "Header Image", out isNew);
        headerBg.color = headerTan;
        headerBg.raycastTarget = false;

        HorizontalLayoutGroup headerLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(headerObj, "Header Layout", out isNew);
        headerLayout.padding = new RectOffset(18, 12, 0, 0);
        headerLayout.spacing = 8f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = false;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;

        GameObject playerLabelObj = CreateGameObjectSafe("PlayerLabel", headerObj.transform);
        RectTransform playerLabelRect = GetOrAddComponentSafe<RectTransform>(playerLabelObj, "PlayerLabel Rect", out isNew);
        playerLabelRect.sizeDelta = new Vector2(140f, 40f);
        LayoutElement playerLe = GetOrAddComponentSafe<LayoutElement>(playerLabelObj, "PlayerLabel LE", out isNew);
        playerLe.flexibleWidth = 1f;
        playerLe.minWidth = 80f;
        Text playerLabel = GetOrAddComponentSafe<Text>(playerLabelObj, "PlayerLabel Text", out isNew);
        playerLabel.text = "Player";
        playerLabel.font = font;
        playerLabel.fontSize = 24;
        playerLabel.fontStyle = FontStyle.Bold;
        playerLabel.alignment = TextAnchor.MiddleLeft;
        playerLabel.color = ink;
        playerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        playerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        playerLabel.raycastTarget = false;

        GameObject dividerObj = CreateGameObjectSafe("HeaderDivider", headerObj.transform);
        RectTransform dividerRect = GetOrAddComponentSafe<RectTransform>(dividerObj, "Divider Rect", out isNew);
        dividerRect.sizeDelta = new Vector2(3f, 22f);
        LayoutElement dividerLe = GetOrAddComponentSafe<LayoutElement>(dividerObj, "Divider LE", out isNew);
        dividerLe.minWidth = 3f;
        dividerLe.preferredWidth = 3f;
        dividerLe.preferredHeight = 22f;
        Image dividerImg = GetOrAddComponentSafe<Image>(dividerObj, "Divider Image", out isNew);
        dividerImg.color = ink;
        dividerImg.raycastTarget = false;

        GameObject winnersLabelObj = CreateGameObjectSafe("WinnersLabel", headerObj.transform);
        RectTransform winnersLabelRect = GetOrAddComponentSafe<RectTransform>(winnersLabelObj, "WinnersLabel Rect", out isNew);
        winnersLabelRect.sizeDelta = new Vector2(210f, 40f);
        LayoutElement winnersLe = GetOrAddComponentSafe<LayoutElement>(winnersLabelObj, "WinnersLabel LE", out isNew);
        winnersLe.minWidth = 210f;
        winnersLe.preferredWidth = 210f;
        Text winnersLabel = GetOrAddComponentSafe<Text>(winnersLabelObj, "WinnersLabel Text", out isNew);
        winnersLabel.text = "Winners";
        winnersLabel.font = font;
        winnersLabel.fontSize = 24;
        winnersLabel.fontStyle = FontStyle.Bold;
        winnersLabel.alignment = TextAnchor.MiddleLeft;
        winnersLabel.color = ink;
        winnersLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        winnersLabel.verticalOverflow = VerticalWrapMode.Overflow;
        winnersLabel.raycastTarget = false;

        GameObject rowsObj = CreateGameObjectSafe("RowsContainer", overallObj.transform);
        RectTransform rowsRect = GetOrAddComponentSafe<RectTransform>(rowsObj, "Rows Rect", out isNew);
        rowsRect.sizeDelta = new Vector2(0f, 0f);
        LayoutElement rowsLe = GetOrAddComponentSafe<LayoutElement>(rowsObj, "Rows LE", out isNew);
        rowsLe.flexibleHeight = 1f;
        rowsLe.minHeight = 280f;
        VerticalLayoutGroup rowsLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(rowsObj, "Rows Layout", out isNew);
        rowsLayout.padding = new RectOffset(10, 6, 18, 18);
        rowsLayout.spacing = 22f;
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = false;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        rowsContainer = rowsObj.transform;

        HideLegacyResultChrome();

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

    private void DestroyNamedChild(Transform parent, string name)
    {
        Transform child = FindAliveChild(parent, name);
        if (child != null) DestroyObjectSafe(child.gameObject);
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
        bgAnimRoutine = null;

        if (roundNumberText != null)
        {
            roundNumberText.text = "Ranking in this Round";
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
                if (titleFoodImage != null) titleFoodImage.sprite = foodSprite;
            }

            HideLegacyResultChrome();
        }

        HideLegacyResultChrome();

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
        if (roundNumberText != null) roundNumberText.text = "Ranking in this Round";
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

    void HideLegacyResultChrome()
    {
        SetAliveActive("Top(Horizontal)", false);
        SetAliveActive("WonFoodBgImage", false);
        SetAliveActive("WonFoodImage", false);
        SetAliveActive("HighlightedRow(Vertical)", false);
        if (wonFoodBgImage != null) wonFoodBgImage.gameObject.SetActive(false);
        if (wonFoodImage != null) wonFoodImage.gameObject.SetActive(false);
        if (titleFoodImage != null) titleFoodImage.gameObject.SetActive(false);
    }

    void SetAliveActive(string childName, bool active)
    {
        Transform child = FindAliveChild(transform, childName);
        if (child != null) child.gameObject.SetActive(active);
    }

    static string FormatCoins(long v)
    {
        return v.ToString();
    }
}