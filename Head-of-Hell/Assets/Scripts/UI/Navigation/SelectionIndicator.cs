using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Subtle "this is selected" effect for one Selectable: a soft glow behind it, a short underline that
/// fades at its ends, and a slight scale-up, all eased in/out. Added automatically by MenuNavigator;
/// all look settings live on the MenuNavigator.
/// Mouse hover selects the element (so mouse and keyboard/gamepad never show two different highlights).
/// Elements whose Navigation is "None" (e.g. TitleButton, mouse-only) show the effect on hover but never
/// take the selection, so the keyboard/gamepad can't get stuck on them.
/// </summary>
[DisallowMultipleComponent]
public class SelectionIndicator : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    MenuNavigator nav;
    Selectable selectable;
    Image glow, underline;
    Vector3 baseScale;
    bool selected, hovered, built;
    float t;   // 0 = off, 1 = fully shown

    static Sprite glowSprite, lineSprite;

    public void Init(MenuNavigator navigator)
    {
        nav = navigator;
        if (built) return;
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;

        glow = MakeChild("~SelGlow", GetGlowSprite(), Image.Type.Sliced);
        var g = glow.rectTransform;
        g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one;
        g.offsetMin = -nav.glowPadding; g.offsetMax = nav.glowPadding;

        underline = MakeChild("~SelLine", GetLineSprite(), Image.Type.Simple);
        var u = underline.rectTransform;
        float half = nav.underlineWidth * 0.5f;
        u.anchorMin = new Vector2(0.5f - half, 0f); u.anchorMax = new Vector2(0.5f + half, 0f);
        u.pivot = new Vector2(0.5f, 0f);
        u.sizeDelta = new Vector2(0f, nav.underlineHeight);
        u.anchoredPosition = new Vector2(0f, 2f);

        built = true;
        Apply(0f);
    }

    Image MakeChild(string name, Sprite sprite, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();                 // behind the button's label
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = type;
        img.raycastTarget = false;                        // never blocks clicks
        return img;
    }

    void OnEnable()
    {
        var es = EventSystem.current;
        selected = es != null && es.currentSelectedGameObject == gameObject;
    }

    void OnDisable()
    {
        // Panel closed: reset instantly so it doesn't reappear half-faded next time.
        selected = hovered = false;
        t = 0f;
        if (built) Apply(0f);
    }

    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        if (selectable != null && selectable.IsInteractable() &&
            selectable.navigation.mode != Navigation.Mode.None && EventSystem.current != null)
        {
            MenuNavigator.lastPointerSelectFrame = Time.frameCount;
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData e) => hovered = false;

    void Update()
    {
        if (!built) return;
        bool mouseOnly = selectable != null && selectable.navigation.mode == Navigation.Mode.None;
        bool show = (selected || (mouseOnly && hovered)) && (selectable == null || selectable.IsInteractable());
        float target = show ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / nav.fadeTime);
        Apply(t);
    }

    void Apply(float k)
    {
        float e = k * k * (3f - 2f * k);   // smoothstep easing
        var gc = nav.glowColor; gc.a *= e; glow.color = gc;
        var lc = nav.underlineColor; lc.a *= e; underline.color = lc;
        transform.localScale = baseScale * Mathf.Lerp(1f, nav.selectedScale, e);
    }

    // ---------- generated sprites (no extra assets needed) ----------

    static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int size = 64, border = 24;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(border - (x + 0.5f), (x + 0.5f) - (size - border)));
                float dy = Mathf.Max(0f, Mathf.Max(border - (y + 0.5f), (y + 0.5f) - (size - border)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (border - 1f);   // 0 inside, 1 at the edge
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        return glowSprite;
    }

    static Sprite GetLineSprite()
    {
        if (lineSprite != null) return lineSprite;
        const int w = 128, h = 8;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float ax = Mathf.Pow(Mathf.Sin(Mathf.PI * (x + 0.5f) / w), 0.8f);       // fades at the ends
                float ay = 1f - Mathf.Abs((y + 0.5f) - h * 0.5f) / (h * 0.5f);          // soft top/bottom
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ax * Mathf.Clamp01(ay * 1.6f)));
            }
        tex.Apply();
        lineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return lineSprite;
    }
}
