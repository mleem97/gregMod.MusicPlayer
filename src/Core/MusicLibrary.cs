using System.Collections.Generic;
using System.IO;

namespace greg.Mods.MusicPlayer.Core;

public sealed class MusicTrack
{
    public string FilePath { get; }
    public string Title { get; }
    public string CoverPath { get; }

    // ID3-Metadaten (TagLibSharp), beim Scan befuellt wo vorhanden.
    public string Id3Title { get; internal set; }
    public string Id3Artist { get; internal set; }
    public string Id3Album { get; internal set; }
    public byte[] Id3CoverBytes { get; internal set; }

    private UnityEngine.Texture2D _coverTexture;

    public MusicTrack(string filePath, string coverPath)
    {
        FilePath = filePath;
        Title = Path.GetFileNameWithoutExtension(filePath);
        CoverPath = coverPath;
    }

    // Anzeigetitel: ID3-Titel bevorzugt, sonst Dateiname.
    public string DisplayTitle => string.IsNullOrWhiteSpace(Id3Title) ? Title : Id3Title;

    // Cover-Textur (ID3-eingebettet bevorzugt, sonst Datei-Cover). Lazy, cached.
    public UnityEngine.Texture2D CoverTexture
    {
        get
        {
            if (_coverTexture != null) return _coverTexture;
            try
            {
                if (Id3CoverBytes != null && Id3CoverBytes.Length > 0)
                {
                    var t = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
                    bool ok = false;
                    try { ok = UnityEngine.ImageConversion.LoadImage(t, Id3CoverBytes); } catch { ok = false; }
                    if (ok) { _coverTexture = t; return t; }
                    try { UnityEngine.Object.Destroy(t); } catch { }
                }
                if (!string.IsNullOrEmpty(CoverPath) && System.IO.File.Exists(CoverPath))
                {
                    byte[] bytes = null;
                    try { bytes = System.IO.File.ReadAllBytes(CoverPath); } catch { }
                    if (bytes != null && bytes.Length > 0)
                    {
                        var t = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
                        bool ok = false;
                        try { ok = UnityEngine.ImageConversion.LoadImage(t, bytes); } catch { ok = false; }
                        if (ok) { _coverTexture = t; return t; }
                        try { UnityEngine.Object.Destroy(t); } catch { }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}

public static class MusicLibrary
{
    private static readonly string[] AudioExts = { ".ogg", ".wav", ".mp3", ".aif", ".aiff" };
    private static readonly string[] CoverNames = { "cover.jpg", "cover.png", "folder.jpg", "folder.png" };
    private static bool _loggedMissingFolder;

    public static List<MusicTrack> Scan(string folder)
    {
        var tracks = new List<MusicTrack>();
        if (string.IsNullOrWhiteSpace(folder)) return tracks;
        try
        {
            if (!Directory.Exists(folder))
            {
                if (!_loggedMissingFolder)
                {
                    _loggedMissingFolder = true;
                    MelonLoader.MelonLogger.Warning("[MusicPlayer] Musik-Ordner nicht vorhanden: " + folder);
                }
                return tracks;
            }
            _loggedMissingFolder = false;
            string[] files = Directory.GetFiles(folder);
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                bool audio = false;
                foreach (string a in AudioExts)
                {
                    if (ext == a) { audio = true; break; }
                }
                if (!audio) continue;
                var track = new MusicTrack(file, FindCover(file, folder));
                ReadId3(track);
                tracks.Add(track);
            }
            tracks.Sort((a, b) => string.Compare(a.Title, b.Title, System.StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Exception ex)
        {
            MelonLoader.MelonLogger.Warning("[MusicPlayer] Scan fehlgeschlagen: " + ex.GetBaseException().Message);
        }
        return tracks;
    }

    // ID3-Tags lesen (Titel/Kuenstler/Album/Cover). Niemals brechen lassen:
    // pro Datei abgesichert, Fehler nur einmalig loggen.
    private static bool _loggedId3;
    private static void ReadId3(MusicTrack track)
    {
        if (track == null) return;
        try
        {
            string ext = Path.GetExtension(track.FilePath).ToLowerInvariant();
            if (ext != ".mp3") return; // TagLib nur wo Tags zu erwarten sind
            using (var file = TagLib.File.Create(track.FilePath))
            {
                if (file == null || file.Tag == null) return;
                var tag = file.Tag;
                if (!string.IsNullOrWhiteSpace(tag.Title))
                    track.Id3Title = TextSanitizer.Clean(tag.Title);
                if (tag.Performers != null && tag.Performers.Length > 0)
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (string p in tag.Performers)
                    {
                        string c = TextSanitizer.Clean(p);
                        if (!string.IsNullOrWhiteSpace(c)) names.Add(c);
                    }
                    if (names.Count > 0)
                        track.Id3Artist = string.Join(", ", names.ToArray());
                }
                if (!string.IsNullOrWhiteSpace(tag.Album))
                    track.Id3Album = TextSanitizer.Clean(tag.Album);
                try
                {
                    var pics = tag.Pictures;
                    if (pics != null && pics.Length > 0 && pics[0] != null && pics[0].Data != null)
                    {
                        byte[] bytes = pics[0].Data.Data;
                        if (bytes != null && bytes.Length > 0)
                            track.Id3CoverBytes = bytes;
                    }
                }
                catch { }
            }
        }
        catch (System.Exception ex)
        {
            if (!_loggedId3)
            {
                _loggedId3 = true;
                MelonLoader.MelonLogger.Warning("[MusicPlayer] ID3-Lesen fehlgeschlagen (einmalig): " + ex.GetBaseException().Message);
            }
        }
    }

    private static string FindCover(string audioFile, string folder)
    {
        try
        {
            foreach (string name in CoverNames)
            {
                string p = Path.Combine(folder, name);
                if (File.Exists(p)) return p;
            }
            string own = Path.ChangeExtension(audioFile, null);
            foreach (string ext in new[] { ".jpg", ".png" })
            {
                if (File.Exists(own + ext)) return own + ext;
            }
        }
        catch { }
        return "";
    }

    public static MusicTrack FindByTitle(List<MusicTrack> tracks, string title)
    {
        if (tracks == null || string.IsNullOrEmpty(title)) return null;
        foreach (MusicTrack t in tracks)
        {
            if (string.Equals(t.Title, title, System.StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
    }
}
