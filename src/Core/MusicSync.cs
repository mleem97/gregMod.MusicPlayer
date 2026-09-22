using MelonLoader;

namespace greg.Mods.MusicPlayer.Core;

// Song-Status-Sync ueber Steam-Lobby-Daten (sicherer Seitenkanal -
// das Spiel-Owned-Netz (NetMsg/Chunking) wird NICHT angefasst).
// Host schreibt Titel+Status, Clients pollen und folgen. Dateiname ist
// der Schlüssel: Jeder Client spielt seine lokale Kopie aus eigenem Ordner.
public static class MusicSync
{
    public const string KeyTrack = "greg.music.track";
    public const string KeyState = "greg.music.state"; // playing | paused | stopped

    private static float _pollTimer;

    public static ulong LobbyId()
    {
        try
        {
            var session = Il2Cpp.CoopSession.Instance;
            if (session == null || !session.IsActive) return 0;
            return session.SessionId;
        }
        catch { return 0; }
    }

    public static bool InMultiplayer()
    {
        try { return GregHost.HasCore ? HostInMultiplayer() : false; }
        catch { return false; }
    }

    private static bool HostInMultiplayer()
    {
        return gregCore.Infrastructure.Networking.GregNetSession.IsMultiplayerActive;
    }

    public static bool IsHost()
    {
        // Standalone: immer Host (Solo-Verhalten).
        try { return GregHost.HasCore ? HostCanMutate() : true; }
        catch { return true; }
    }

    private static bool HostCanMutate()
    {
        return gregCore.Infrastructure.Networking.GregNetSession.CanMutateWorld;
    }

    public static void Publish(string title, string state)
    {
        try
        {
            ulong lobby = LobbyId();
            if (lobby == 0) return;
            Il2CppSteamworks.SteamMatchmaking.SetLobbyData(new Il2CppSteamworks.CSteamID(lobby), KeyTrack, title ?? "");
            Il2CppSteamworks.SteamMatchmaking.SetLobbyData(new Il2CppSteamworks.CSteamID(lobby), KeyState, state ?? "");
        }
        catch (System.Exception ex)
        {
            MelonLogger.Warning("[MusicPlayer] Sync-Publish fehlgeschlagen: " + ex.GetBaseException().Message);
        }
    }

    // Client-Tick: Lobby lesen, bei Abweichung folgen. Rueckgabe: (track, state).
    public static bool Poll(float dt, out string track, out string state)
    {
        track = "";
        state = "";
        try
        {
            _pollTimer += dt;
            if (_pollTimer < 3f) return false;
            _pollTimer = 0f;
            ulong lobby = LobbyId();
            if (lobby == 0) return false;
            track = Il2CppSteamworks.SteamMatchmaking.GetLobbyData(new Il2CppSteamworks.CSteamID(lobby), KeyTrack) ?? "";
            state = Il2CppSteamworks.SteamMatchmaking.GetLobbyData(new Il2CppSteamworks.CSteamID(lobby), KeyState) ?? "";
            return true;
        }
        catch { return false; }
    }
}
