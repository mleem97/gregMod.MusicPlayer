using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace greg.Mods.MusicPlayer.Core;

// Standalone playback (no gregCore): base features without cache,
// crossfade and preload. Download + WAV/MP3 decode + one AudioSource.
// All types mod-local, Unity or NuGet (JIT-safe without DLL).
public static class ModLocalAudio
{
    private static GameObject _obj;
    private static AudioSource _src;
    private static string _title = "";
    private static string _path = "";
    private static bool _wasPlaying;
    private static AudioClip _clip;

    public static string CurrentTitle => _title;
    public static string CurrentPath => _path;

    public static bool IsPlaying
    {
        get
        {
            try { return _src != null && _src.isPlaying; }
            catch { return false; }
        }
    }

    private static AudioSource Source()
    {
        if (_src != null) return _src;
        try
        {
            _obj = new GameObject("gregMusicLocalSource");
            UnityEngine.Object.DontDestroyOnLoad(_obj);
            _src = _obj.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;
        }
        catch { _src = null; }
        return _src;
    }

    public static void PlayFile(string filePath, string title, float volume)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (string.IsNullOrEmpty(title)) title = Path.GetFileNameWithoutExtension(filePath);
        try
        {
            var src = Source();
            if (src == null) return;
            MelonLogger.Msg("[MusicPlayer] Load (standalone): '" + title + "'");
            MelonCoroutines.Start(LoadAndPlay(filePath, title, volume));
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
            if (_src == null) return;
            _src.Pause();
            _wasPlaying = false;
        }
        catch { }
    }

    public static void Resume(float volume)
    {
        try
        {
            if (_src == null) return;
            _src.volume = volume;
            _src.UnPause();
            _wasPlaying = true;
        }
        catch { }
    }

    public static void Stop()
    {
        try
        {
            if (_src != null)
            {
                try { _src.Stop(); } catch { }
                try { _src.clip = null; } catch { }
            }
            if (_clip != null)
            {
                try { UnityEngine.Object.Destroy(_clip); } catch { }
                _clip = null;
            }
            _wasPlaying = false;
            _title = "";
            _path = "";
        }
        catch { }
    }

    public static void ApplyVolume(float volume)
    {
        try
        {
            if (volume < 0f) volume = 0f;
            if (volume > 1f) volume = 1f;
            if (_src != null) { try { _src.volume = volume; } catch { } }
        }
        catch { }
    }

    // True genau an der Ende-Flanke.
    public static bool PollEnded()
    {
        try
        {
            if (_src == null) { _wasPlaying = false; return false; }
            bool playing = _src.isPlaying;
            bool ended = _wasPlaying && !playing && !string.IsNullOrEmpty(_title);
            _wasPlaying = playing;
            return ended;
        }
        catch { return false; }
    }

    public static string ProgressText()
    {
        try
        {
            if (_src == null || _src.clip == null) return "";
            float pos = _src.time;
            float len = _src.clip.length;
            if (len <= 0f) return "";
            if (pos < 0f) pos = 0f;
            if (pos > len) pos = len;
            return FormatTime(pos) + " / " + FormatTime(len);
        }
        catch { return ""; }
    }

    private static string FormatTime(float s)
    {
        int m = (int)(s / 60f);
        int sec = (int)(s % 60f);
        return m + ":" + (sec < 10 ? "0" : "") + sec;
    }

    public static float ProgressFraction()
    {
        try
        {
            if (_src == null || _src.clip == null) return -1f;
            float len = _src.clip.length;
            if (len <= 0f) return -1f;
            float pos = _src.time;
            if (pos < 0f) pos = 0f;
            if (pos > len) pos = len;
            return pos / len;
        }
        catch { return -1f; }
    }

    public static float RemainingSeconds()
    {
        try
        {
            if (_src == null || _src.clip == null || !_src.isPlaying) return -1f;
            float len = _src.clip.length;
            if (len <= 0f) return -1f;
            float rem = len - _src.time;
            return rem < 0f ? 0f : rem;
        }
        catch { return -1f; }
    }

    public static void Seek(float fraction)
    {
        try
        {
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;
            if (_src == null || _src.clip == null) return;
            float len = _src.clip.length;
            if (len <= 0f) return;
            _src.time = fraction * len;
            _wasPlaying = true;
        }
        catch { }
    }

    private static string ToFileUrl(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        try { return new Uri(path).AbsoluteUri; }
        catch
        {
            string url = path.Replace("\\", "/");
            return "file:///" + Uri.EscapeDataString(url.Replace(":", "|")).Replace("|", ":");
        }
    }

    private static IEnumerator LoadAndPlay(string filePath, string title, float volume)
    {
        string url = ToFileUrl(filePath);
        byte[] data = null;
        string fail = null;
        UnityWebRequest req = null;
        try { req = UnityWebRequest.Get(url); } catch { fail = "Request"; }
        if (req != null)
        {
            req.timeout = 30;
            yield return req.SendWebRequest();
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                    fail = "Download: " + req.error;
                else
                {
                    try { data = req.downloadHandler != null ? req.downloadHandler.data : null; } catch { }
                    if (data == null || data.Length == 0) fail = "Empty";
                }
            }
            catch (Exception ex) { fail = "Download: " + ex.GetBaseException().Message; }
        }
        if (fail != null)
        {
            MelonLogger.Warning("[MusicPlayer] " + fail + " (" + title + ")");
            yield break;
        }

        float[] samples = null;
        int channels = 0;
        int frequency = 0;
        try
        {
            if (!LocalAudioDecoder.TryDecode(filePath, data, out samples, out channels, out frequency)
                || samples == null || samples.Length == 0 || channels <= 0 || frequency <= 0)
            {
                MelonLogger.Warning("[MusicPlayer] Decode failed (" + title + ")");
                yield break;
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] Decode: " + ex.GetBaseException().Message);
            yield break;
        }

        AudioClip clip = null;
        try
        {
            int frames = samples.Length / channels;
            clip = AudioClip.Create(title, frames, channels, frequency, false);
            if (clip == null || !clip.SetData(samples, 0))
            {
                MelonLogger.Warning("[MusicPlayer] Clip creation failed (" + title + ")");
                try { if (clip != null) UnityEngine.Object.Destroy(clip); } catch { }
                yield break;
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] Clip: " + ex.GetBaseException().Message);
            yield break;
        }

        try
        {
            var src = Source();
            if (src == null)
            {
                try { UnityEngine.Object.Destroy(clip); } catch { }
                yield break;
            }
            if (_clip != null)
            {
                try { UnityEngine.Object.Destroy(_clip); } catch { }
            }
            _clip = clip;
            _title = title;
            _path = filePath;
            try { src.Stop(); } catch { }
            src.clip = clip;
            src.volume = volume;
            src.loop = false;
            src.Play();
            _wasPlaying = true;
            MelonLogger.Msg("[MusicPlayer] Playing: '" + title + "' (" + clip.length.ToString("F1") + "s)");
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[MusicPlayer] Play: " + ex.GetBaseException().Message);
        }
    }
}
