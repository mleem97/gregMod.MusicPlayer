using MelonLoader;

namespace greg.Mods.MusicPlayer.Core;

// Song status sync via Steam lobby data (safe side channel -
// the game-owned network (NetMsg/chunking) is NOT touched).
// Host writes title+status, clients poll and follow. The file name is
// the key: each client plays its local copy from its own folder.
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
        // Standalone: always host (solo behavior).
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
            MelonLogger.Warning("[MusicPlayer] Sync publish failed: " + ex.GetBaseException().Message);
        }
    }

    // Client tick: read lobby, follow on mismatch. Returns: (track, state).
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
