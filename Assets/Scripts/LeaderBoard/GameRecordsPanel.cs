using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GameRecordsPanel : MonoBehaviour
{
    [Header("Rows Container")]
    public Transform rowsContainer;

    [Header("Buttons")]
    public Button closeButton;

    [Header("Status Text Reference")]
    public Text statusMessageText;

    [Header("Timing")]
    public float openDuration  = 0.4f;
    public Ease  openEase      = Ease.OutBack;
    public float closeDuration = 0.3f;
    public Ease  closeEase     = Ease.InBack;

    [Header("Dynamic Script Layout Configuration")]
    public Sprite panelBackgroundSprite;
    public Color panelBackgroundColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);

    [Header("Dynamic Script Close Button Configuration")]
    public Vector2 closeButtonSize = new Vector2(40f, 40f);
    public Color closeButtonColor = new Color(0.8f, 0.25f, 0.25f, 1f); // Reddish

    [Header("Card Prefab")]
    public GameRecordCard cardPrefab;

    private CanvasGroup   canvasGroup;
    private RectTransform panelRect;
    private bool          isShowing = false;
    private Coroutine     activeSequence;

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
        // BuildGameRecordsUI();
 
        // Start fully hidden
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

    [ContextMenu("Build Game Records UI")]
    public void BuildGameRecordsUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Game Records UI");
        }
#endif

        bool isNew;

        // 1. Setup the Panel's RectTransform
        panelRect = GetOrAddComponentSafe<RectTransform>(gameObject, "Panel RectTransform", out isNew);

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

        // 3. Create Title Header Pill ("Game records" title banner at the top center)
        Transform titleTransform = transform.Find("HeaderBanner");
        if (titleTransform == null)
        {
            GameObject headerObj = CreateGameObjectSafe("HeaderBanner", transform);
            titleTransform = headerObj.transform;

            RectTransform headerRect = GetOrAddComponentSafe<RectTransform>(headerObj, "HeaderBanner RectTransform", out isNew);
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.sizeDelta = new Vector2(300f, 65f);
            headerRect.anchoredPosition = new Vector2(0f, 0f);

            Image headerImage = GetOrAddComponentSafe<Image>(headerObj, "HeaderBanner Image", out isNew);
            headerImage.color = new Color(0.02f, 0.2f, 0.4f, 1f); // Dark blue banner

            GameObject btnTextObj = CreateGameObjectSafe("Text", headerObj.transform);
            RectTransform txtRect = GetOrAddComponentSafe<RectTransform>(btnTextObj, "Text RectTransform", out isNew);
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            Text txt = GetOrAddComponentSafe<Text>(btnTextObj, "Text Text", out isNew);
            txt.text = "Game records";
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
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
            scrollRectTransform.offsetMax = new Vector2(-25f, -60f);

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
            vLayout.spacing = 15f;
            vLayout.padding = new RectOffset(10, 10, 15, 15);
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

        // Status Message Text (for Loading / No records)
        Transform statusTextTrans = transform.Find("StatusMessageText");
        if (statusTextTrans == null)
        {
            GameObject statusObj = CreateGameObjectSafe("StatusMessageText", transform);
            RectTransform sRect = GetOrAddComponentSafe<RectTransform>(statusObj, "StatusMessageText RectTransform", out isNew);
            sRect.anchorMin = new Vector2(0.5f, 0.5f);
            sRect.anchorMax = new Vector2(0.5f, 0.5f);
            sRect.pivot = new Vector2(0.5f, 0.5f);
            sRect.anchoredPosition = Vector2.zero;
            sRect.sizeDelta = new Vector2(400f, 50f);

            statusMessageText = GetOrAddComponentSafe<Text>(statusObj, "StatusMessageText Text", out isNew);
            statusMessageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusMessageText.fontSize = 22;
            statusMessageText.alignment = TextAnchor.MiddleCenter;
            statusMessageText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            statusMessageText.text = "";
        }
        else
        {
            statusMessageText = GetOrAddComponentSafe<Text>(statusTextTrans.gameObject, "StatusMessageText Text", out isNew);
        }

        // 5. Create Close Button (if closeButton is null)
        Transform closeBtnTrans = transform.Find("closeButton");
        if (closeBtnTrans == null) closeBtnTrans = transform.Find("CloseButton");
        
        if (closeBtnTrans == null)
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
            
            closeButton = GetOrAddComponentSafe<Button>(closeBtnObj, "CloseButton Button", out isNew);

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
        else
        {
            closeButton = closeBtnTrans.GetComponent<Button>();
            if (closeButton == null) closeButton = GetOrAddComponentSafe<Button>(closeBtnTrans.gameObject, "CloseButton Button", out isNew);
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

    public void Open()
    {
        gameObject.SetActive(true);
        InitializeIfNeeded();
        isShowing = true;

        // Clear container and show Loading status
        ClearContainer();
        if (statusMessageText != null)
        {
            statusMessageText.text = "Loading...";
            statusMessageText.gameObject.SetActive(true);
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
    }

    public void Populate(List<GameRecord> records)
    {
        ClearContainer();
        
        if (statusMessageText != null)
        {
            statusMessageText.gameObject.SetActive(false);
        }

        if (records != null)
        {
            Debug.Log($"[GameRecordsPanel] Populating {records.Count} game records.");
        }

        if (cardPrefab != null && rowsContainer != null && records != null)
        {
            if (records.Count == 0)
            {
                if (statusMessageText != null)
                {
                    statusMessageText.text = "No records found";
                    statusMessageText.gameObject.SetActive(true);
                }
            }
            else
            {
                foreach (var record in records)
                {
                    GameRecordCard cardInstance = Instantiate(cardPrefab, rowsContainer);
                    cardInstance.gameObject.SetActive(true);
                    cardInstance.SetData(record);
                }
            }
        }
    }

    public void Hide()
    {
        isShowing = false;

        if (activeSequence != null) StopCoroutine(activeSequence);
        activeSequence = StartCoroutine(HideSequence());
    }

    IEnumerator HideSequence()
    {
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        panelRect.DOKill();
        canvasGroup.DOKill();

        panelRect.DOScale(Vector3.zero, closeDuration).SetEase(closeEase);
        canvasGroup.DOFade(0f, closeDuration);

        yield return new WaitForSeconds(closeDuration);

        canvasGroup.alpha    = 0f;
        panelRect.localScale = Vector3.zero;
        activeSequence       = null;
        gameObject.SetActive(false);
    }

    private void ClearContainer()
    {
        if (rowsContainer != null)
        {
            foreach (Transform child in rowsContainer)
            {
                if (cardPrefab != null && child.gameObject == cardPrefab.gameObject)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }
                Destroy(child.gameObject);
            }
        }
    }
}
