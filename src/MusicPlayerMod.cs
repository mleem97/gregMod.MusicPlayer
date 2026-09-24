using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using greg.Mods.MusicPlayer.Core;
using gregCore.PublicApi.Audio;

[assembly: MelonInfo(typeof(greg.Mods.MusicPlayer.MusicPlayerMod), "gregMod.MusicPlayer", "1.1.0", "teamGreg")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace greg.Mods.MusicPlayer;

public class MusicPlayerMod : MelonMod
{
    internal static MelonPreferences_Entry<string> FolderEntry;
    internal static MelonPreferences_Entry<float> VolumeEntry;
    internal static MelonPreferences_Entry<bool> AutoPlayEntry;
    internal static MelonPreferences_Entry<bool> SyncEnabledEntry;
    internal static MelonPreferences_Entry<bool> SidebarRightEntry;
    internal static MelonPreferences_Entry<int> ModeEntry;
    internal static MelonPreferences_Entry<string> QueueEntry;
    internal static MelonPreferences_Entry<string> ToggleKeyEntry;
    private static UnityEngine.InputSystem.Key _toggleKey = UnityEngine.InputSystem.Key.F9;

    private static List<MusicTrack> _tracks = new List<MusicTrack>();
    private static int _index = -1;
    private static float _rescanTimer;
    private static float _progressTimer;
    private static readonly MusicQueue Queue = new MusicQueue();

    public static MusicQueue PlayQueue => Queue;

    internal static string ToggleKeyLabel
    {
        get { try { return _toggleKey.ToString(); } catch { return "F9"; } }
    }

    public static RepeatMode PlayMode
    {
        get
        {
            try
            {
                if (ModeEntry == null) return RepeatMode.All;
                int v = ModeEntry.Value;
                if (v < 0 || v > 3) return RepeatMode.All;
                return (RepeatMode)v;
            }
            catch { return RepeatMode.All; }
        }
    }

    public override void OnInitializeMelon()
    {
        var cat = MelonPreferences.CreateCategory("MusicPlayer");
        string defFolder = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "..", "Local", "Steam", "steamapps", "common", "Data Center", "Mods", "Music");
        try
        {
            defFolder = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                MelonLoader.Utils.MelonEnvironment.GameRootDirectory, "Mods", "Music"));
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] GameRootDirectory path failed: " + ex.Message);
        }
        FolderEntry = cat.CreateEntry("MusicFolder", defFolder, "MusicFolder",
            "Folder with music files (.ogg/.wav/.mp3) + optional covers (cover.jpg/png).");
        VolumeEntry = cat.CreateEntry("Volume", 0.7f, "Volume", "Volume 0.0 to 1.0.");
        AutoPlayEntry = cat.CreateEntry("AutoPlay", true, "AutoPlay", "Auto-advance to next track (host only).");
        SyncEnabledEntry = cat.CreateEntry("SyncEnabled", true, "SyncEnabled", "Sync song state via Steam lobby.");
        SidebarRightEntry = cat.CreateEntry("SidebarRight", false, "SidebarRight", "Panel as right sidebar (slides in). Off = freely draggable.");
        ModeEntry = cat.CreateEntry("PlayMode", 1, "PlayMode", "Playback mode: 0=Off, 1=All, 2=One, 3=Shuffle.");
        QueueEntry = cat.CreateEntry("SavedQueue", "", "SavedQueue", "Saved queue (file paths, one per line).");
        ToggleKeyEntry = cat.CreateEntry("ToggleKey", "F9", "ToggleKey", "Hotkey to open/close the player panel.");
        try
        {
            if (Enum.TryParse<UnityEngine.InputSystem.Key>(ToggleKeyEntry.Value, true, out var k)
                && k != UnityEngine.InputSystem.Key.None)
                _toggleKey = k;
            else
                MelonLogger.Warning($"[MusicPlayer] Unknown ToggleKey '{ToggleKeyEntry.Value}', defaulting to F9.");
        }
        catch { }

        MelonLogger.Msg("[MusicPlayer] Music folder: " + defFolder);
        if (!System.IO.Directory.Exists(defFolder))
        {
            MelonLogger.Warning("[MusicPlayer] Folder MISSING: " + defFolder);
            try
            {
                System.IO.Directory.CreateDirectory(defFolder);
                MelonLogger.Msg("[MusicPlayer] Folder created: " + defFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[MusicPlayer] Could not create folder: " + ex.Message);
            }
        }

        Rescan();
        RestoreQueue();
        if (GregHost.HasCore)
        {
            RegisterSettingsTab();
            RegisterModContract();
            RegisterHudAndOpener();
            try { CoreSubscribeTrackStarted(); } catch { }
        }
        MelonLogger.Msg("[MusicPlayer] gregMod.MusicPlayer initialized.");
        if (!GregHost.HasCore)
            MelonLogger.Msg("[MusicPlayer] Standalone mode (no gregCore): base features.");
    }

    private static void CoreSubscribeTrackStarted()
    {
        GregAudioService.TrackStarted += OnAudioTrackStarted;
    }

    private static void OnAudioTrackStarted(string filePath, string title)
    {
        try
        {
            if (GregHost.HasCore) DiscordPresence.OnTrackStarted(filePath, title, _tracks);
        }
        catch { }
    }

    // Settings hub (gregCore): second control surface next to F9 panel.
    private static void RegisterSettingsTab()
    {
        try
        {
            greg.UI.Settings.GregSettingsHub.RegisterTab("musicplayer.settings", "Music",
                (Action<gregCore.UI.GregPanelBuilder>)(b =>
                {
                    b.AddSlider("Volume", 0f, 1f, ReadVolume(), v => SetVolume(v));
                    b.AddButton("Mode: cycle (Off/All/One/Shuffle)", () => CycleMode());
                    b.AddToggle("Sync via lobby", ReadSyncFlag(), v =>
                    {
                        try { if (SyncEnabledEntry != null) SyncEnabledEntry.Value = v; } catch { }
                    });
                    b.AddToggle("Right sidebar", ReadSidebar(), v =>
                    {
                        try { if (SidebarRightEntry != null) SidebarRightEntry.Value = v; } catch { }
                    });
                    b.AddSecondaryButton("Rescan folder", () => Rescan());
                }));
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Settings tab failed: " + ex.GetBaseException().Message);
        }
    }

    private static bool ReadSidebar()
    {
        try { return SidebarRightEntry != null && SidebarRightEntry.Value; }
        catch { return false; }
    }

    // Mod contract: register with framework (name/version/menus).
    private static void RegisterModContract()
    {
        try
        {
            gregCore.Core.Mods.GregModRegistry.Register(
                "gregMod.MusicPlayer", "MusicPlayer", "1.1.0",
                new string[] { "musicplayer" });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Mod registration failed: " + ex.GetBaseException().Message);
        }
    }

    // Key HUD (right edge) + opener for F1 hub. Call only with gregCore
    // (own method for JIT split without gregCore DLL).
    private static void RegisterHudAndOpener()
    {
        try
        {
            gregCore.UI.GregHudRegistry.Register("musicplayer", _toggleKey.ToString(), "Music");
            gregCore.UI.GregMenuRegistry.RegisterOpener("musicplayer", () => UI.MusicUI.Toggle());
            gregCore.UI.GregMenuRegistry.RegisterCloser("musicplayer",
                () => { try { if (UI.MusicUI.IsVisible) UI.MusicUI.Toggle(); } catch { /* best-effort */ } });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] HUD registration failed: " + ex.GetBaseException().Message);
        }
    }

    public override void OnUpdate()
    {
        // Click routing for panel (fallback if no EventSystem delivers).
        try { UI.MusicUI.RouteClicks(); } catch { }
        // Bar dragging (seek/volume).
        try { UI.MusicUI.PollBars(); } catch { }

        try
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb[_toggleKey].wasPressedThisFrame)
            {
                UI.MusicUI.Toggle();
            }
        }
        catch { }

        _rescanTimer += Time.deltaTime;
        if (_rescanTimer > 30f)
        {
            _rescanTimer = 0f;
            Rescan();
        }

        bool sync = ReadSyncFlag();
        bool host = MusicSync.IsHost();

        if (sync && !host)
        {
            FollowHost();
            return;
        }

        if (ReadAutoPlay() && host && MusicController.CheckAutoAdvance())
        {
            AdvanceQueue();
        }

        // Tick crossfade.
        try { MusicController.TickFade(Time.deltaTime); } catch { }

        // Refresh progress ~2x/sec (only with open panel).
        _progressTimer += Time.deltaTime;
        if (_progressTimer > 0.5f)
        {
            _progressTimer = 0f;
            try { UI.MusicUI.UpdateProgress(); } catch { }
            // 30s before end: preload next track (queue wins).
            try { PreloadAhead(); } catch { }
        }
    }

    public static List<MusicTrack> Tracks => _tracks;
    public static int Index => _index;

    public static void Rescan()
    {
        string folder = "";
        try { folder = FolderEntry != null ? FolderEntry.Value : ""; } catch { }
        if (string.IsNullOrWhiteSpace(folder))
        {
            _tracks = new List<MusicTrack>();
            return;
        }
        if (!System.IO.Directory.Exists(folder))
        {
            _tracks = new List<MusicTrack>();
            return;
        }
        _tracks = MusicLibrary.Scan(folder);
        if (_index >= _tracks.Count) _index = -1;
        UI.MusicUI.Refresh();
    }

    // Library: play track immediately. If in queue, jump there,
    // else enqueue next and jump to it.
    public static void PlayIndex(int i)
    {
        if (_tracks.Count == 0) return;
        if (i < 0) i = 0;
        if (i >= _tracks.Count) i = 0;
        _index = i;
        MusicTrack track = _tracks[_index];
        if (track == null) return;
        int qi = IndexInQueue(track);
        if (qi >= 0) Queue.PlayAt(qi);
        else
        {
            Queue.EnqueueNext(track);
            Queue.PlayAt(Queue.Position + 1 <= Queue.Count - 1 ? Queue.Position + 1 : Queue.Count - 1);
        }
        SaveQueue();
        PlayQueued(track);
    }

    // Transport: next (manual) / prev.
    public static void PlayNext()
    {
        EnsureQueueFilled();
        if (Queue.Count == 0) return;
        MusicTrack next = Queue.Advance(PlayMode, true);
        if (next == null) { StopPlayback(); return; }
        SyncIndexToQueue();
        PlayQueued(next);
    }

    public static void PlayPrev()
    {
        EnsureQueueFilled();
        if (Queue.Count == 0) return;
        MusicTrack prev = Queue.StepBack();
        if (prev == null) return;
        SyncIndexToQueue();
        PlayQueued(prev);
    }

    // Play/pause master switch (toggle).
    public static void PlayToggle()
    {
        if (MusicController.IsPlayingNow) { Pause(); return; }
        PlayPressed();
    }

    // Explicit play: resume or start, never pause.
    public static void PlayPressed()
    {
        if (MusicController.IsPlayingNow) return;
        if (!string.IsNullOrEmpty(MusicController.CurrentTitle))
        {
            Resume();
            return;
        }
        EnsureQueueFilled();
        if (Queue.Count == 0) return;
        MusicTrack cur = Queue.Current ?? Queue.PlayAt(0);
        if (cur == null) return;
        SyncIndexToQueue();
        PlayQueued(cur);
    }

    // Explicit stop.
    public static void Stop()
    {
        MusicController.Stop();
        try { if (GregHost.HasCore) DiscordPresence.Reset(); } catch { }
        UI.MusicUI.PushToast("Stopped.");
        UI.MusicUI.Refresh();
        if (ReadSyncFlag() && MusicSync.IsHost())
            MusicSync.Publish(MusicController.CurrentTitle, "stopped");
    }

    public static void SetVolume(float v)
    {
        try
        {
            if (v < 0f) v = 0f;
            if (v > 1f) v = 1f;
            if (VolumeEntry != null) VolumeEntry.Value = v;
            MusicController.ApplyVolume(v);
        }
        catch { }
    }

    // Track end reached -> continue or stop per mode.
    private static void AdvanceQueue()
    {
        if (Queue.Count == 0) return;
        MusicTrack next = Queue.Advance(PlayMode, false);
        if (next == null) { StopPlayback(); return; }
        SyncIndexToQueue();
        PlayQueued(next);
    }

    private static void StopPlayback()
    {
        MusicController.Stop();
        UI.MusicUI.PushToast("End of queue.");
        UI.MusicUI.Refresh();
    }

    private static void PlayQueued(MusicTrack track)
    {
        if (track == null) return;
        MusicController.PlayTrack(track, ReadVolume());
        ShowNowPlaying(track);
        UI.MusicUI.Refresh();
        if (ReadSyncFlag() && MusicSync.IsHost())
            MusicSync.Publish(track.Title, "playing");
        // Trigger next directly (30s rule too late for short tracks).
        try { PreloadAhead(); } catch { }
    }

    // Pick next track (no switch) and preload if needed.
    private static void PreloadAhead()
    {
        try
        {
            if (!MusicSync.IsHost()) return;
            if (!MusicController.IsPlayingNow) return;
            float remaining = MusicController.RemainingSeconds();
            // Only in last third or <30s, and no permanent polling:
            // PreloadTrack is idempotent (cache/running -> no-op).
            if (remaining >= 0f && remaining < 30f)
            {
                var next = Queue.PeekNext(PlayMode);
                if (next == null) return;
                if (MusicController.IsCached(next.FilePath)) return;
                if (MusicController.IsPreloading(next.FilePath)) return;
                // Same track (RepeatOne) already cached.
                if (Queue.Current != null && string.Equals(Queue.Current.FilePath, next.FilePath,
                        StringComparison.OrdinalIgnoreCase)) return;
                MusicController.PreloadTrack(next);
            }
        }
        catch { }
    }

    // FIFA/NFSU style: "NOW PLAYING" small, TITLE bold large, artist smaller.
    // Plus cover from ID3 (fallback: note icon). Text sanitized (emoji-free).
    private static void CoreNowPlaying(string title, string artist, MusicTrack track)
    {
        UnityEngine.Texture2D cover = null;
        try { cover = track.CoverTexture; } catch { }
        gregCore.UI.GregNotificationManager.ShowRich(
            "NOW PLAYING", title, artist, cover, "note", 5f);
    }

    private static void ShowNowPlaying(MusicTrack track)
    {
        try
        {
            string title = TextSanitizer.Clean(track.DisplayTitle);
            if (!string.IsNullOrEmpty(title)) title = title.ToUpperInvariant();
            if (string.IsNullOrEmpty(title)) title = track.Title;
            string artist = TextSanitizer.Clean(track.Id3Artist ?? "");
            if (GregHost.HasCore) { CoreNowPlaying(title, artist, track); return; }
            UI.MusicUI.PushToast("Playing: " + track.Title);
        }
        catch
        {
            UI.MusicUI.PushToast("Playing: " + track.Title);
        }
    }

    private static void EnsureQueueFilled()
    {
        if (Queue.Count == 0 && _tracks.Count > 0)
        {
            Queue.ReplaceAll(_tracks);
            SaveQueue();
        }
    }

    private static int IndexInQueue(MusicTrack track)
    {
        if (track == null) return -1;
        var items = Queue.Items;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && string.Equals(items[i].FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static void SyncIndexToQueue()
    {
        try
        {
            var cur = Queue.Current;
            _index = cur != null ? _tracks.IndexOf(cur) : -1;
        }
        catch { }
    }

    // --- Queue-Verwaltung (UI) ---

    public static void EnqueueTrack(int libraryIndex)
    {
        if (libraryIndex < 0 || libraryIndex >= _tracks.Count) return;
        var t = _tracks[libraryIndex];
        if (t == null) return;
        Queue.Enqueue(t);
        SaveQueue();
        UI.MusicUI.PushToast("Queued: " + t.Title);
        UI.MusicUI.Refresh();
    }

    public static void PlayQueueAt(int queueIndex)
    {
        var t = Queue.PlayAt(queueIndex);
        if (t == null) return;
        SaveQueue();
        SyncIndexToQueue();
        PlayQueued(t);
    }

    public static void RemoveQueueAt(int queueIndex)
    {
        Queue.RemoveAt(queueIndex);
        SaveQueue();
        SyncIndexToQueue();
        UI.MusicUI.Refresh();
    }

    public static void ClearQueue()
    {
        Queue.Clear();
        SaveQueue();
        _index = -1;
        UI.MusicUI.Refresh();
    }

    public static void CycleMode()
    {
        int next = ((int)PlayMode + 1) % 4;
        try { if (ModeEntry != null) ModeEntry.Value = next; } catch { }
        UI.MusicUI.PushToast("Mode: " + MusicQueue.ModeLabel((RepeatMode)next));
        UI.MusicUI.Refresh();
    }

    private static void SaveQueue()
    {
        try
        {
            if (QueueEntry == null) return;
            var parts = new List<string>();
            foreach (var t in Queue.Items)
            {
                if (t != null && !string.IsNullOrEmpty(t.FilePath)) parts.Add(t.FilePath);
            }
            QueueEntry.Value = string.Join("\n", parts.ToArray());
        }
        catch { }
    }

    private static void RestoreQueue()
    {
        try
        {
            if (QueueEntry == null) return;
            string saved = QueueEntry.Value ?? "";
            if (string.IsNullOrWhiteSpace(saved)) return;
            foreach (string line in saved.Split('\n'))
            {
                string p = line.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                foreach (var t in _tracks)
                {
                    if (t != null && string.Equals(t.FilePath, p, StringComparison.OrdinalIgnoreCase))
                    {
                        Queue.Enqueue(t);
                        break;
                    }
                }
            }
            if (Queue.Count > 0)
                MelonLogger.Msg($"[MusicPlayer] Queue restored: {Queue.Count} tracks.");
        }
        catch { }
    }

    public static void Pause()
    {
        MusicController.Pause();
        UI.MusicUI.Refresh();
        if (ReadSyncFlag() && MusicSync.IsHost())
            MusicSync.Publish(MusicController.CurrentTitle, "paused");
    }

    public static void Resume()
    {
        MusicController.Resume(ReadVolume());
        UI.MusicUI.Refresh();
        if (ReadSyncFlag() && MusicSync.IsHost())
            MusicSync.Publish(MusicController.CurrentTitle, "playing");
    }

    private static void FollowHost()
    {
        try
        {
            string track;
            string state;
            if (!MusicSync.Poll(Time.deltaTime, out track, out state)) return;
            if (string.IsNullOrEmpty(track)) return;
            if (track != MusicController.CurrentTitle)
            {
                MusicTrack local = MusicLibrary.FindByTitle(_tracks, track);
                if (local != null)
                {
                    _index = _tracks.IndexOf(local);
                    MusicController.PlayTrack(local, ReadVolume());
                    UI.MusicUI.PushToast("Sync: " + local.Title);
                }
                else
                {
                    UI.MusicUI.PushToast("Sync: '" + track + "' missing locally.");
                }
            }
            if (state == "paused" && MusicController.IsPlayingNow) MusicController.Pause();
            else if (state == "stopped" && MusicController.IsPlayingNow) MusicController.Stop();
            else if (state == "playing" && !MusicController.IsPlayingNow &&
                     track == MusicController.CurrentTitle) MusicController.Resume(ReadVolume());
        }
        catch { }
    }

    private static float ReadVolume()
    {
        try { return VolumeEntry != null ? VolumeEntry.Value : 0.7f; }
        catch { return 0.7f; }
    }

    private static bool ReadAutoPlay()
    {
        try { return AutoPlayEntry != null && AutoPlayEntry.Value; }
        catch { return false; }
    }

    private static bool ReadSyncFlag()
    {
        try { return SyncEnabledEntry != null && SyncEnabledEntry.Value; }
        catch { return false; }
    }
}
