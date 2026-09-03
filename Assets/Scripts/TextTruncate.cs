using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps a legacy <see cref="Text"/> inside its own box by trimming the string
/// and appending an ellipsis — "Zimo Live Official" → "Zimo Live Of..".
///
/// Why not Best Fit? Best Fit only constrains by the rect's HEIGHT when
/// horizontalOverflow is Overflow, so it happily grows a long name past the
/// panel edge. It also renders every name at a different size, which looks
/// inconsistent in a row. Trimming keeps one font size and always fits.
///
/// Trimming is measured by real rendered WIDTH, not a character count, so wide
/// names ("WWWWW") and narrow ones ("iiiii") are both handled correctly.
///
/// Setup: attach to the Text. Whatever assigns `text.text` keeps working —
/// this re-trims whenever the value changes.
/// </summary>
[RequireComponent(typeof(Text))]
public class TextTruncate : MonoBehaviour
{
    [Tooltip("Appended when the text has to be cut. \"..\" or \"…\".")]
    public string ellipsis = "..";

    [Tooltip("Hard cap on characters before the ellipsis. This is the DETERMINISTIC " +
             "limit: font measurement depends on canvas scale, glyph hinting and " +
             "rounding, which differ between the Editor and a WebGL build on a real " +
             "screen — a name that measured too wide in the Editor could measure as " +
             "fitting on device and spill out. The cap can't vary by platform. " +
             "0 = no cap (width only).")]
    public int maxCharacters = 14;

    [Tooltip("Extra padding (px) kept clear on the right edge.")]
    public float rightPadding = 4f;

    Text text;
    RectTransform rect;

    /// The last FULL string we were handed (never the trimmed version).
    string source;
    /// The trimmed string we last wrote, so we can tell our own writes apart
    /// from a new value assigned by the game.
    string rendered;

    void Awake()
    {
        text = GetComponent<Text>();
        rect = (RectTransform)transform;
        source = text.text;
    }

    void LateUpdate()
    {
        if (text == null) return;

        // Someone assigned a new value → that becomes the new source string.
        if (!string.Equals(text.text, rendered))
            source = text.text;

        string fitted = Fit(source);
        if (!string.Equals(text.text, fitted))
            text.text = fitted;
        rendered = fitted;
    }

    string Fit(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // 1. Hard character cap first — platform-independent, so the device and
        //    the Editor always agree.
        if (maxCharacters > 0 && s.Length > maxCharacters)
            s = s.Substring(0, maxCharacters).TrimEnd() + ellipsis;

        // 2. Then shrink further by measured width, for names made of unusually
        //    wide glyphs ("WWWWWWWWWWWWWW" still overflows at 14 characters).
        float max = rect.rect.width - rightPadding;
        if (max <= 0f) return s;
        if (Width(s) <= max) return s;

        string body = s.EndsWith(ellipsis) ? s.Substring(0, s.Length - ellipsis.Length) : s;
        for (int len = body.Length - 1; len > 0; len--)
        {
            string candidate = body.Substring(0, len).TrimEnd() + ellipsis;
            if (Width(candidate) <= max) return candidate;
        }
        return ellipsis;
    }

    float Width(string s)
    {
        var settings = text.GetGenerationSettings(Vector2.zero);
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings)
             / text.pixelsPerUnit;
    }
}
