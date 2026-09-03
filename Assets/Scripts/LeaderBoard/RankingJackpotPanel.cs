using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Zimo.Net;

public class RankingJackpotPanel : MonoBehaviour
{
    [Header("UI Prefabs & Containers")]
    public RankingRow rowPrefab;
    public Transform rowsContainer;
    public RankingRow myRankingRow;

    [Header("Jackpot Display")]
    public long startingJackpot = 123456788;
    public float jackpotTickInterval = 1f;
    public int jackpotMinIncrement = 10;
    public int jackpotMaxIncrement = 50;
    public Text[] jackpotDigitTexts;

    [Header("Timer & Labels")]
    public Text timerText;
    public Text descriptionText;

    [Header("Tab Buttons")]
    public Button todayTabButton;
    public Button yesterdayTabButton;
    public Image todayTabImage;
    public Image yesterdayTabImage;

    [Header("Navigation Buttons")]
    public Button closeButton;

    [Header("Animations")]
    public float openDuration = 0.4f;
    public Ease openEase = Ease.OutBack;
    public float closeDuration = 0.3f;
    public Ease closeEase = Ease.InBack;

    [Header("Design Config (Used for Auto Build)")]
    public Vector2 panelSize = new Vector2(500f, 720f);
    public Color panelColor = new Color(0.015f, 0.09f, 0.2f, 1f);
    public Color textOutlineColor = new Color(0.5f, 0.1f, 0f, 1f);
    public Color activeTabColor = new Color(0f, 0.55f, 0.85f, 1f);
    public Color inactiveTabColor = new Color(0.08f, 0.22f, 0.4f, 1f);

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private bool isShowing = false;
    private bool isTodaySelected = true;
    private long currentJackpot;
    private string playerUsername = "You";
    private DateTime? serverJackpotEndsAt = null;

    private void InitializeIfNeeded()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        if (panelRect == null) panelRect = gameObject.AddComponent<RectTransform>();

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Awake()
    {
        InitializeIfNeeded();

        // Start hidden
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (panelRect != null) panelRect.localScale = Vector3.zero;

        // Register Button clicks
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(HidePanel);
        }

        if (todayTabButton != null)
        {
            todayTabButton.onClick.RemoveAllListeners();
            todayTabButton.onClick.AddListener(() => OnTabClicked(true));
        }

        if (yesterdayTabButton != null)
        {
            yesterdayTabButton.onClick.RemoveAllListeners();
            yesterdayTabButton.onClick.AddListener(() => OnTabClicked(false));
        }

        currentJackpot = startingJackpot;
        UpdateJackpotOdometer();

        gameObject.SetActive(false);
    }

    public void ShowPanel()
    {
        InitializeIfNeeded();
        gameObject.SetActive(true);
        isShowing = true;
        isTodaySelected = true;

        if (currentJackpot <= 0)
        {
            currentJackpot = startingJackpot > 0 ? startingJackpot : 123456788;
        }
        UpdateJackpotOdometer();

        // Animate Panel Open
        panelRect.DOKill();
        canvasGroup.DOKill();
        panelRect.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        panelRect.DOScale(Vector3.one, openDuration).SetEase(openEase);
        canvasGroup.DOFade(1f, openDuration * 0.5f).OnComplete(() =>
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        });

        // Resolve Local Username from GameManager
        if (GameManager.Instance != null && GameManager.Instance.usernameText != null)
        {
            playerUsername = GameManager.Instance.usernameText.text;
        }

        // Fetch local balance to ensure user details are current
        if (GameManager.Instance != null && GameManager.Instance.api != null)
        {
            GameManager.Instance.api.GetBalance(balance =>
            {
                if (balance != null && !string.IsNullOrEmpty(balance.username))
                {
                    playerUsername = balance.username;
                }
                // Refresh list with correct username context
                FetchLeaderboardData();
            });

            // Fetch current jackpot pool value from server
            GameManager.Instance.api.GetJackpot(jackpotInfo =>
            {
                if (jackpotInfo != null)
                {
                    currentJackpot = jackpotInfo.jackpot;
                    UpdateJackpotOdometer();

                    if (!string.IsNullOrEmpty(jackpotInfo.endsAt))
                    {
                        if (DateTime.TryParse(jackpotInfo.endsAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                        {
                            serverJackpotEndsAt = parsed.ToUniversalTime();
                        }
                    }
                }
            });
        }
        else
        {
            FetchLeaderboardData();
        }

        // Start Coroutines
        StartCoroutine(TimerCountdownRoutine());
        StartCoroutine(JackpotIncrementRoutine());
    }

    public void HidePanel()
    {
        if (!isShowing) return;
        isShowing = false;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        panelRect.DOKill();
        canvasGroup.DOKill();

        panelRect.DOScale(Vector3.zero, closeDuration).SetEase(closeEase);
        canvasGroup.DOFade(0f, closeDuration).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void OnTabClicked(bool today)
    {
        if (isTodaySelected == today) return;
        isTodaySelected = today;
        UpdateTabVisuals();
        FetchLeaderboardData();
    }

    private void UpdateTabVisuals()
    {
        // Visual effects for tab selected/unselected are disabled as requested.
    }

    private void FetchLeaderboardData()
    {
        // Clear current entries
        if (rowsContainer != null)
        {
            foreach (Transform child in rowsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (myRankingRow != null)
        {
            myRankingRow.gameObject.SetActive(false);
        }

        var api = GameManager.Instance != null ? GameManager.Instance.api : null;
        if (api == null)
        {
            // Offline/Fallback Demo mode
            PopulateMockData();
            return;
        }

        if (isTodaySelected)
        {
            api.GetLeaderboard(entries =>
            {
                if (entries != null) PopulateList(entries);
                else PopulateMockData();
            }, _ => PopulateMockData());
        }
        else
        {
            api.GetLeaderboardYesterday(entries =>
            {
                if (entries != null) PopulateList(entries);
                else PopulateMockData();
            }, _ => PopulateMockData());
        }
    }

    private void PopulateList(LeaderboardEntryDto[] entries)
    {
        if (rowPrefab == null || rowsContainer == null) return;

        // Instantiate and populate top 15 rows only
        int limit = Mathf.Min(entries.Length, 15);
        for (int i = 0; i < limit; i++)
        {
            RankingRow row = Instantiate(rowPrefab, rowsContainer);
            row.gameObject.SetActive(true);
            row.SetData(entries[i].rank, entries[i].username, entries[i].wonAmount, null, entries[i].avatar);
            
            // Add subtle scaling micro-animation for entrance
            row.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            row.transform.DOScale(Vector3.one, 0.2f).SetDelay(i * 0.03f);
        }

        // Bind the bottom "My Ranking" display
        UpdateMyRanking(entries);
    }

    private void UpdateMyRanking(LeaderboardEntryDto[] entries)
    {
        if (myRankingRow == null) return;

        // Search for user
        LeaderboardEntryDto myEntry = null;
        foreach (var entry in entries)
        {
            if (entry.username == playerUsername || entry.username == "You")
            {
                myEntry = entry;
                break;
            }
        }

        myRankingRow.gameObject.SetActive(true);

        if (myEntry != null)
        {
            // User is ranked in the top list
            myRankingRow.SetData(myEntry.rank, playerUsername, myEntry.wonAmount, null, myEntry.avatar);
        }
        else
        {
            // User is not in the top list
            if (isTodaySelected)
            {
                // Retrieve player's current today win balance from server if available
                long todaysWinCoins = 0;
                if (GameManager.Instance != null && GameManager.Instance.todaysWinTracker != null)
                {
                    // TodaysWinTracker gets value from server BalanceDto.todaysWin
                    todaysWinCoins = GameManager.Instance.todaysWinTracker.TodaysWin;
                }

                // Real avatar of the logged-in player (blank → default sprite).
                string myAvatar = GameManager.Instance != null
                    ? GameManager.Instance.PlayerAvatarUrl
                    : "";

                if (todaysWinCoins > 0)
                {
                    // Player has wins today but not in top 15 (e.g. rank 33+)
                    myRankingRow.SetData(33, playerUsername, todaysWinCoins, null, myAvatar);
                }
                else
                {
                    // Player has no wins today
                    myRankingRow.SetData(0, playerUsername, 0, null, myAvatar);
                    myRankingRow.rankText.text = "-";
                }
            }
            else
            {
                // Yesterday tab, player not in the top list — show their real profile.
                string myAvatar = GameManager.Instance != null
                    ? GameManager.Instance.PlayerAvatarUrl
                    : "";
                myRankingRow.SetData(0, playerUsername, 0, null, myAvatar);
                myRankingRow.rankText.text = "-";
            }
        }
    }

    private void PopulateMockData()
    {
        // Generates default mock list so the layout is populated even offline
        var mockList = new List<LeaderboardEntryDto>();
        for (int i = 1; i <= 10; i++)
        {
            mockList.Add(new LeaderboardEntryDto
            {
                rank = i,
                username = i == 4 ? "You" : $"Player_Repo_{i}",
                wonAmount = isTodaySelected ? (100000000 / i) : (150000000 / i),
                avatar = $"https://picsum.photos/100?random={i}"
            });
        }
        PopulateList(mockList.ToArray());
    }

    private void UpdateJackpotOdometer()
    {
        if (jackpotDigitTexts == null || jackpotDigitTexts.Length == 0) return;

        string valStr = currentJackpot.ToString().PadLeft(jackpotDigitTexts.Length, '0');
        for (int i = 0; i < jackpotDigitTexts.Length; i++)
        {
            if (i < valStr.Length && jackpotDigitTexts[i] != null)
            {
                jackpotDigitTexts[i].text = valStr[i].ToString();
            }
        }
    }

    private IEnumerator JackpotIncrementRoutine()
    {
        float syncTimer = 0f;
        while (isShowing)
        {
            yield return new WaitForSeconds(jackpotTickInterval);

            // Client-side visual interpolation
            currentJackpot += UnityEngine.Random.Range(jackpotMinIncrement, jackpotMaxIncrement + 1);
            UpdateJackpotOdometer();

            // Periodic sync check with server to align local odometer with the official global pool
            syncTimer += jackpotTickInterval;
            if (syncTimer >= 2f)
            {
                syncTimer = 0f;
                if (GameManager.Instance != null && GameManager.Instance.api != null)
                {
                    GameManager.Instance.api.GetJackpot(jackpotInfo =>
                    {
                        if (jackpotInfo != null)
                        {
                            // Soft update: prevent going backwards if network is slightly delayed
                            if (jackpotInfo.jackpot > currentJackpot)
                            {
                                currentJackpot = jackpotInfo.jackpot;
                                UpdateJackpotOdometer();
                            }
                        }
                    });
                }
            }
        }
    }

    private IEnumerator TimerCountdownRoutine()
    {
        while (isShowing)
        {
            DateTime now = DateTime.UtcNow;
            DateTime endsAt = serverJackpotEndsAt ?? now.Date.AddDays(1);
            TimeSpan timeRemaining = endsAt - now;

            if (timerText != null)
            {
                timerText.text = string.Format("{0:00}:{1:00}:{2:00}", 
                    (int)Math.Max(0, timeRemaining.TotalHours), 
                    Math.Max(0, timeRemaining.Minutes), 
                    Math.Max(0, timeRemaining.Seconds));
            }
            yield return new WaitForSeconds(1f);
        }
    }

    [ContextMenu("Build Ranking UI")]
    public void BuildRankingUI()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, "Build Ranking UI");
        }
#endif
        bool isNew;
        Sprite roundedSprite = GetBuiltinSpriteSafe("UI/Skin/UISprite.psd");
        Sprite knobSprite = GetBuiltinSpriteSafe("UI/Skin/Knob.psd");
        Font defaultFont = GetBuiltinFontSafe();

        // 1. Panel Size Delta & Pivot
        panelRect = GetComponent<RectTransform>();
        if (panelRect == null)
        {
            panelRect = GetOrAddComponentSafe<RectTransform>(gameObject, "Panel RectTransform", out isNew);
        }
        panelRect.sizeDelta = panelSize;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;

        // Background Image
        Image bgImg = GetComponent<Image>();
        if (bgImg == null) bgImg = GetOrAddComponentSafe<Image>(gameObject, "Background Image", out isNew);
        bgImg.color = panelColor;
        if (roundedSprite != null)
        {
            bgImg.sprite = roundedSprite;
            bgImg.type = Image.Type.Sliced;
        }

        // CanvasGroup
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = GetOrAddComponentSafe<CanvasGroup>(gameObject, "CanvasGroup", out isNew);

        // 2. Title Text "Ranking Jackpot"
        GameObject titleObj = FindOrCreateChildSafe("TitleText", transform);
        RectTransform tRect = GetOrAddComponentSafe<RectTransform>(titleObj, "Title RectTransform", out isNew);
        tRect.anchorMin = new Vector2(0.5f, 1f);
        tRect.anchorMax = new Vector2(0.5f, 1f);
        tRect.pivot = new Vector2(0.5f, 1f);
        tRect.sizeDelta = new Vector2(400f, 60f);
        tRect.anchoredPosition = new Vector2(0f, -25f);

        Text titleText = GetOrAddComponentSafe<Text>(titleObj, "Title Text", out isNew);
        titleText.text = "Ranking Jackpot";
        titleText.font = defaultFont;
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.9f, 0f, 1f); // Gold yellow

        Shadow shadow = GetOrAddComponentSafe<Shadow>(titleObj, "Title Shadow", out isNew);
        shadow.effectColor = textOutlineColor;
        shadow.effectDistance = new Vector2(2f, -3f);

        // 3. Jackpot Odometer Container
        GameObject jackObj = FindOrCreateChildSafe("JackpotOdometer", transform);
        RectTransform jRect = GetOrAddComponentSafe<RectTransform>(jackObj, "Jackpot RectTransform", out isNew);
        jRect.anchorMin = new Vector2(0.5f, 1f);
        jRect.anchorMax = new Vector2(0.5f, 1f);
        jRect.pivot = new Vector2(0.5f, 1f);
        jRect.sizeDelta = new Vector2(440f, 50f);
        jRect.anchoredPosition = new Vector2(0f, -95f);

        Image jBg = GetOrAddComponentSafe<Image>(jackObj, "Jackpot Image", out isNew);
        jBg.color = new Color(1f, 0.85f, 0f, 1f); // Golden yellow border/box
        if (roundedSprite != null)
        {
            jBg.sprite = roundedSprite;
            jBg.type = Image.Type.Sliced;
        }

        HorizontalLayoutGroup jLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(jackObj, "Jackpot Layout", out isNew);
        jLayout.padding = new RectOffset(8, 8, 4, 4);
        jLayout.spacing = 5f;
        jLayout.childAlignment = TextAnchor.MiddleCenter;
        jLayout.childControlWidth = false;
        jLayout.childControlHeight = false;
        jLayout.childForceExpandWidth = false;
        jLayout.childForceExpandHeight = false;

        GameObject coinIconObj = FindOrCreateChildSafe("CoinIcon", jackObj.transform);
        RectTransform coinRect = GetOrAddComponentSafe<RectTransform>(coinIconObj, "Coin RectTransform", out isNew);
        coinRect.sizeDelta = new Vector2(28f, 28f);
        Image coinImg = GetOrAddComponentSafe<Image>(coinIconObj, "Coin Image", out isNew);
        coinImg.color = Color.white;
        if (knobSprite != null) coinImg.sprite = knobSprite;

        jackpotDigitTexts = new Text[11];
        for (int i = 0; i < 11; i++)
        {
            GameObject digitObj = FindOrCreateChildSafe($"Digit_{i}", jackObj.transform);
            RectTransform dRect = GetOrAddComponentSafe<RectTransform>(digitObj, "Digit RectTransform", out isNew);
            dRect.sizeDelta = new Vector2(26f, 36f);

            Image dImg = GetOrAddComponentSafe<Image>(digitObj, "Digit Image", out isNew);
            dImg.color = new Color(0.12f, 0.28f, 0.65f, 1f); // Dark blue digit bg
            if (roundedSprite != null)
            {
                dImg.sprite = roundedSprite;
                dImg.type = Image.Type.Sliced;
            }

            GameObject dTxtObj = FindOrCreateChildSafe("Text", digitObj.transform);
            RectTransform dtRect = GetOrAddComponentSafe<RectTransform>(dTxtObj, "Text Rect", out isNew);
            dtRect.anchorMin = Vector2.zero;
            dtRect.anchorMax = Vector2.one;
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = Vector2.zero;

            Text dTxt = GetOrAddComponentSafe<Text>(dTxtObj, "Digit Text", out isNew);
            dTxt.font = defaultFont;
            dTxt.fontSize = 20;
            dTxt.fontStyle = FontStyle.Bold;
            dTxt.alignment = TextAnchor.MiddleCenter;
            dTxt.color = new Color(1f, 0.85f, 0f, 1f); // Gold digit

            jackpotDigitTexts[i] = dTxt;
        }

        // 4. Timer Capsule Pill
        GameObject timeCapsObj = FindOrCreateChildSafe("TimerCapsule", transform);
        RectTransform tmRect = GetOrAddComponentSafe<RectTransform>(timeCapsObj, "Timer RectTransform", out isNew);
        tmRect.anchorMin = new Vector2(0.5f, 1f);
        tmRect.anchorMax = new Vector2(0.5f, 1f);
        tmRect.pivot = new Vector2(0.5f, 1f);
        tmRect.sizeDelta = new Vector2(170f, 26f);
        tmRect.anchoredPosition = new Vector2(0f, -155f);

        Image tmBg = GetOrAddComponentSafe<Image>(timeCapsObj, "Timer Image", out isNew);
        tmBg.color = new Color(0f, 0.05f, 0.35f, 1f); // Deep capsule blue
        if (roundedSprite != null)
        {
            tmBg.sprite = roundedSprite;
            tmBg.type = Image.Type.Sliced;
        }

        HorizontalLayoutGroup tLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(timeCapsObj, "Timer Layout", out isNew);
        tLayout.spacing = 6f;
        tLayout.childAlignment = TextAnchor.MiddleCenter;
        tLayout.childControlWidth = false;
        tLayout.childControlHeight = false;
        tLayout.childForceExpandWidth = false;
        tLayout.childForceExpandHeight = false;

        GameObject clockIcon = FindOrCreateChildSafe("ClockIcon", timeCapsObj.transform);
        RectTransform clockRect = GetOrAddComponentSafe<RectTransform>(clockIcon, "Clock RectTransform", out isNew);
        clockRect.sizeDelta = new Vector2(14f, 14f);
        Image clockImg = GetOrAddComponentSafe<Image>(clockIcon, "Clock Image", out isNew);
        clockImg.color = new Color(0.7f, 0.85f, 1f, 1f);
        if (knobSprite != null) clockImg.sprite = knobSprite;

        GameObject clockTextObj = FindOrCreateChildSafe("ClockText", timeCapsObj.transform);
        RectTransform clockTxtRect = GetOrAddComponentSafe<RectTransform>(clockTextObj, "ClockText RectTransform", out isNew);
        clockTxtRect.sizeDelta = new Vector2(100f, 22f);

        timerText = GetOrAddComponentSafe<Text>(clockTextObj, "ClockText Text", out isNew);
        timerText.text = "03:02:30";
        timerText.font = defaultFont;
        timerText.fontSize = 13;
        timerText.fontStyle = FontStyle.Bold;
        timerText.alignment = TextAnchor.MiddleLeft;
        timerText.color = Color.white;

        // 5. Description Text
        GameObject descObj = FindOrCreateChildSafe("DescriptionText", transform);
        RectTransform descRect = GetOrAddComponentSafe<RectTransform>(descObj, "Desc RectTransform", out isNew);
        descRect.anchorMin = new Vector2(0.5f, 1f);
        descRect.anchorMax = new Vector2(0.5f, 1f);
        descRect.pivot = new Vector2(0.5f, 1f);
        descRect.sizeDelta = new Vector2(440f, 40f);
        descRect.anchoredPosition = new Vector2(0f, -190f);

        descriptionText = GetOrAddComponentSafe<Text>(descObj, "Desc Text", out isNew);
        descriptionText.text = "a winning item, and successful bets are paid out according to the item's designated multiplier.";
        descriptionText.font = defaultFont;
        descriptionText.fontSize = 13;
        descriptionText.alignment = TextAnchor.MiddleCenter;
        descriptionText.color = new Color(1f, 1f, 1f, 0.75f);

        // 6. Column Headers
        GameObject headObj = FindOrCreateChildSafe("ColumnHeaders", transform);
        RectTransform hRect = GetOrAddComponentSafe<RectTransform>(headObj, "Headers RectTransform", out isNew);
        hRect.anchorMin = new Vector2(0.5f, 1f);
        hRect.anchorMax = new Vector2(0.5f, 1f);
        hRect.pivot = new Vector2(0.5f, 1f);
        hRect.sizeDelta = new Vector2(420f, 22f);
        hRect.anchoredPosition = new Vector2(0f, -240f);

        HorizontalLayoutGroup hLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(headObj, "Headers Layout", out isNew);
        hLayout.padding = new RectOffset(6, 6, 0, 0);
        hLayout.spacing = 10f;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        GameObject rankH = FindOrCreateChildSafe("RankHeader", headObj.transform);
        RectTransform rhRect = GetOrAddComponentSafe<RectTransform>(rankH, "RH Rect", out isNew);
        rhRect.sizeDelta = new Vector2(70f, 20f);
        Text rh = GetOrAddComponentSafe<Text>(rankH, "RH Text", out isNew);
        rh.text = "Rank";
        rh.font = defaultFont;
        rh.fontSize = 14;
        rh.fontStyle = FontStyle.Bold;
        rh.color = new Color(0.8f, 0.9f, 1f, 1f);

        GameObject nameH = FindOrCreateChildSafe("NameHeader", headObj.transform);
        RectTransform nhRect = GetOrAddComponentSafe<RectTransform>(nameH, "NH Rect", out isNew);
        nhRect.sizeDelta = new Vector2(150f, 20f);
        Text nh = GetOrAddComponentSafe<Text>(nameH, "NH Text", out isNew);
        nh.text = "Name";
        nh.font = defaultFont;
        nh.fontSize = 14;
        nh.fontStyle = FontStyle.Bold;
        nh.alignment = TextAnchor.MiddleLeft;
        nh.color = new Color(0.8f, 0.9f, 1f, 1f);

        GameObject headSpacer = FindOrCreateChildSafe("Spacer", headObj.transform);
        GetOrAddComponentSafe<RectTransform>(headSpacer, "Spacer Rect", out isNew);
        LayoutElement hsLE = GetOrAddComponentSafe<LayoutElement>(headSpacer, "Spacer LE", out isNew);
        hsLE.flexibleWidth = 1f;

        GameObject coinsH = FindOrCreateChildSafe("CoinsHeader", headObj.transform);
        RectTransform chRect = GetOrAddComponentSafe<RectTransform>(coinsH, "CH Rect", out isNew);
        chRect.sizeDelta = new Vector2(140f, 20f);
        Text ch = GetOrAddComponentSafe<Text>(coinsH, "CH Text", out isNew);
        ch.text = "Coins Play";
        ch.font = defaultFont;
        ch.fontSize = 14;
        ch.fontStyle = FontStyle.Bold;
        ch.alignment = TextAnchor.MiddleRight;
        ch.color = new Color(0.8f, 0.9f, 1f, 1f);

        // 7. Scroll Box Frame (Semi-transparent background border)
        GameObject frameObj = FindOrCreateChildSafe("ListFrame", transform);
        RectTransform frRect = GetOrAddComponentSafe<RectTransform>(frameObj, "Frame RectTransform", out isNew);
        frRect.anchorMin = new Vector2(0.5f, 1f);
        frRect.anchorMax = new Vector2(0.5f, 1f);
        frRect.pivot = new Vector2(0.5f, 1f);
        frRect.sizeDelta = new Vector2(446f, 335f);
        frRect.anchoredPosition = new Vector2(0f, -270f);

        Image frBg = GetOrAddComponentSafe<Image>(frameObj, "Frame Image", out isNew);
        frBg.color = new Color(0.12f, 0.28f, 0.65f, 0.2f); // Semi-transparent blue border
        if (roundedSprite != null)
        {
            frBg.sprite = roundedSprite;
            frBg.type = Image.Type.Sliced;
        }

        // ScrollView
        GameObject scrollObj = FindOrCreateChildSafe("ScrollView", frameObj.transform);
        RectTransform scRect = GetOrAddComponentSafe<RectTransform>(scrollObj, "Scroll RectTransform", out isNew);
        scRect.anchorMin = new Vector2(0.5f, 1f);
        scRect.anchorMax = new Vector2(0.5f, 1f);
        scRect.pivot = new Vector2(0.5f, 1f);
        scRect.sizeDelta = new Vector2(440f, 265f);
        scRect.anchoredPosition = new Vector2(0f, -3f);

        ScrollRect scroll = GetOrAddComponentSafe<ScrollRect>(scrollObj, "Scroll Component", out isNew);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;

        // Viewport
        GameObject viewObj = FindOrCreateChildSafe("Viewport", scrollObj.transform);
        RectTransform viewRect = GetOrAddComponentSafe<RectTransform>(viewObj, "View Rect", out isNew);
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = Vector2.zero;
        viewRect.offsetMax = Vector2.zero;
        GetOrAddComponentSafe<Image>(viewObj, "View Image", out isNew).color = new Color(1f, 1f, 1f, 0.005f);
        GetOrAddComponentSafe<Mask>(viewObj, "View Mask", out isNew).showMaskGraphic = false;

        // Content
        GameObject contentObj = FindOrCreateChildSafe("Content", viewObj.transform);
        RectTransform conRect = GetOrAddComponentSafe<RectTransform>(contentObj, "Content Rect", out isNew);
        conRect.anchorMin = new Vector2(0f, 1f);
        conRect.anchorMax = new Vector2(1f, 1f);
        conRect.pivot = new Vector2(0.5f, 1f);
        conRect.offsetMin = Vector2.zero;
        conRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vLayout = GetOrAddComponentSafe<VerticalLayoutGroup>(contentObj, "Content Layout", out isNew);
        vLayout.padding = new RectOffset(6, 6, 6, 6);
        vLayout.spacing = 5f;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter csf = GetOrAddComponentSafe<ContentSizeFitter>(contentObj, "Content SizeFitter", out isNew);
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewRect;
        scroll.content = conRect;
        rowsContainer = conRect;

        // My Ranking Row Pinned at bottom
        GameObject myRankObj = FindOrCreateChildSafe("MyRankingRow", frameObj.transform);
        RectTransform mrRect = GetOrAddComponentSafe<RectTransform>(myRankObj, "MyRank RectTransform", out isNew);
        mrRect.anchorMin = new Vector2(0.5f, 0f);
        mrRect.anchorMax = new Vector2(0.5f, 0f);
        mrRect.pivot = new Vector2(0.5f, 0f);
        mrRect.sizeDelta = new Vector2(440f, 55f);
        mrRect.anchoredPosition = new Vector2(0f, 3f);

        myRankingRow = GetOrAddComponentSafe<RankingRow>(myRankObj, "MyRank RowComponent", out isNew);
        myRankingRow.BuildRowUI();
        myRankingRow.backgroundImage.color = new Color(0.12f, 0.22f, 0.45f, 1f); // Highlight blue

        // 8. Today / Yesterday Tab Buttons
        GameObject btnsObj = FindOrCreateChildSafe("TabButtons", transform);
        RectTransform bRect = GetOrAddComponentSafe<RectTransform>(btnsObj, "Buttons RectTransform", out isNew);
        bRect.anchorMin = new Vector2(0.5f, 0f);
        bRect.anchorMax = new Vector2(0.5f, 0f);
        bRect.pivot = new Vector2(0.5f, 0f);
        bRect.sizeDelta = new Vector2(440f, 50f);
        bRect.anchoredPosition = new Vector2(0f, 20f);

        HorizontalLayoutGroup bLayout = GetOrAddComponentSafe<HorizontalLayoutGroup>(btnsObj, "Buttons Layout", out isNew);
        bLayout.spacing = 15f;
        bLayout.childAlignment = TextAnchor.MiddleCenter;
        bLayout.childControlWidth = true;
        bLayout.childControlHeight = true;
        bLayout.childForceExpandWidth = true;
        bLayout.childForceExpandHeight = false;

        // Today button
        GameObject todayObj = FindOrCreateChildSafe("TodayButton", btnsObj.transform);
        todayTabButton = GetOrAddComponentSafe<Button>(todayObj, "Today Button", out isNew);
        todayTabImage = GetOrAddComponentSafe<Image>(todayObj, "Today Image", out isNew);
        todayTabImage.color = activeTabColor;
        if (roundedSprite != null)
        {
            todayTabImage.sprite = roundedSprite;
            todayTabImage.type = Image.Type.Sliced;
        }
        GameObject todayTextObj = FindOrCreateChildSafe("Text", todayObj.transform);
        RectTransform ttRect = GetOrAddComponentSafe<RectTransform>(todayTextObj, "TodayText Rect", out isNew);
        ttRect.anchorMin = Vector2.zero;
        ttRect.anchorMax = Vector2.one;
        ttRect.offsetMin = Vector2.zero;
        ttRect.offsetMax = Vector2.zero;
        Text tt = GetOrAddComponentSafe<Text>(todayTextObj, "TodayText Text", out isNew);
        tt.text = "Today";
        tt.font = defaultFont;
        tt.fontSize = 18;
        tt.fontStyle = FontStyle.Bold;
        tt.alignment = TextAnchor.MiddleCenter;
        tt.color = Color.white;

        // Yesterday button
        GameObject yesterdayObj = FindOrCreateChildSafe("YesterdayButton", btnsObj.transform);
        yesterdayTabButton = GetOrAddComponentSafe<Button>(yesterdayObj, "Yesterday Button", out isNew);
        yesterdayTabImage = GetOrAddComponentSafe<Image>(yesterdayObj, "Yesterday Image", out isNew);
        yesterdayTabImage.color = inactiveTabColor;
        if (roundedSprite != null)
        {
            yesterdayTabImage.sprite = roundedSprite;
            yesterdayTabImage.type = Image.Type.Sliced;
        }
        GameObject yesterdayTextObj = FindOrCreateChildSafe("Text", yesterdayObj.transform);
        RectTransform ytRect = GetOrAddComponentSafe<RectTransform>(yesterdayTextObj, "YesterdayText Rect", out isNew);
        ytRect.anchorMin = Vector2.zero;
        ytRect.anchorMax = Vector2.one;
        ytRect.offsetMin = Vector2.zero;
        ytRect.offsetMax = Vector2.zero;
        Text yt = GetOrAddComponentSafe<Text>(yesterdayTextObj, "YesterdayText Text", out isNew);
        yt.text = "Yesterday";
        yt.font = defaultFont;
        yt.fontSize = 18;
        yt.fontStyle = FontStyle.Bold;
        yt.alignment = TextAnchor.MiddleCenter;
        yt.color = Color.white;

        // 9. Close Button (top-right X circle)
        GameObject closeObj = FindOrCreateChildSafe("CloseButton", transform);
        RectTransform cRect = GetOrAddComponentSafe<RectTransform>(closeObj, "Close RectTransform", out isNew);
        cRect.anchorMin = new Vector2(1f, 1f);
        cRect.anchorMax = new Vector2(1f, 1f);
        cRect.pivot = new Vector2(1f, 1f);
        cRect.sizeDelta = new Vector2(36f, 36f);
        cRect.anchoredPosition = new Vector2(-15f, -15f);

        closeButton = GetOrAddComponentSafe<Button>(closeObj, "Close Button", out isNew);
        Image cImg = GetOrAddComponentSafe<Image>(closeObj, "Close Image", out isNew);
        cImg.color = new Color(0.02f, 0.1f, 0.25f, 1f); // Dark button color
        if (knobSprite != null) cImg.sprite = knobSprite;

        GameObject closeTxtObj = FindOrCreateChildSafe("Text", closeObj.transform);
        RectTransform ctRect = GetOrAddComponentSafe<RectTransform>(closeTxtObj, "CloseText Rect", out isNew);
        ctRect.anchorMin = Vector2.zero;
        ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = Vector2.zero;
        ctRect.offsetMax = Vector2.zero;
        Text ct = GetOrAddComponentSafe<Text>(closeTxtObj, "CloseText Text", out isNew);
        ct.text = "X";
        ct.font = defaultFont;
        ct.fontSize = 18;
        ct.fontStyle = FontStyle.Bold;
        ct.alignment = TextAnchor.MiddleCenter;
        ct.color = Color.white;

        // 9.5. Auto-create RowPrefab template if null
        if (rowPrefab == null)
        {
            GameObject prefabObj = FindOrCreateChildSafe("RankingRowPrefab", transform);
            prefabObj.SetActive(false); // Hide the template row
            RankingRow prefabRowComp = GetOrAddComponentSafe<RankingRow>(prefabObj, "RowPrefab Component", out isNew);
            prefabRowComp.BuildRowUI();
            rowPrefab = prefabRowComp;
        }

        // 10. Generate preview rows in Editor to verify list visual layout instantly
        if (rowPrefab != null)
        {
            CreateEditorPreviewRows();
        }
        else
        {
            Debug.LogWarning("[RankingJackpotPanel] Please assign Row Prefab in the inspector to render visual preview rows.");
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

    private void CreateEditorPreviewRows()
    {
        if (rowPrefab == null || rowsContainer == null) return;

        // Clear existing children
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in rowsContainer)
        {
            toDestroy.Add(child.gameObject);
        }
        for (int i = toDestroy.Count - 1; i >= 0; i--)
        {
            if (toDestroy[i] != null)
            {
                if (Application.isPlaying) Destroy(toDestroy[i]);
                else DestroyImmediate(toDestroy[i]);
            }
        }

        // Build 5 sample preview rows
        for (int i = 1; i <= 5; i++)
        {
            RankingRow row = Instantiate(rowPrefab, rowsContainer);
            row.gameObject.SetActive(true);
            row.gameObject.name = $"PreviewRow_{i}";
            row.BuildRowUI();
            row.SetData(i, i == 4 ? "You" : $"Repo_Player_{i}", 651616565 / i, null, $"https://picsum.photos/100?random={i}");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(row.gameObject, "Create Preview Row");
            }
#endif
        }
    }

    private GameObject FindOrCreateChildSafe(string name, Transform parent)
    {
        Transform child = parent.Find(name);
        if (child != null) return child.gameObject;
        return CreateGameObjectSafe(name, parent);
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
