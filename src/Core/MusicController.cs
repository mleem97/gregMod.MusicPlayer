using System;
using MelonLoader;

namespace greg.Mods.MusicPlayer.Core;

// Thin facade over AudioBackend (core/standalone dispatch).
// This file touches NO gregCore types (JIT-safe without DLL).
public static class MusicController
{
    public static string CurrentTitle => AudioBackend.CurrentTitle;
    public static bool IsPlayingNow => AudioBackend.IsPlayingNow;

    public static void PlayTrack(MusicTrack track, float volume)
    {
        if (track == null) return;
        try
        {
            MelonLogger.Msg("[MusicPlayer] Load: '" + track.Title + "'");
            AudioBackend.PlayTrack(track, volume);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] Play: " + ex.GetBaseException().Message);
        }
    }

    public static void Pause()
    {
        try { AudioBackend.Pause(); MelonLogger.Msg("[MusicPlayer] Paused."); }
        catch { }
    }

    public static void Resume(float volume)
    {
        try { AudioBackend.Resume(volume); MelonLogger.Msg("[MusicPlayer] Resumed."); }
        catch { }
    }

    public static void Stop()
    {
        try { AudioBackend.Stop(); MelonLogger.Msg("[MusicPlayer] Stopped."); }
        catch { }
    }

    public static void ApplyVolume(float volume)
    {
        try { AudioBackend.ApplyVolume(volume); } catch { }
    }

    public static bool CheckAutoAdvance()
    {
        try { return AudioBackend.CheckAutoAdvance(); } catch { return false; }
    }

    public static void TickFade(float dt)
    {
        try { AudioBackend.TickFade(dt); } catch { }
    }

    public static string ProgressText()
    {
        try { return AudioBackend.ProgressText(); } catch { return ""; }
    }

    public static float ProgressFraction()
    {
        try { return AudioBackend.ProgressFraction(); } catch { return -1f; }
    }

    public static float RemainingSeconds()
    {
        try { return AudioBackend.RemainingSeconds(); } catch { return -1f; }
    }

    public static void Seek(float fraction)
    {
        try { AudioBackend.Seek(fraction); } catch { }
    }

    public static bool IsCached(string filePath)
    {
        try { return AudioBackend.IsCached(filePath); } catch { return false; }
    }

    public static bool IsPreloading(string filePath)
    {
        try { return AudioBackend.IsPreloading(filePath); } catch { return false; }
    }

    public static void PreloadTrack(MusicTrack track)
    {
        if (track == null) return;
        try { AudioBackend.PreloadTrack(track); } catch { }
    }
}
