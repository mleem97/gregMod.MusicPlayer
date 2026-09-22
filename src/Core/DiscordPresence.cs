using System;
using System.Collections.Generic;
using MelonLoader;
using gregCore.Infrastructure.Social;

namespace greg.Mods.MusicPlayer.Core;

// Discord Rich Presence fuer den MusicPlayer (Englisch).
// Pro Titel: zufaellige Details-Phrase + State-Zeile mit {ARTIST}/{ALBUM}/
// {TITLE}. Texte sanitiert (Emoji-frei). Sprach-Assets bleiben beim
// Framework-Default (Custom-Cover brauchen Discord-seitige Uploads).
public static class DiscordPresence
{
    private static readonly Random Rng = new Random();

    private static readonly string[] DetailsPool = new string[]
    {
        "Playing Data Center",
        "Running the Data Center",
        "Data Center FM",
        "On Shift in the Data Center",
    };

    private static readonly string[] StatePool = new string[]
    {
        "Listening to {ARTIST} while Deploying Servers",
        "Racking {ALBUM} between Racks",
        "Cabling to {TITLE}",
        "Patching Fibers to {ARTIST}",
        "Monitoring Uptime with {ARTIST}",
        "Provisioning Servers to {TITLE}",
        "Rebooting Racks to {ALBUM}",
        "Labeling Cables to {TITLE}",
    };

    private static readonly string[] GenericStates = new string[]
    {
        "Listening to the Radio",
        "On Air in the Data Center",
        "Spinning Tracks between Tickets",
    };

    public static void OnTrackStarted(string filePath, string title, List<MusicTrack> library)
    {
        try
        {
            string artist = "";
            string album = "";
            string display = title ?? "";
            try
            {
                if (library != null && !string.IsNullOrEmpty(filePath))
                {
                    foreach (var t in library)
                    {
                        if (t != null && string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                        {
                            artist = TextSanitizer.Clean(t.Id3Artist ?? "");
                            album = TextSanitizer.Clean(t.Id3Album ?? "");
                            string dt = TextSanitizer.Clean(t.DisplayTitle ?? "");
                            if (!string.IsNullOrEmpty(dt)) display = dt;
                            break;
                        }
                    }
                }
            }
            catch { }

            string details = DetailsPool[Rng.Next(DetailsPool.Length)];
            string state;
            if (string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(album))
            {
                state = GenericStates[Rng.Next(GenericStates.Length)];
            }
            else
            {
                state = StatePool[Rng.Next(StatePool.Length)];
                state = state.Replace("{ARTIST}", string.IsNullOrEmpty(artist) ? display : artist);
                state = state.Replace("{ALBUM}", string.IsNullOrEmpty(album) ? display : album);
                state = state.Replace("{TITLE}", string.IsNullOrEmpty(display) ? "Music" : display);
            }
            DiscordService.UpdatePresence(details, state);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Discord-Presence fehlgeschlagen: " + ex.Message);
        }
    }

    public static void Reset()
    {
        try { DiscordService.UpdatePresence("Playing Data Center", ""); }
        catch { }
    }
}
