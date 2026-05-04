using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeLoadingOverlay : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private RectTransform spinnerRoot;
    [SerializeField] private Image eyeImage;
    [SerializeField] private Sprite[] eyeFrames;
    [SerializeField] private RectTransform progressTrack;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image glowBar;
    [SerializeField] private string baseMessage = "Refreshing player stats";
    [SerializeField] private float dotIntervalSeconds = 0.35f;
    [SerializeField] private float pulseSpeed = 1.6f;
    [SerializeField] private float progressSmoothingSpeed = 1.8f;
    [SerializeField] private float eyeBlinkIntervalSeconds = 2.0f;
    [SerializeField] private float eyeBlinkFrameDuration = 0.06f;

    private float timer;
    private int dotCount;
    private float animationTime;
    private float currentProgress;
    private float targetProgress;
    private int currentEyeFrameIndex;
    private float eyeBlinkTimer;
    private float eyeFrameTimer;
    private bool eyeBlinking;
    private bool eyeClosing = true;

    public static RuntimeLoadingOverlay Create(
        TMP_FontAsset fontAsset = null,
        Sprite[] loadingEyeFrames = null,
        string overlayName = "RuntimeLoadingOverlay"
    )
    {
        GameObject root = new GameObject(
            overlayName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(RuntimeLoadingOverlay)
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
        dimmer.transform.SetParent(root.transform, false);
        RectTransform dimmerRect = dimmer.GetComponent<RectTransform>();
        dimmerRect.anchorMin = Vector2.zero;
        dimmerRect.anchorMax = Vector2.one;
        dimmerRect.offsetMin = Vector2.zero;
        dimmerRect.offsetMax = Vector2.zero;
        dimmerRect.sizeDelta = Vector2.zero;
        dimmerRect.anchoredPosition = Vector2.zero;
        dimmer.GetComponent<Image>().color = new Color(0.055f, 0.012f, 0.028f, 1f);

        GameObject aura = new GameObject("Aura", typeof(RectTransform), typeof(Image));
        aura.transform.SetParent(root.transform, false);
        RectTransform auraRect = aura.GetComponent<RectTransform>();
        auraRect.anchorMin = new Vector2(0.5f, 0.5f);
        auraRect.anchorMax = new Vector2(0.5f, 0.5f);
        auraRect.sizeDelta = new Vector2(1800f, 1200f);
        auraRect.anchoredPosition = Vector2.zero;
        Image auraImage = aura.GetComponent<Image>();
        auraImage.color = new Color(0.35f, 0.06f, 0.10f, 0.10f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 500f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.11f, 0.06f, 0.08f, 0.97f);

        GameObject panelBorder = new GameObject("PanelBorder", typeof(RectTransform), typeof(Image));
        panelBorder.transform.SetParent(panel.transform, false);
        RectTransform borderRect = panelBorder.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(10f, 10f);
        borderRect.offsetMax = new Vector2(-10f, -10f);
        panelBorder.GetComponent<Image>().color = new Color(0.66f, 0.26f, 0.35f, 0.28f);

        GameObject eyeRoot = new GameObject("EyeRoot", typeof(RectTransform));
        eyeRoot.transform.SetParent(panel.transform, false);
        RectTransform eyeRootRect = eyeRoot.GetComponent<RectTransform>();
        eyeRootRect.anchorMin = new Vector2(0.5f, 0.5f);
        eyeRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        eyeRootRect.pivot = new Vector2(0.5f, 0.5f);
        eyeRootRect.sizeDelta = new Vector2(240f, 130f);
        eyeRootRect.anchoredPosition = new Vector2(0f, 125f);

        GameObject eyeImageObject = new GameObject("EyeImage", typeof(RectTransform), typeof(Image));
        eyeImageObject.transform.SetParent(eyeRoot.transform, false);
        RectTransform eyeImageRect = eyeImageObject.GetComponent<RectTransform>();
        eyeImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        eyeImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        eyeImageRect.pivot = new Vector2(0.5f, 0.5f);
        eyeImageRect.sizeDelta = new Vector2(240f, 130f);
        eyeImageRect.anchoredPosition = Vector2.zero;
        Image eyeImage = eyeImageObject.GetComponent<Image>();
        eyeImage.preserveAspect = true;

        if (loadingEyeFrames != null && loadingEyeFrames.Length > 0)
        {
            eyeImage.sprite = loadingEyeFrames[0];

            for (int i = 0; i < loadingEyeFrames.Length; i++)
            {
                if (loadingEyeFrames[i] != null && loadingEyeFrames[i].texture != null)
                    loadingEyeFrames[i].texture.filterMode = FilterMode.Point;
            }
        }
        else
        {
            Debug.LogWarning("RuntimeLoadingOverlay: loading eye sprite not assigned.");
            eyeImage.enabled = false;
        }

        GameObject title = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(panel.transform, false);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(700f, 90f);
        titleRect.anchoredPosition = new Vector2(0f, 35f);

        TextMeshProUGUI text = title.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 54f;
        text.color = new Color(0.95f, 0.86f, 0.76f, 1f);
        if (fontAsset != null)
            text.font = fontAsset;
        text.text = "Refreshing player stats";

        GameObject subtitle = new GameObject("HintText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitle.transform.SetParent(panel.transform, false);
        RectTransform subtitleRect = subtitle.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleRect.pivot = new Vector2(0.5f, 0.5f);
        subtitleRect.sizeDelta = new Vector2(700f, 70f);
        subtitleRect.anchoredPosition = new Vector2(0f, -45f);

        TextMeshProUGUI hint = subtitle.GetComponent<TextMeshProUGUI>();
        hint.alignment = TextAlignmentOptions.Center;
        hint.fontSize = 28f;
        hint.color = new Color(0.84f, 0.74f, 0.70f, 1f);
        if (fontAsset != null)
            hint.font = fontAsset;
        hint.text = "Calibrating fresh charts, metrics, and profile insight";

        GameObject progressTrackObject = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        progressTrackObject.transform.SetParent(panel.transform, false);
        RectTransform progressTrackRect = progressTrackObject.GetComponent<RectTransform>();
        progressTrackRect.anchorMin = new Vector2(0.5f, 0.5f);
        progressTrackRect.anchorMax = new Vector2(0.5f, 0.5f);
        progressTrackRect.pivot = new Vector2(0.5f, 0.5f);
        progressTrackRect.sizeDelta = new Vector2(620f, 18f);
        progressTrackRect.anchoredPosition = new Vector2(0f, -135f);
        progressTrackObject.GetComponent<Image>().color = new Color(0.23f, 0.10f, 0.13f, 1f);

        GameObject progressFillObject = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        progressFillObject.transform.SetParent(progressTrackObject.transform, false);
        RectTransform progressFillRect = progressFillObject.GetComponent<RectTransform>();
        progressFillRect.anchorMin = new Vector2(0f, 0f);
        progressFillRect.anchorMax = new Vector2(0f, 1f);
        progressFillRect.pivot = new Vector2(0f, 0.5f);
        progressFillRect.anchoredPosition = Vector2.zero;
        progressFillRect.sizeDelta = new Vector2(260f, 0f);
        Image progressFillImage = progressFillObject.GetComponent<Image>();
        progressFillImage.color = new Color(0.83f, 0.33f, 0.39f, 1f);

        GameObject glow = new GameObject("GlowBar", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(progressTrackObject.transform, false);
        RectTransform glowRect = glow.GetComponent<RectTransform>();
        glowRect.anchorMin = new Vector2(0f, 0f);
        glowRect.anchorMax = new Vector2(0f, 1f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = new Vector2(32f, 0f);
        glowRect.anchoredPosition = Vector2.zero;
        Image glowImage = glow.GetComponent<Image>();
        glowImage.color = new Color(1f, 0.88f, 0.80f, 0.75f);

        RuntimeLoadingOverlay overlay = root.GetComponent<RuntimeLoadingOverlay>();
        overlay.statusText = text;
        overlay.hintText = hint;
        overlay.spinnerRoot = eyeRootRect;
        overlay.eyeImage = eyeImage;
        overlay.eyeFrames = loadingEyeFrames;
        overlay.progressTrack = progressTrackRect;
        overlay.progressFill = progressFillImage;
        overlay.glowBar = glowImage;
        overlay.baseMessage = "Refreshing player stats";
        overlay.currentProgress = 0f;
        overlay.targetProgress = 0f;
        overlay.currentEyeFrameIndex = loadingEyeFrames != null && loadingEyeFrames.Length > 0
            ? loadingEyeFrames.Length - 1
            : 0;
        overlay.eyeBlinkTimer = 0f;
        overlay.eyeFrameTimer = 0f;
        overlay.eyeBlinking = false;
        overlay.eyeClosing = true;
        overlay.UpdateLabel(force: true);
        overlay.ApplyProgressVisuals();

        DontDestroyOnLoad(root);
        root.SetActive(false);
        return overlay;
    }

    public void Show(string message = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
            baseMessage = message;

        dotCount = 0;
        timer = 0f;
        animationTime = 0f;
        currentProgress = 0f;
        targetProgress = 0f;
        currentEyeFrameIndex = eyeFrames != null && eyeFrames.Length > 0
            ? eyeFrames.Length - 1
            : 0;
        eyeBlinkTimer = 0f;
        eyeFrameTimer = 0f;
        eyeBlinking = false;
        eyeClosing = true;
        UpdateLabel(force: true);
        ApplyProgressVisuals();
        UpdateEyeFrame();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetProgress(float progress, string message = null, string hint = null)
    {
        targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));

        if (!string.IsNullOrWhiteSpace(message))
            baseMessage = message;

        if (hintText != null && hint != null)
            hintText.text = hint;

        UpdateLabel(force: true);
    }

    public float CurrentProgress => currentProgress;

    private void Update()
    {
        if (!gameObject.activeSelf || statusText == null)
            return;

        timer += Time.unscaledDeltaTime;
        animationTime += Time.unscaledDeltaTime;
        currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, progressSmoothingSpeed * Time.unscaledDeltaTime);

        if (timer < dotIntervalSeconds)
        {
            AnimateVisuals();
            return;
        }

        timer = 0f;
        dotCount = (dotCount + 1) % 4;
        UpdateLabel(force: false);
        AnimateVisuals();
    }

    private void UpdateLabel(bool force)
    {
        if (statusText == null)
            return;

        string dots = new string('.', dotCount);
        statusText.text = force || dotCount == 0
            ? baseMessage
            : baseMessage + dots;
    }

    private void AnimateVisuals()
    {
        UpdateEyeFrame();
        ApplyProgressVisuals();

        if (glowBar != null && progressFill != null)
        {
            Color glowColor = glowBar.color;
            glowColor.a = Mathf.Lerp(0.35f, 0.9f, 0.5f + 0.5f * Mathf.Sin(animationTime * 2.4f));
            glowBar.color = glowColor;
        }

        if (hintText != null)
        {
            Color hintColor = hintText.color;
            hintColor.a = Mathf.Lerp(0.72f, 1f, 0.5f + 0.5f * Mathf.Sin(animationTime * 1.7f));
            hintText.color = hintColor;
        }
    }

    private void UpdateEyeFrame()
    {
        if (eyeImage == null || eyeFrames == null || eyeFrames.Length == 0 || !eyeImage.enabled)
            return;

        if (eyeFrames.Length == 1)
        {
            eyeImage.sprite = eyeFrames[eyeFrames.Length - 1];
            return;
        }

        if (!eyeBlinking)
        {
            currentEyeFrameIndex = eyeFrames.Length - 1;
            eyeImage.sprite = eyeFrames[currentEyeFrameIndex];
            eyeBlinkTimer += Time.unscaledDeltaTime;

            if (eyeBlinkTimer >= eyeBlinkIntervalSeconds)
            {
                eyeBlinking = true;
                eyeClosing = true;
                eyeBlinkTimer = 0f;
                eyeFrameTimer = 0f;
            }

            return;
        }

        eyeFrameTimer += Time.unscaledDeltaTime;
        if (eyeFrameTimer < eyeBlinkFrameDuration)
            return;

        eyeFrameTimer = 0f;

        if (eyeClosing)
        {
            if (currentEyeFrameIndex > 0)
            {
                currentEyeFrameIndex--;
            }
            else
            {
                eyeClosing = false;
                if (currentEyeFrameIndex < eyeFrames.Length - 1)
                    currentEyeFrameIndex++;
            }
        }
        else
        {
            if (currentEyeFrameIndex < eyeFrames.Length - 1)
            {
                currentEyeFrameIndex++;
            }
            else
            {
                eyeBlinking = false;
                eyeClosing = true;
            }
        }

        eyeImage.sprite = eyeFrames[currentEyeFrameIndex];
    }

    private void ApplyProgressVisuals()
    {
        if (progressFill == null)
            return;

        float trackWidth = progressTrack != null ? progressTrack.rect.width : 0f;
        if (trackWidth <= 0f && progressTrack != null)
            trackWidth = progressTrack.sizeDelta.x;

        RectTransform fillRect = progressFill.rectTransform;
        float fillWidth = trackWidth * currentProgress;
        fillRect.sizeDelta = new Vector2(fillWidth, fillRect.sizeDelta.y);

        if (glowBar == null)
            return;

        RectTransform glowRect = glowBar.rectTransform;
        float clampedX = Mathf.Clamp(fillWidth, 0f, Mathf.Max(0f, trackWidth));
        glowRect.anchoredPosition = new Vector2(clampedX, 0f);
        glowRect.gameObject.SetActive(fillWidth > 0.001f);
    }
}
