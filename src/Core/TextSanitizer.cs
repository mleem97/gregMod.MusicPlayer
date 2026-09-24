namespace greg.Mods.MusicPlayer.Core;

// Text cleanup for UI/toast/Discord: keeps all Unicode letters
// (incl. umlauts, CJK etc.), removes emojis/symbols (pictograms, flags,
// modifiers, ZWJ sequences, variation selectors). Plain text only.
//
// Deliberately WITHOUT regex: RegexOptions.Compiled needs code generation and
// throws on IL2CPP at class init (TypeInitializationException in the
// MelonLoader log). Manual scanning is deterministic and faster.
public static class TextSanitizer
{
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        try
        {
            var buf = new char[text.Length];
            int n = 0;
            bool lastWasSpace = true; // trim leading spaces immediately
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (IsRemoved(c))
                {
                    // High surrogate: also discard the paired low surrogate.
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
            // Trim trailing space.
            if (n > 0 && buf[n - 1] == ' ') n--;
            return new string(buf, 0, n);
        }
        catch { return text.Trim(); }
    }

    private static bool IsRemoved(char c)
    {
        // Symbols & pictograms, dingbats, misc. symbols.
        if (c >= '\u2600' && c <= '\u27BF') return true;
        if (c >= '\u2B00' && c <= '\u2BFF') return true;
        // Variation selectors, ZWJ, combining enclosing keycap.
        if (c >= '\uFE00' && c <= '\uFE0F') return true;
        if (c == '\u200D' || c == '\u20E3') return true;
        // Surrogate (U+10000+: smileys, flags, modifiers, tags) — discarded
        // together as a pair (see Clean).
        if (c >= '\uD800' && c <= '\uDFFF') return true;
        return false;
    }
}
