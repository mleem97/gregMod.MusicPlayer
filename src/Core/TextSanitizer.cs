namespace greg.Mods.MusicPlayer.Core;

// Textbereinigung fuer UI/Toast/Discord: behaelt alle Unicode-Buchstaben
// (inkl. Umlaute, CJK etc.), entfernt Emojis/Symbole (Piktogramme, Flags,
// Modifier, ZWJ-Sequenzen, Variantenselektoren). Nur reiner Text.
//
// Absichtlich OHNE Regex: RegexOptions.Compiled braucht Codegenerierung und
// wirft auf IL2CPP beim Klassen-Init (TypeInitializationException im
// MelonLoader-Log). Manueller Scan ist deterministisch und schneller.
public static class TextSanitizer
{
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        try
        {
            var buf = new char[text.Length];
            int n = 0;
            bool lastWasSpace = true; // fuehrende Spaces gleich trimmen
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (IsRemoved(c))
                {
                    // High-Surrogate: zugehoeriges Low-Surrogate mit verwerfen.
                    if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        i++;
                    continue;
                }
                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace)
                    {
                        buf[n++] = ' ';
                        lastWasSpace = true;
                    }
                    continue;
                }
                buf[n++] = c;
                lastWasSpace = false;
            }
            // Trailing Space trimmen.
            if (n > 0 && buf[n - 1] == ' ') n--;
            return new string(buf, 0, n);
        }
        catch { return text.Trim(); }
    }

    private static bool IsRemoved(char c)
    {
        // Symbole & Piktogramme, Dingbats, div. Symbole.
        if (c >= '\u2600' && c <= '\u27BF') return true;
        if (c >= '\u2B00' && c <= '\u2BFF') return true;
        // Variantenselektoren, ZWJ, Combining Enclosing Keycap.
        if (c >= '\uFE00' && c <= '\uFE0F') return true;
        if (c == '\u200D' || c == '\u20E3') return true;
        // Surrogate (U+10000+: Smileys, Flags, Modifier, Tags) — Paar wird
        // gemeinsam verworfen (siehe Clean).
        if (c >= '\uD800' && c <= '\uDFFF') return true;
        return false;
    }
}
