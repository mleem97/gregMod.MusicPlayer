using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;
using greg.Mods.MusicPlayer.Core;

namespace greg.Mods.MusicPlayer.UI;

// Standalone-UI-Schicht (ohne gregCore): eigene UIDocument, Toasts,
// Input-Guard, Font. Wird nur benutzt wenn GregHost.HasCore == false.
// Alle Methoden sind frei von gregCore-Typen (JIT-sicher).
public static class ModLocalUI
{
    private sealed class Toast
    {
        public VisualElement Element;
        public float Until;
    }

    private static VisualElement _layer;
    private static readonly List<Toast> _toasts = new List<Toast>();
    private static Font _font;
    private static bool _fontTried;

    private static readonly List<UnityEngine.InputSystem.PlayerInput> _suspended =
        new List<UnityEngine.InputSystem.PlayerInput>();
    private static bool _lockApplied;
    private static float _nextRescanRealtime;
    private static bool _wantLock;

    public static VisualElement GetLayerRoot()
    {
        if (_layer != null) return _layer;
        try
        {
            var go = new GameObject("gregMusicUILayer");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var doc = go.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.sortingOrder = 1000;
            doc.panelSettings = ps;
            var root = doc.rootVisualElement;
            if (root == null) return null;
            _layer = new VisualElement();
            _layer.name = "MusicUILayer";
            _layer.style.flexGrow = 1f;
            _layer.pickingMode = PickingMode.Ignore;
            root.Add(_layer);
            return _layer;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Layer-Aufbau fehlgeschlagen: " + ex.Message);
            return null;
        }
    }

    public static Font ResolveFont()
    {
        if (_font != null) return _font;
        if (_fontTried) return null;
        _fontTried = true;
        try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        return _font;
    }

    public static void ShowToast(string message, float duration = 5f)
    {
        if (string.IsNullOrEmpty(message)) return;
        try
        {
            var layer = GetLayerRoot();
            if (layer == null) return;
            var box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.right = 20f;
            box.style.top = 20f + _toasts.Count * 64f;
            box.style.width = 350f;
            box.style.backgroundColor = ModTheme.SurfaceDark;
            box.style.borderLeftWidth = 4f;
            box.style.borderLeftColor = ModTheme.PrimaryAccent;
            box.style.borderTopLeftRadius = 4f;
            box.style.borderTopRightRadius = 4f;
            box.style.borderBottomLeftRadius = 4f;
            box.style.borderBottomRightRadius = 4f;
            box.style.paddingLeft = 12f;
            box.style.paddingRight = 12f;
            box.style.paddingTop = 10f;
            box.style.paddingBottom = 10f;
            var label = new Label(message);
            label.style.color = new Color(0.95f, 0.95f, 0.97f);
            label.style.fontSize = 13;
            var f = ResolveFont();
            if (f != null)
            {
                try { label.style.unityFont = f; } catch { }
            }
            box.Add(label);
            layer.Add(box);
            lock (_toasts) { _toasts.Add(new Toast { Element = box, Until = Time.realtimeSinceStartup + duration }); }
            while (_toasts.Count > 4)
            {
                var old = _toasts[0];
                _toasts.RemoveAt(0);
                try { if (old?.Element != null) old.Element.RemoveFromHierarchy(); } catch { }
            }
        }
        catch { }
    }

    // Input-Guard: true = sperren (Panel offen), false = freigeben.
    public static void SetLocked(bool locked)
    {
        _wantLock = locked;
        RefreshLock();
    }

    public static void Tick()
    {
        // Toasts altern lassen
        try
        {
            if (_toasts.Count > 0)
            {
                float now = Time.realtimeSinceStartup;
                lock (_toasts)
                {
                    for (int i = _toasts.Count - 1; i >= 0; i--)
                    {
                        var t = _toasts[i];
                        if (t == null || now >= t.Until)
                        {
                            try { t?.Element?.RemoveFromHierarchy(); } catch { }
                            _toasts.RemoveAt(i);
                        }
                    }
                }
            }
        }
        catch { }
        RefreshLock();
    }

    private static void RefreshLock()
    {
        try
        {
            if (_wantLock && !_lockApplied)
            {
                _lockApplied = true;
                ForceCursor();
                SetPlayerManager(false);
                SuspendAll();
            }
            else if (_wantLock && _lockApplied)
            {
                ForceCursor();
                SuspendNew();
            }
            else if (!_wantLock && _lockApplied)
            {
                _lockApplied = false;
                ResumeAll();
                SetPlayerManager(true);
                try
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
                catch { }
            }
        }
        catch { }
    }

    private static void ForceCursor()
    {
        try
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
        catch { }
    }

    private static void SetPlayerManager(bool enabled)
    {
        try
        {
            var pm = Il2Cpp.PlayerManager.instance;
            if (pm == null) return;
            try { pm.enabledMouseMovement = enabled; } catch { }
            try { pm.enabledPlayerMovement = enabled; } catch { }
            try { pm.enabledRayLookInteract = enabled; } catch { }
        }
        catch { }
    }

    private static void SuspendAll()
    {
        _suspended.Clear();
        _nextRescanRealtime = 0f;
        SuspendNew();
    }

    private static void SuspendNew()
    {
        float now = 0f;
        try { now = Time.realtimeSinceStartup; } catch { return; }
        if (now < _nextRescanRealtime) return;
        _nextRescanRealtime = now + 2f;
        UnityEngine.InputSystem.PlayerInput[] all = null;
        try { all = Resources.FindObjectsOfTypeAll<UnityEngine.InputSystem.PlayerInput>(); } catch { }
        if (all == null) return;
        foreach (var pi in all)
        {
            if (pi == null || _suspended.Contains(pi)) continue;
            try
            {
                var go = pi.gameObject;
                if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
            }
            catch { continue; }
            try
            {
                try
                {
                    var asset = pi.actions;
                    if (asset != null && asset.enabled) asset.Disable();
                }
                catch { }
                pi.DeactivateInput();
                _suspended.Add(pi);
            }
            catch { }
        }
    }

    private static void ResumeAll()
    {
        foreach (var pi in _suspended)
        {
            if (pi == null) continue;
            try { pi.ActivateInput(); } catch { }
            try
            {
                UnityEngine.InputSystem.InputActionAsset asset = null;
                try { asset = pi.actions; } catch { }
                if (asset != null && !asset.enabled) asset.Enable();
            }
            catch { }
        }
        _suspended.Clear();
    }
}
