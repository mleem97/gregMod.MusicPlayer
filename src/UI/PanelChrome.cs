using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;
using greg.Mods.MusicPlayer.Core;

namespace greg.Mods.MusicPlayer.UI;

// Panel-Chrome hinter Interface: mit gregCore (GregPanel: Slide/Drag/
// Registry) oder standalone (eigene UIDocument, sofort sichtbar, Drag).
// Aufrufer duerfen NUR das Interface anfassen (JIT-Sicherheit ohne DLL).
public interface IPanelChrome
{
    VisualElement Root { get; }
    VisualElement Content { get; }
    bool IsVisible { get; }
    void Show();
    void Hide();
    void Toggle();
    void Configure(bool sidebar, float width);
    void SetDragHandle(VisualElement handle);
    void Rebuild(Action<VisualElement> build);
    void Tick();
}

public static class PanelChromeFactory
{
    public static IPanelChrome Create()
    {
        if (GregHost.HasCore)
        {
            try { return new CoreChrome(); } catch { }
        }
        return new LocalChrome();
    }

    // --- gregCore-Implementierung (nur bei vorhandener DLL aufrufen!) ---
    private sealed class CoreChrome : IPanelChrome
    {
        private gregCore.UI.GregPanel _panel;

        public VisualElement Root => _panel != null ? _panel.Root : null;
        public VisualElement Content => _panel != null ? _panel.Content : null;
        public bool IsVisible => _panel != null && _panel.IsVisible;

        private void Ensure(bool sidebar, float width)
        {
            if (_panel != null)
            {
                _panel.Configure(new gregCore.UI.GregMenuOptions
                {
                    LockCamera = true,
                    LockMovement = true,
                    LockInteract = true,
                    ShowCursor = true,
                    Draggable = !sidebar,
                    SlideFromRight = sidebar,
                    PanelWidth = width,
                });
                return;
            }
            _panel = gregCore.UI.GregPanel.GetOrCreate("musicplayer", "MUSICPLAYER",
                new gregCore.UI.GregMenuOptions
                {
                    LockCamera = true,
                    LockMovement = true,
                    LockInteract = true,
                    ShowCursor = true,
                    Draggable = !sidebar,
                    SlideFromRight = sidebar,
                    PanelWidth = width,
                });
        }

        public void Show() { Ensure(false, 420f); if (_panel != null) _panel.Show(); }
        public void Hide() { if (_panel != null) _panel.Hide(); }
        public void Toggle()
        {
            Ensure(false, 420f);
            if (_panel != null) _panel.Toggle();
        }

        public void Configure(bool sidebar, float width)
        {
            Ensure(sidebar, width);
        }

        public void SetDragHandle(VisualElement handle)
        {
            if (_panel != null) _panel.SetDragHandle(handle);
        }

        public void Rebuild(Action<VisualElement> build)
        {
            if (_panel != null) _panel.RebuildContent(build);
        }

        public void Tick() { /* Slide/Drag treibt die zentrale Registry */ }
    }

    // --- Standalone-Implementierung (keine gregCore-Typen!) ---
    private sealed class LocalChrome : IPanelChrome
    {
        private VisualElement _root;
        private VisualElement _content;
        private bool _visible;
        private bool _sidebar = true;
        private float _width = 420f;
        private VisualElement _dragHandle;
        private bool _dragging;
        private Vector2 _dragOffset;

        public VisualElement Root => _root;
        public VisualElement Content => _content;
        public bool IsVisible => _visible;

        private void Ensure()
        {
            if (_root != null) return;
            var layer = ModLocalUI.GetLayerRoot();
            if (layer == null) return;
            _root = new VisualElement();
            _root.name = "MusicPlayerPanel";
            _root.style.position = Position.Absolute;
            _root.style.width = _width;
            _root.style.backgroundColor = ModTheme.PanelBackground;
            _root.style.borderTopLeftRadius = ModTheme.CornerRadius;
            _root.style.borderTopRightRadius = ModTheme.CornerRadius;
            _root.style.borderBottomLeftRadius = ModTheme.CornerRadius;
            _root.style.borderBottomRightRadius = ModTheme.CornerRadius;
            _root.style.borderLeftWidth = ModTheme.BorderWidth;
            _root.style.borderRightWidth = ModTheme.BorderWidth;
            _root.style.borderTopWidth = ModTheme.BorderWidth;
            _root.style.borderBottomWidth = ModTheme.BorderWidth;
            _root.style.borderLeftColor = ModTheme.NeutralBorder;
            _root.style.borderRightColor = ModTheme.NeutralBorder;
            _root.style.borderTopColor = ModTheme.NeutralBorder;
            _root.style.borderBottomColor = ModTheme.NeutralBorder;
            _root.style.paddingLeft = ModTheme.Padding;
            _root.style.paddingRight = ModTheme.Padding;
            _root.style.paddingTop = 12f;
            _root.style.paddingBottom = 12f;
            _root.style.display = DisplayStyle.None;
            var title = new Label("MUSICPLAYER");
            ModTheme.ApplyTextStyle(title, true);
            _root.Add(title);
            _dragHandle = title;
            _content = new VisualElement();
            _content.name = "Content";
            _content.style.flexGrow = 1f;
            _root.Add(_content);
            layer.Add(_root);
            Snap();
        }

        public void Show()
        {
            Ensure();
            if (_root == null) return;
            _visible = true;
            _root.style.display = DisplayStyle.Flex;
            Snap();
        }

        public void Hide()
        {
            _visible = false;
            _dragging = false;
            if (_root != null)
            {
                try { _root.style.display = DisplayStyle.None; } catch { }
            }
        }

        public void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        public void Configure(bool sidebar, float width)
        {
            _sidebar = sidebar;
            if (width > 0f) _width = width;
            if (_root != null)
            {
                try { _root.style.width = _width; } catch { }
                if (_visible) Snap();
            }
        }

        public void SetDragHandle(VisualElement handle)
        {
            if (handle != null) _dragHandle = handle;
        }

        public void Rebuild(Action<VisualElement> build)
        {
            if (_content == null || build == null) return;
            try
            {
                _content.Clear();
                build(_content);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MusicPlayer] Panel-Rebuild fehlgeschlagen: " + ex.Message);
            }
        }

        public void Tick()
        {
            if (!_visible || _sidebar) { _dragging = false; return; }
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse == null || _dragHandle == null || _root == null) return;
                Vector2 pos = mouse.position.ReadValue();
                pos.y = Screen.height - pos.y;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    try
                    {
                        Rect b = _dragHandle.worldBound;
                        if (b.width > 0f && b.height > 0f && b.Contains(pos))
                        {
                            _dragging = true;
                            Rect r = _root.worldBound;
                            _dragOffset = new Vector2(pos.x - r.x, pos.y - r.y);
                        }
                    }
                    catch { }
                    return;
                }
                if (_dragging)
                {
                    if (mouse.leftButton.isPressed)
                    {
                        float nx = pos.x - _dragOffset.x;
                        float ny = pos.y - _dragOffset.y;
                        if (nx < 0f) nx = 0f;
                        if (ny < 0f) ny = 0f;
                        _root.style.left = nx;
                        _root.style.top = ny;
                    }
                    else _dragging = false;
                }
            }
            catch { }
        }

        private void Snap()
        {
            if (_root == null) return;
            try
            {
                if (_sidebar)
                {
                    float x = Screen.width - _width - 16f;
                    _root.style.left = x < 0f ? 0f : x;
                    _root.style.top = 60f;
                }
                else
                {
                    _root.style.left = 16f;
                    _root.style.top = 60f;
                }
            }
            catch { }
        }
    }
}
