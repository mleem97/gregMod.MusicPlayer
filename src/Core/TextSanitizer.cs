using System.Text.RegularExpressions;

namespace greg.Mods.MusicPlayer.Core;

// Textbereinigung fuer UI/Toast/Discord: behaelt alle Unicode-Buchstaben
// (inkl. Umlaute, CJK etc.), entfernt Emojis/Symbole (Piktogramme, Flags,
// Modifier, ZWJ-Sequenzen, Variantenselektoren). Nur reiner Text.
public static class TextSanitizer
{
    // Symbole & Piktogramme, Dingbats, div. Symbole, Variantenselektoren,
    // Skin-Tone-Modifier, Regional-Indikatoren (Flags), Tags.
    private static readonly Regex EmojiPattern = new Regex(
        "[\u2600-\u27BF\u2B00-\u2BFF\uFE00-\uFE0F\u200D\u20E3\U0001F000-\U0001FAFF\U0001F1E6-\U0001F1FF\U0001F3FB-\U0001F3FF\U000E0020-\U000E007F]",
        RegexOptions.Compiled);

    private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);

    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        try
        {
            string s = EmojiPattern.Replace(text, "");
            s = WhitespacePattern.Replace(s, " ").Trim();
            return s;
        }
        catch { return text.Trim(); }
    }
}
