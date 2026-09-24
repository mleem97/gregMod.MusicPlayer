using UnityEngine;
using UnityEngine.UIElements;

namespace greg.Mods.MusicPlayer.UI;

// Mod-local mirror of GregUITheme (sky/atoll blue, modern minimal).
// Deliberately decoupled: works identically WITH and WITHOUT gregCore.
// Mirror theme changes from gregCore here.
public static class ModTheme
{
    public static readonly Color PrimaryAccent = ParseHex("#0AA2C0"); // Atoll blue (CTA)
    public static readonly Color PrimaryTextOnAccent = ParseHex("#06121F"); // Dark navy on atoll
    public static readonly Color SecondaryColor = ParseHex("#87CEEB"); // Sky blue (highlights)
    public static readonly Color TertiaryColor = new Color(1.00f, 0.85f, 0.28f); // Gold
    public static readonly Color NeutralBorder = new Color(0.14f, 0.17f, 0.22f, 0.80f);
    public static readonly Color BackgroundDark = new Color(0.06f, 0.08f, 0.11f, 0.96f);
    public static readonly Color SurfaceDark = new Color(0.08f, 0.10f, 0.13f, 0.98f);
    public static Color PanelBackground => SurfaceDark;
    public static readonly Color TextPrimary = new Color(0.92f, 0.94f, 0.96f);
    public static readonly Color TextDim = new Color(0.62f, 0.66f, 0.72f);

    public static readonly float CornerRadius = 8f;
    public static readonly float Padding = 16f;
    public static readonly float Spacing = 12f;
    public static readonly float HeaderHeight = 40f;
    public static readonly float BorderWidth = 2f;

    private static Color ParseHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;
        return Color.magenta;
    }

    public static void ApplyTextStyle(Label label, bool isHeadline = false)
    {
        label.style.fontSize = isHeadline ? 20 : 14;
        label.style.color = isHeadline ? TextPrimary : new Color(0.88f, 0.88f, 0.88f);
        label.style.unityFontStyleAndWeight = isHeadline ? FontStyle.Bold : FontStyle.Normal;
    }

    public static void ApplyPrimaryButtonStyle(Button button)
    {
        button.style.backgroundColor = PrimaryAccent;
        button.style.color = PrimaryTextOnAccent;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = CornerRadius;
        button.style.borderTopRightRadius = CornerRadius;
        button.style.borderBottomLeftRadius = CornerRadius;
        button.style.borderBottomRightRadius = CornerRadius;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
    }

    public static void ApplySecondaryButtonStyle(Button button)
    {
        button.style.backgroundColor = new Color(0.11f, 0.13f, 0.17f);
        button.style.color = new Color(0.85f, 0.87f, 0.90f);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = CornerRadius;
        button.style.borderTopRightRadius = CornerRadius;
        button.style.borderBottomLeftRadius = CornerRadius;
        button.style.borderBottomRightRadius = CornerRadius;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
    }
}
