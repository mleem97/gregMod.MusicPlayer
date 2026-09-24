using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;
using greg.Mods.MusicPlayer.Core;

namespace greg.Mods.MusicPlayer.UI;

// UI Toolkit panel on gregCore layer root + manual click routing.
// Reason: IMGUI is largely stripped from IL2CPP build
// ("Method unstripping failed"), and no EventSystem exists that
// would deliver toolkit clicks. Instead: worldBound hit test with
// mouse polling (InputSystem, proven working).
// No MonoBehaviour, no OnGUI, no registration needed.
public static class MusicUI
{
    private sealed class Clickable
    {
        public VisualElement Element;
        public Action Action;
    }

    private const int TracksPerPage = 8;

    private static Label _progressLabel;

    private static IPanelChrome _chrome;
    private static VisualElement _content;
    private static VisualElement _seekBar;
    private static VisualElement _seekFill;
    private static VisualElement _volBar;
    private static VisualElement _volFill;
    private static bool _seeking;
    private static bool _volSeeking;
    private static int _page;
    private static bool _queueOpen = true;
    private static bool _libOpen;
    private static DateTime _lastRealClickUtc = DateTime.MinValue;
    private static readonly List<Clickable> _clickables = new List<Clickable>();
    private static readonly List<Label> _labels = new List<Label>();

    public static bool IsVisible => _chrome != null && _chrome.IsVisible;

    public static void Toggle()
    {
        try
        {
            if (_chrome == null) _chrome = PanelChromeFactory.Create();
            if (_chrome == null) return;
            _chrome.Configure(ReadSidebar(), 420f);
            bool willShow = !_chrome.IsVisible;
            if (willShow) Rebuild();
            _chrome.Toggle();
            MelonLogger.Msg("[MusicPlayer] Panel " + (_chrome.IsVisible ? "shown." : "hidden."));
            try { if (GregHost.HasCore) ReportOpenState(); } catch { /* best-effort */ }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] UI toggle failed: " + ex.GetBaseException().Message);
        }
    }

    // Separate method (JIT split): reports panel state to F1 hub.
    private static void ReportOpenState()
    {
        try { gregCore.UI.GregMenuRegistry.SetOpen("musicplayer", IsVisible); } catch { /* best-effort */ }
    }

    public static void Refresh()
    {
        try
        {
            if (_chrome == null || !_chrome.IsVisible) return;
            Rebuild();
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] UI refresh failed: " + ex.Message);
        }
    }

    public static void PushToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        try
        {
            if (GregHost.HasCore) CoreToast(message);
            else ModLocalUI.ShowToast("[Music] " + message, 5f);
        }
        catch { }
    }

    private static void CoreToast(string message)
    {
        gregCore.UI.GregNotificationManager.Show("[Music] " + message, 5f);
    }

    // Every frame from mod OnUpdate: forwards mouse clicks to visible buttons
    // (replacement for missing EventSystem).
    public static void RouteClicks()
    {
        if (!IsVisible || _clickables.Count == 0) return;
        try
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            Vector2 pos = mouse.position.ReadValue();
            // Real clicks win: if one arrived <500ms ago, stay silent.
            try
            {
                if ((DateTime.UtcNow - _lastRealClickUtc).TotalMilliseconds < 500.0) return;
            }
            catch { }
            pos.y = Screen.height - pos.y;
            for (int i = _clickables.Count - 1; i >= 0; i--)
            {
                var c = _clickables[i];
                if (c == null || c.Element == null || c.Action == null) continue;
                try
                {
                    if (!c.Element.visible) continue;
                    Rect b = c.Element.worldBound;
                    if (b.width <= 0f || b.height <= 0f) continue;
                    if (b.Contains(pos))
                    {
                        c.Action();
                        return;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Click routing failed: " + ex.Message);
        }
    }

    private static bool ReadSidebar()
    {
        try { return MusicPlayerMod.SidebarRightEntry != null && MusicPlayerMod.SidebarRightEntry.Value; }
        catch { return false; }
    }

    // Update progress directly (no rebuild): text + fill level.
    public static void UpdateProgress()
    {
        try
        {
            if (!IsVisible) return;
            if (_progressLabel != null) _progressLabel.text = MusicController.ProgressText();
            if (!_seeking) RefreshSeekFill();
        }
        catch { }
    }

    // Bar drag logic: call from mod OnUpdate. Click on bar jumps
    // immediately, hold+drag seeks or adjusts (volume).
    public static void PollBars()
    {
        if (!IsVisible) { _seeking = false; _volSeeking = false; return; }
        try
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();
            pos.y = Screen.height - pos.y;
            bool down = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;

            if (down && Hit(_seekBar, pos)) { _seeking = true; ApplySeek(pos); }
            else if (_seeking && held) ApplySeek(pos);
            else if (_seeking && !held) _seeking = false;

            if (down && Hit(_volBar, pos)) { _volSeeking = true; ApplyVol(pos); }
            else if (_volSeeking && held) ApplyVol(pos);
            else if (_volSeeking && !held) _volSeeking = false;
        }
        catch { }
    }

    private static bool Hit(VisualElement el, Vector2 pos)
    {
        try
        {
            if (el == null || !el.visible) return false;
            Rect b = el.worldBound;
            if (b.width <= 0f || b.height <= 0f) return false;
            return b.Contains(pos);
        }
        catch { return false; }
    }

    private static float FractionAt(VisualElement bar, Vector2 pos)
    {
        try
        {
            Rect b = bar.worldBound;
            if (b.width <= 0f) return 0f;
            float f = (pos.x - b.x) / b.width;
            if (f < 0f) return 0f;
            if (f > 1f) return 1f;
            return f;
        }
        catch { return 0f; }
    }

    private static void ApplySeek(Vector2 pos)
    {
        try
        {
            if (_seekBar == null) return;
            float f = FractionAt(_seekBar, pos);
            MusicController.Seek(f);
            SetFill(_seekFill, f);
            if (_progressLabel != null) _progressLabel.text = MusicController.ProgressText();
        }
        catch { }
    }

    private static void ApplyVol(Vector2 pos)
    {
        try
        {
            if (_volBar == null) return;
            float f = FractionAt(_volBar, pos);
            MusicPlayerMod.SetVolume(f);
            SetFill(_volFill, f);
            Refresh();
        }
        catch { }
    }

    private static void RefreshSeekFill()
    {
        try
        {
            float f = MusicController.ProgressFraction();
            SetFill(_seekFill, f < 0f ? 0f : f);
        }
        catch { }
    }

    private static void RefreshVolFill()
    {
        try { SetFill(_volFill, ReadVolume()); } catch { }
    }

    private static void SetFill(VisualElement fill, float fraction)
    {
        try
        {
            if (fill == null) return;
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;
            fill.style.width = new Length(fraction * 100f, LengthUnit.Percent);
        }
        catch { }
    }

    private static VisualElement Bar(Color bg)
    {
        var bar = new VisualElement();
        bar.style.height = 22f;
        bar.style.backgroundColor = new Color(bg.r, bg.g, bg.b, 0.35f);
        bar.style.borderTopLeftRadius = 4f;
        bar.style.borderTopRightRadius = 4f;
        bar.style.borderBottomLeftRadius = 4f;
        bar.style.borderBottomRightRadius = 4f;
        bar.style.marginTop = 4f;
        bar.style.marginBottom = 8f;
        return bar;
    }

    private static VisualElement Fill(Color c)
    {
        var fill = new VisualElement();
        fill.style.position = Position.Absolute;
        fill.style.left = 0f;
        fill.style.top = 0f;
        fill.style.bottom = 0f;
        fill.style.width = new Length(0f, LengthUnit.Percent);
        fill.style.backgroundColor = c;
        fill.style.borderTopLeftRadius = 4f;
        fill.style.borderBottomLeftRadius = 4f;
        fill.style.borderBottomRightRadius = 4f;
        fill.style.borderTopRightRadius = 4f;
        fill.pickingMode = PickingMode.Ignore;
        return fill;
    }

    private static void Rebuild()
    {
        if (_chrome == null) return;
        _content = _chrome.Content;
        if (_content == null) return;
        bool host = true;
        try { host = MusicSync.IsHost(); } catch { }

        _content.Clear();
        _clickables.Clear();
        _labels.Clear();
        _progressLabel = null;

        string current = MusicController.CurrentTitle;
        bool playing = MusicController.IsPlayingNow;
        var queue = MusicPlayerMod.PlayQueue;

        var title = MkLabel("MUSICPLAYER");
        ModTheme.ApplyTextStyle(title, true);
        _content.Add(title);

        var status = MkLabel((playing ? "Playing: " : "Ready: ")
            + (string.IsNullOrEmpty(current) ? "-" : current));
        ModTheme.ApplyTextStyle(status, false);
        _content.Add(status);

        _progressLabel = MkLabel(MusicController.ProgressText());
        ModTheme.ApplyTextStyle(_progressLabel, false);
        _content.Add(_progressLabel);

        AddSeparator();

        if (host)
        {
            // 1. Transport bar: prev / play / pause / stop / next.
            var row = Row();
            AddIconBtn(row, "prev", () => { try { MusicPlayerMod.PlayPrev(); } catch { } });
            AddIconBtn(row, "play", () => { try { MusicPlayerMod.PlayPressed(); } catch { } });
            AddIconBtn(row, "pause", () => { try { MusicPlayerMod.Pause(); } catch { } });
            AddIconBtn(row, "stop", () => { try { MusicPlayerMod.Stop(); } catch { } });
            AddIconBtn(row, "next", () => { try { MusicPlayerMod.PlayNext(); } catch { } });
            _content.Add(row);

            // 2. Fortschritt: klick-/ziehbare Seekbar + Zeittext.
            _seekBar = Bar(ModTheme.NeutralBorder);
            _seekFill = Fill(ModTheme.PrimaryAccent);
            _seekBar.Add(_seekFill);
            _content.Add(_seekBar);

            var modeRow = Row();
            AddBtn(modeRow, "Mode: " + MusicQueue.ModeLabel(MusicPlayerMod.PlayMode),
                () => { try { MusicPlayerMod.CycleMode(); } catch { } }, false);
            AddBtn(modeRow, "Clear queue", () => { try { MusicPlayerMod.ClearQueue(); } catch { } }, false);
            _content.Add(modeRow);

            // Lautstaerke: Balken (klick/zieh) + Wert.
            var volRow = Row();
            var volIcon = IconElement("volume", 24f);
            if (volIcon != null) volRow.Add(volIcon);
            else
            {
                var volLabel = MkLabel("Volume");
                ModTheme.ApplyTextStyle(volLabel, false);
                volLabel.style.width = 90f;
                volRow.Add(volLabel);
            }
            _volBar = Bar(ModTheme.NeutralBorder);
            _volBar.style.flexGrow = 1f;
            _volFill = Fill(ModTheme.SecondaryColor);
            _volBar.Add(_volFill);
            volRow.Add(_volBar);
            float vol = ReadVolume();
            var volVal = MkLabel(vol.ToString("0.00"));
            ModTheme.ApplyTextStyle(volVal, false);
            volVal.style.width = 40f;
            volRow.Add(volVal);
            _content.Add(volRow);
            RefreshVolFill();

            AddBtn(_content, "Rescan", () => { try { MusicPlayerMod.Rescan(); } catch { } }, false);
        }
        else
        {
            var note = MkLabel("Control: Host (sync active).");
            ModTheme.ApplyTextStyle(note, false);
            _content.Add(note);
        }

        AddSeparator();

        // --- Queue (collapsible, default open) ---
        AddBtn(_content, (_queueOpen ? "Queue ▾ (" : "Queue ▸ (") + queue.Count + ")",
            () => { try { _queueOpen = !_queueOpen; Rebuild(); } catch { } }, false);
        if (_queueOpen)
        {
        if (queue.Count == 0)
        {
            var empty = MkLabel("Empty - enqueue tracks with +.");
            ModTheme.ApplyTextStyle(empty, false);
            _content.Add(empty);
        }
        else
        {
            int show = Math.Min(queue.Count, 10);
            for (int i = 0; i < show; i++)
            {
                int local = i;
                var item = queue.Items[i];
                string mark = i == queue.Position ? "> " : (i + 1) + ". ";
                string t = item != null ? item.Title : "?";
                var qrow = Row();
                var jump = new Button();
                jump.text = mark + t;
                jump.style.height = 30f;
                jump.style.flexGrow = 1f;
                ModTheme.ApplySecondaryButtonStyle(jump);
                try { jump.style.color = new Color(0.88f, 0.88f, 0.88f); } catch { }
                try
                {
                    jump.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                    {
                        try { _lastRealClickUtc = DateTime.UtcNow; if (host) MusicPlayerMod.PlayQueueAt(local); } catch { }
                    }));
                }
                catch { }
                qrow.Add(jump);
                _clickables.Add(new Clickable { Element = jump, Action = () => { try { if (host) MusicPlayerMod.PlayQueueAt(local); } catch { } } });
                _labels.Add(null);
                var rm = new Button();
                rm.text = TryIconBg(rm, "x") ? "" : "x";
                rm.style.height = 30f;
                rm.style.width = 36f;
                ModTheme.ApplySecondaryButtonStyle(rm);
                try
                {
                    rm.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                    {
                        try { _lastRealClickUtc = DateTime.UtcNow; if (host) MusicPlayerMod.RemoveQueueAt(local); } catch { }
                    }));
                }
                catch { }
                qrow.Add(rm);
                _clickables.Add(new Clickable { Element = rm, Action = () => { try { if (host) MusicPlayerMod.RemoveQueueAt(local); } catch { } } });
                _content.Add(qrow);
            }
            if (queue.Count > show)
            {
                var more = MkLabel("... and " + (queue.Count - show) + " more.");
                ModTheme.ApplyTextStyle(more, false);
                _content.Add(more);
            }
        }
        }

        AddSeparator();

        // --- Library (collapsible, default collapsed) ---
        var tracks = MusicPlayerMod.Tracks;
        int count = tracks != null ? tracks.Count : 0;
        AddBtn(_content, (_libOpen ? "Library ▾ (" : "Library ▸ (") + count + ")",
            () => { try { _libOpen = !_libOpen; Rebuild(); } catch { } }, false);
        if (_libOpen)
        {
        if (count == 0)
        {
            var none = MkLabel("No tracks found.");
            ModTheme.ApplyTextStyle(none, false);
            _content.Add(none);
        }
        else
        {
            int pages = Math.Max(1, (count + TracksPerPage - 1) / TracksPerPage);
            if (_page >= pages) _page = pages - 1;
            if (_page < 0) _page = 0;
            if (pages > 1)
            {
                var prow = Row();
                AddBtn(prow, "< Page", () => { _page = (_page - 1 + pages) % pages; Rebuild(); }, false);
                AddBtn(prow, "Page >", () => { _page = (_page + 1) % pages; Rebuild(); }, false);
                _content.Add(prow);
            }
            int idx = MusicPlayerMod.Index;
            int start = _page * TracksPerPage;
            int end = Math.Min(start + TracksPerPage, count);
            for (int i = start; i < end; i++)
            {
                int local = i;
                string mark = i == idx ? "> " : "";
                string t = (tracks[i] != null ? tracks[i].Title : "?");
                var lrow = Row();
                var play = new Button();
                play.text = mark + t;
                play.style.height = 30f;
                play.style.flexGrow = 1f;
                ModTheme.ApplySecondaryButtonStyle(play);
                try { play.style.color = new Color(0.88f, 0.88f, 0.88f); } catch { }
                try
                {
                    play.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                    {
                        try { _lastRealClickUtc = DateTime.UtcNow; if (host) MusicPlayerMod.PlayIndex(local); } catch { }
                    }));
                }
                catch { }
                lrow.Add(play);
                _clickables.Add(new Clickable { Element = play, Action = () => { try { if (host) MusicPlayerMod.PlayIndex(local); } catch { } } });
                var plus = new Button();
                plus.text = TryIconBg(plus, "plus") ? "" : "+";
                plus.style.height = 30f;
                plus.style.width = 36f;
                ModTheme.ApplySecondaryButtonStyle(plus);
                try
                {
                    plus.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
                    {
                        try { _lastRealClickUtc = DateTime.UtcNow; if (host) MusicPlayerMod.EnqueueTrack(local); } catch { }
                    }));
                }
                catch { }
                lrow.Add(plus);
                _clickables.Add(new Clickable { Element = plus, Action = () => { try { if (host) MusicPlayerMod.EnqueueTrack(local); } catch { } } });
                _content.Add(lrow);
            }
        }
        }

        AddBtn(_content, "Close (" + MusicPlayerMod.ToggleKeyLabel + ")", () => { try { Toggle(); } catch { } }, false);
        ApplyFont();
    }

    private static Label MkLabel(string text)
    {
        var l = new Label(text);
        _labels.Add(l);
        return l;
    }

    // Toolkit default font is unusable in IL2CPP builds (invisible text).
    // So assign game font from gregCore (LegacyRuntime / game font).
    private static void ApplyFont()
    {
        Font f = null;
        try { f = GregHost.HasCore ? CoreFont() : ModLocalUI.ResolveFont(); } catch { }
        if (f == null) return;
        try
        {
            foreach (var l in _labels)
            {
                try { if (l != null) l.style.unityFont = f; } catch { }
            }
            foreach (var c in _clickables)
            {
                try
                {
                    var b = c != null ? c.Element as Button : null;
                    if (b != null) b.style.unityFont = f;
                }
                catch { }
            }
        }
        catch { }
    }

    private static Font CoreFont()
    {
        return gregCore.UI.GregFontLoader.DefaultUGUIFont;
    }

    private static float ReadVolume()
    {
        try { return MusicPlayerMod.VolumeEntry != null ? MusicPlayerMod.VolumeEntry.Value : 0.7f; }
        catch { return 0.7f; }
    }

    private static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 6f;
        return row;
    }

    private static void AddSeparator()
    {
        var sep = new VisualElement();
        sep.style.height = 2f;
        sep.style.backgroundColor = ModTheme.NeutralBorder;
        sep.style.marginTop = 6f;
        sep.style.marginBottom = 6f;
        _content.Add(sep);
    }

    private static VisualElement IconElement(string name, float size)
    {
        try
        {
            if (GregHost.HasCore) return CoreIcon(name, size);
            return null;
        }
        catch { return null; }
    }

    private static VisualElement CoreIcon(string name, float size)
    {
        return gregCore.UI.GregIconToolkit.Icon(name, size);
    }

    private static bool TryIconBg(VisualElement el, string name)
    {
        try
        {
            if (GregHost.HasCore) return CoreIconBg(el, name);
            return false;
        }
        catch { return false; }
    }

    private static bool CoreIconBg(VisualElement el, string name)
    {
        return gregCore.UI.GregIconToolkit.ApplyBackground(el, name);
    }

    private static void AddIconBtn(VisualElement parent, string icon, Action action)
    {
        var btn = new Button();
        btn.text = "";
        btn.style.height = 36f;
        btn.style.flexGrow = 1f;
        ModTheme.ApplySecondaryButtonStyle(btn);
        if (!TryIconBg(btn, icon))
            btn.text = icon;
        try
        {
            btn.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
            {
                try { _lastRealClickUtc = DateTime.UtcNow; action?.Invoke(); } catch { }
            }));
        }
        catch { }
        parent.Add(btn);
        _clickables.Add(new Clickable { Element = btn, Action = action });
    }

    private static void AddBtn(VisualElement parent, string label, Action action, bool primary, float width = 0f)
    {
        var btn = new Button();
        btn.text = label;
        btn.style.height = 34f;
        btn.style.marginBottom = 6f;
        if (width > 0f) btn.style.width = width;
        else btn.style.flexGrow = 1f;
        if (primary) ModTheme.ApplyPrimaryButtonStyle(btn);
        else
        {
            ModTheme.ApplySecondaryButtonStyle(btn);
            try { btn.style.color = new Color(0.88f, 0.88f, 0.88f); } catch { }
        }
        // Real toolkit callback (works once an EventSystem is active
        // - see GregUIInputSystem). Manual routing stays fallback,
        // yields by timestamp once real clicks arrive.
        try
        {
            btn.RegisterCallback<ClickEvent>(new Action<ClickEvent>(_ =>
            {
                try { _lastRealClickUtc = DateTime.UtcNow; action?.Invoke(); } catch { }
            }));
        }
        catch { }
        parent.Add(btn);
        _clickables.Add(new Clickable { Element = btn, Action = action });
    }
}
