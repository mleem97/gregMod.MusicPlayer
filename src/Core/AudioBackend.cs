using System;
using MelonLoader;

namespace greg.Mods.MusicPlayer.Core;

// Audio dispatch: with gregCore -> GregAudioService (cache, crossfade,
// preload, optimized). Without -> ModLocalAudio (basic functions).
// Dispatcher methods touch ONLY mod-local types (JIT-safe without DLL);
// core paths live in separate methods and run only when HasCore is set.
public static class AudioBackend
{
    public static string CurrentTitle
    {
        get
        {
            try { return GregHost.HasCore ? CoreTitle() : ModLocalAudio.CurrentTitle; }
            catch { return ""; }
        }
    }

    public static bool IsPlayingNow
    {
        get
        {
            try { return GregHost.HasCore ? CoreIsPlaying() : ModLocalAudio.IsPlaying; }
            catch { return false; }
        }
    }

    public static void PlayTrack(MusicTrack track, float volume)
    {
        if (track == null) return;
        try
        {
            if (GregHost.HasCore) CorePlay(track, volume);
            else ModLocalAudio.PlayFile(track.FilePath, track.Title, volume);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] Play: " + ex.GetBaseException().Message);
        }
    }

    public static void Pause()
    {
        try
        {
            if (GregHost.HasCore) CorePause();
            else ModLocalAudio.Pause();
        }
        catch { }
    }

    public static void Resume(float volume)
    {
        try
        {
            if (GregHost.HasCore) CoreResume(volume);
            else ModLocalAudio.Resume(volume);
        }
        catch { }
    }

    public static void Stop()
    {
        try
        {
            if (GregHost.HasCore) CoreStop();
            else ModLocalAudio.Stop();
        }
        catch { }
    }

    public static void ApplyVolume(float volume)
    {
        try
        {
            if (GregHost.HasCore) CoreVolume(volume);
            else ModLocalAudio.ApplyVolume(volume);
        }
        catch { }
    }

    public static bool CheckAutoAdvance()
    {
        try
        {
            if (GregHost.HasCore) return gregCore.PublicApi.Audio.GregAudioService.Poll();
            return ModLocalAudio.PollEnded();
        }
        catch { return false; }
    }

    public static void TickFade(float dt)
    {
        try
        {
            if (GregHost.HasCore) CoreTick(dt);
        }
        catch { }
    }

    public static string ProgressText()
    {
        try
        {
            if (GregHost.HasCore) return CoreProgressText();
            return ModLocalAudio.ProgressText();
        }
        catch { return ""; }
    }

    public static float ProgressFraction()
    {
        try
        {
            if (GregHost.HasCore) return CoreProgressFraction();
            return ModLocalAudio.ProgressFraction();
        }
        catch { return -1f; }
    }

    public static float RemainingSeconds()
    {
        try
        {
            if (GregHost.HasCore) return CoreRemaining();
            return ModLocalAudio.RemainingSeconds();
        }
        catch { return -1f; }
    }

    public static void Seek(float fraction)
    {
        try
        {
            if (GregHost.HasCore) CoreSeek(fraction);
            else ModLocalAudio.Seek(fraction);
        }
        catch { }
    }

    public static bool IsCached(string filePath)
    {
        try
        {
            if (GregHost.HasCore) return CoreIsCached(filePath);
            return false;
        }
        catch { return false; }
    }

    public static bool IsPreloading(string filePath)
    {
        try
        {
            if (GregHost.HasCore) return CoreIsPreloading(filePath);
            return false;
        }
        catch { return false; }
    }

    public static void PreloadTrack(MusicTrack track)
    {
        if (track == null) return;
        try
        {
            if (GregHost.HasCore) CorePreload(track);
            // Standalone: no preload (basic function).
        }
        catch { }
    }

    // --- Core path (only called when the DLL is present!) ---
    private static string CoreTitle() => gregCore.PublicApi.Audio.GregAudioService.CurrentTitle;
    private static bool CoreIsPlaying() => gregCore.PublicApi.Audio.GregAudioService.IsPlaying;
    private static void CorePlay(MusicTrack t, float v) => gregCore.PublicApi.Audio.GregAudioService.PlayFile(t.FilePath, t.Title, v);
    private static void CorePause() => gregCore.PublicApi.Audio.GregAudioService.Pause();
    private static void CoreResume(float v) => gregCore.PublicApi.Audio.GregAudioService.Resume(v);
    private static void CoreStop() => gregCore.PublicApi.Audio.GregAudioService.Stop();
    private static void CoreVolume(float v) => gregCore.PublicApi.Audio.GregAudioService.ApplyVolume(v);
    private static void CoreTick(float dt) => gregCore.PublicApi.Audio.GregAudioService.Tick(dt);
    private static string CoreProgressText() => gregCore.PublicApi.Audio.GregAudioService.ProgressText();
    private static float CoreProgressFraction() => gregCore.PublicApi.Audio.GregAudioService.ProgressFraction();
    private static float CoreRemaining() => gregCore.PublicApi.Audio.GregAudioService.RemainingSeconds();
    private static void CoreSeek(float f) => gregCore.PublicApi.Audio.GregAudioService.Seek(f);
    private static bool CoreIsCached(string p) => gregCore.PublicApi.Audio.GregAudioService.IsCached(p);
    private static bool CoreIsPreloading(string p) => gregCore.PublicApi.Audio.GregAudioService.IsPreloading(p);
    private static void CorePreload(MusicTrack t) => gregCore.PublicApi.Audio.GregAudioService.PreloadFile(t.FilePath, t.Title);
}
