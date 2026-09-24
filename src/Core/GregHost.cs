using System;

namespace greg.Mods.MusicPlayer.Core;

// Detects at runtime whether gregCore is present (without a hard dependency
// at runtime: pure type-name lookup, no direct type access).
// With core: optimized central services. Without: mod-local minimal
// implementations (standalone mode, basic functions only).
// IMPORTANT: methods touching gregCore types may ONLY be called
// when HasCore is true (otherwise JIT TypeLoad with a missing DLL).
public static class GregHost
{
    private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
    private static bool? _hasCore;

    public static bool HasCore
    {
        get
        {
            if (_hasCore == null)
            {
                try { _hasCore = Type.GetType(ProbeType) != null; }
                catch { _hasCore = false; }
            }
            return _hasCore.Value;
        }
    }

    // Testing only (e.g. force standalone behavior).
    public static void OverrideForTesting(bool? value)
    {
        _hasCore = value;
    }
}
