using System;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Celeste.Mod.Deathlink.IO;
using Celeste.Mod.Deathlink.Data;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.Deathlink.Message;

namespace Celeste.Mod.Deathlink;

public class DeathlinkModule : EverestModule
{
    public static DeathlinkModule Instance;

    public override Type SettingsType => typeof(DeathlinkModuleSettings);
    public static DeathlinkModuleSettings Settings => (DeathlinkModuleSettings)Instance._Settings;
    public override Type SaveDataType => typeof(DeathlinkModuleSaveData);
    public static DeathlinkModuleSaveData SaveData => (DeathlinkModuleSaveData)Instance._SaveData;
    public override Type SessionType => typeof(DeathlinkModuleSession);
    public static DeathlinkModuleSession Session => (DeathlinkModuleSession)Instance._Session;

    private static Hook hook_Player_orig_Die;
    private CNetComm Comm;

    private bool propagate = true;
    private bool should_die = false;
    private static Dictionary<string, int> deathCounts = new Dictionary<string, int>();
    private ulong counter = 0;
    private const ulong ProgressHeartbeatFrames = 180;
    private const ulong RoomSyncWaitFrames = 30;
    private static readonly TimeSpan ProgressTimeout = TimeSpan.FromSeconds(15);

    private readonly Dictionary<string, PlayerRoomProgress> playerRoomProgress = new Dictionary<string, PlayerRoomProgress>();
    private string syncMap;
    private string syncChannel;
    private int syncTeam;
    private global::Celeste.Session currentGameSession;
    private RoomCheckpoint? committedCheckpoint;
    private ulong lastProgressBroadcastFrame;
    private bool awaitingInitialRoomSync;
    private ulong roomSyncStartedFrame;
    private ulong lastRoomSyncRequestFrame;
    private RoomSyncUpdate pendingRoomSyncSnapshot;
    private uint pendingRoomSyncSenderId = uint.MaxValue;

    public static string map;
    public static string room;

    public StatusComponent Status;

    public DeathlinkModule()
    {
        Instance = this;
    }

    public override void Load()
    {
        Celeste.Instance.Components.Add(Comm = new CNetComm(Celeste.Instance));

        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
        Logger.Log(LogLevel.Info, "Deathlink", "Deathlink loaded!");

        CNetComm.OnReceiveDeathlinkUpdate += OnReceiveDeathlinkUpdateHandler;
        CNetComm.OnReceiveRoomProgressUpdate += OnReceiveRoomProgressUpdateHandler;
        CNetComm.OnReceiveRoomSyncUpdate += OnReceiveRoomSyncUpdateHandler;

        hook_Player_orig_Die = new Hook(
                typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
                typeof(DeathlinkModule).GetMethod("OnPlayerDie"));

        On.Celeste.LevelLoader.StartLevel += OnLoadLevel;
        On.Celeste.Player.OnTransition += OnPlayerTransition;
    }

    public override void Initialize()
    {
        base.Initialize();
        Celeste.Instance.Components.Add(Status = new StatusComponent(Celeste.Instance));
    }

    public override void Unload()
    {
        Celeste.Instance.Components.Remove(Comm);
        Comm = null;
        Celeste.Instance.Components.Remove(Status);
        Status = null;

        CNetComm.OnReceiveDeathlinkUpdate -= OnReceiveDeathlinkUpdateHandler;
        CNetComm.OnReceiveRoomProgressUpdate -= OnReceiveRoomProgressUpdateHandler;
        CNetComm.OnReceiveRoomSyncUpdate -= OnReceiveRoomSyncUpdateHandler;

        hook_Player_orig_Die?.Dispose();
        hook_Player_orig_Die = null;

        On.Celeste.LevelLoader.StartLevel -= OnLoadLevel;
        On.Celeste.Player.OnTransition -= OnPlayerTransition;

        ResetRoomSyncState();
        currentGameSession = null;
        map = null;
        room = null;
    }

    static bool ShouldRecieveDeath(int otherTeam, string otherMap, string otherRoom, LocationModes otherLocationMode)
    {
        bool locationFlag = (Settings.Location.LocationMode == LocationModes.Everywhere) ||
                        (Settings.Location.LocationMode == LocationModes.SameMap && otherMap == map) ||
                        (Settings.Location.LocationMode == LocationModes.SameRoom && otherMap == map && otherRoom == room);

        return Settings.Location.ReceiveDeaths
            && (otherTeam == 0 || otherTeam == Settings.Team)
            && locationFlag
            && AreaScope.AllowsDeath(Settings.Location.SyncRoomProgress, map, otherMap);
    }

    static bool ShouldSendDeath()
    {
        return Settings.Location.KillOthers && Instance.propagate;
    }

    static bool ShouldSyncRoomProgress()
    {
        return Settings.Enabled
            && Settings.Location.SyncRoomProgress
            && CNetComm.Instance?.IsConnected == true;
    }

    static bool ShouldAnnounceDeath(string player, int team)
    {
        return (team == 0) ||
                (Settings.Status.AnnounceMode == AnnounceModes.All) ||
                (Settings.Status.AnnounceMode == AnnounceModes.Team && team == Settings.Team) ||
                (Settings.Status.AnnounceMode == AnnounceModes.Self && player == CNetComm.Instance.CnetClient.PlayerInfo.FullName);
    }

    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        Instance.should_die = false;
        Instance.ResetRoomProgressAttempt();
        if (Settings.Enabled && ShouldSendDeath())
        {
            if (registerStats && self.StateMachine.State != Player.StReflectionFall && self.StateMachine.State != Player.StDummy)
            {
                if (CNetComm.Instance.IsConnected)
                {
                    Instance.AnnounceDeath(CNetComm.Instance.CnetClient.PlayerInfo.FullName, Settings.Team);
                    CNetComm.Instance.Send(new DeathlinkUpdate(), false);
                }
            }
        }
        Instance.ApplyCommittedRoomRespawn(self.SceneAs<Level>());
        Instance.propagate = true;

        // Now actually do the thing
        return orig(self, direction, ifInvincible, registerStats);
    }

    public static void OnLoadLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self)
    {
        SetCurrentLocation(self.Level.Session);
        orig(self);
    }

    public static void OnPlayerTransition(On.Celeste.Player.orig_OnTransition orig, Player self)
    {
        Session session = self.SceneAs<Level>().Session;
        SetCurrentLocation(session);
        orig(self);
    }

    private static string GetAreaScope(Session session)
        => session == null
            ? null
            : AreaScope.Create(session.Area.SID, (int)session.Area.Mode);

    private static void SetCurrentLocation(Session session)
    {
        string currentMap = GetAreaScope(session);
        if (!ReferenceEquals(Instance.currentGameSession, session) || map != currentMap)
        {
            Instance.ResetRoomSyncState();
            Instance.should_die = false;
            Instance.propagate = true;
            Instance.currentGameSession = session;
        }

        map = currentMap;
        room = session?.Level;
    }

    public void OnReceiveDeathlinkUpdateHandler(DeathlinkUpdate data)
    {
        if (Settings.Enabled)
        {
            AnnounceDeath(data.player.FullName, data.team);
            if (ShouldRecieveDeath(data.team, data.map, data.room, data.locationMode))
            {
                ResetRoomProgressAttempt();
                propagate = false;
                should_die = true;
            }
        }
    }

    public void OnReceiveRoomProgressUpdateHandler(RoomProgressUpdate data)
    {
        if (!ShouldSyncRoomProgress() || should_die || data.player == null)
        {
            return;
        }

        if (data.team != Settings.Team || data.map != map || data.cnetChannel != CNetComm.Instance.CurrentChannel?.Name)
        {
            return;
        }

        EnsureRoomSyncScope(data.map, data.cnetChannel, data.team);
        if (data.hasRespawnPoint)
        {
            RecordPlayerCheckpoint(data.player.FullName, ToCheckpoint(data.room, data.respawnPoint));
        }
    }

    public void OnReceiveRoomSyncUpdateHandler(RoomSyncUpdate data)
    {
        if (!ShouldSyncRoomProgress() || data.player == null)
        {
            return;
        }

        if (data.team != Settings.Team || data.map != map || data.cnetChannel != CNetComm.Instance.CurrentChannel?.Name)
        {
            return;
        }

        EnsureRoomSyncScope(data.map, data.cnetChannel, data.team);
        if (data.isRequest)
        {
            HandleRoomSyncRequest(data);
        }
        else
        {
            HandleRoomSyncSnapshot(data);
        }
    }

    public void Update(GameTime gameTime)
    {
        UpdateInput();
        UpdateRoomProgress();

        counter++;
        if (counter % 10 != 0) return;

        if (should_die)
        {
            Level level = Engine.Scene as Level;

            if (level?.Transitioning == false)
            {
                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    if (player.StateMachine.State != Player.StDummy)
                    {
                        ApplyCommittedRoomRespawn(level);
                        player.Die(Vector2.Zero);
                    }
                    else
                    {
                        Logger.Log(LogLevel.Debug, "Deathlink", "Player not found");
                    }
                }
            }
        }
    }

    private void UpdateRoomProgress()
    {
        Level level = Engine.Scene as Level;
        if (level == null)
        {
            return;
        }

        SetCurrentLocation(level.Session);

        if (!ShouldSyncRoomProgress())
        {
            ResetRoomSyncState();
            return;
        }

        string channel = CNetComm.Instance.CurrentChannel?.Name;
        string playerName = CNetComm.Instance.CnetClient?.PlayerInfo?.FullName;
        if (string.IsNullOrEmpty(channel) || string.IsNullOrEmpty(playerName))
        {
            ResetRoomSyncState();
            return;
        }

        EnsureRoomSyncScope(map, channel, Settings.Team);
        bool waitingForInitialSync = WaitForInitialRoomSync();

        if (should_die || level.Transitioning)
        {
            return;
        }

        RoomCheckpoint? currentCheckpoint = ResolveCurrentCheckpoint(level);
        if (waitingForInitialSync || !currentCheckpoint.HasValue)
        {
            return;
        }

        PruneStaleRoomProgress(playerName);

        RoomCheckpoint checkpoint = currentCheckpoint.Value;
        bool shouldBroadcast = !playerRoomProgress.TryGetValue(playerName, out PlayerRoomProgress localProgress)
            || !localProgress.CurrentCheckpoint.HasValue
            || localProgress.CurrentCheckpoint.Value != checkpoint
            || counter - lastProgressBroadcastFrame >= ProgressHeartbeatFrames;

        bool recorded = RecordPlayerCheckpoint(playerName, checkpoint);

        if (recorded && shouldBroadcast)
        {
            lastProgressBroadcastFrame = counter;
            CNetComm.Instance.Send(new RoomProgressUpdate(checkpoint.Room, ToVector2(checkpoint)), false);
        }
        else if (!recorded
            && playerRoomProgress.TryGetValue(playerName, out localProgress)
            && localProgress.AwaitingCommittedCheckpoint
            && counter - lastRoomSyncRequestFrame >= ProgressHeartbeatFrames)
        {
            SendRoomSyncRequest();
        }
    }

    private void EnsureRoomSyncScope(string currentMap, string currentChannel, int currentTeam)
    {
        if (syncMap == currentMap && syncChannel == currentChannel && syncTeam == currentTeam)
        {
            return;
        }

        ResetRoomSyncState();
        syncMap = currentMap;
        syncChannel = currentChannel;
        syncTeam = currentTeam;
    }

    private void ResetRoomSyncState()
    {
        playerRoomProgress.Clear();
        syncMap = null;
        syncChannel = null;
        syncTeam = 0;
        committedCheckpoint = null;
        lastProgressBroadcastFrame = 0;
        awaitingInitialRoomSync = false;
        roomSyncStartedFrame = 0;
        lastRoomSyncRequestFrame = 0;
        pendingRoomSyncSnapshot = null;
        pendingRoomSyncSenderId = uint.MaxValue;
    }

    private bool WaitForInitialRoomSync()
    {
        if (committedCheckpoint.HasValue)
        {
            return false;
        }

        if (!awaitingInitialRoomSync)
        {
            awaitingInitialRoomSync = true;
            roomSyncStartedFrame = counter;
            pendingRoomSyncSnapshot = null;
            pendingRoomSyncSenderId = uint.MaxValue;
            SendRoomSyncRequest();
            return true;
        }

        if (counter - roomSyncStartedFrame < RoomSyncWaitFrames)
        {
            return true;
        }

        awaitingInitialRoomSync = false;
        if (pendingRoomSyncSnapshot != null)
        {
            AdoptRoomSyncSnapshot(pendingRoomSyncSnapshot);
        }

        return false;
    }

    private void SendRoomSyncRequest()
    {
        lastRoomSyncRequestFrame = counter;
        CNetComm.Instance.Send(RoomSyncUpdate.CreateRequest(), false);
    }

    private void HandleRoomSyncRequest(RoomSyncUpdate data)
    {
        uint? localPlayerId = CNetComm.Instance.CnetID;
        if (!committedCheckpoint.HasValue || !localPlayerId.HasValue || data.player.ID == localPlayerId.Value)
        {
            return;
        }

        RegisterJoiningPlayer(data.player.FullName);
        RoomCheckpoint checkpoint = committedCheckpoint.Value;
        CNetComm.Instance.Send(RoomSyncUpdate.CreateSnapshot(data.player.ID, checkpoint.Room, ToVector2(checkpoint)), false);
    }

    private void HandleRoomSyncSnapshot(RoomSyncUpdate data)
    {
        uint? localPlayerId = CNetComm.Instance.CnetID;
        if (!awaitingInitialRoomSync
            || committedCheckpoint.HasValue
            || !localPlayerId.HasValue
            || data.targetPlayerId != localPlayerId.Value
            || string.IsNullOrEmpty(data.committedRoom)
            || !data.hasRespawnPoint)
        {
            return;
        }

        if (pendingRoomSyncSnapshot == null || data.player.ID < pendingRoomSyncSenderId)
        {
            pendingRoomSyncSnapshot = data;
            pendingRoomSyncSenderId = data.player.ID;
        }
    }

    private void RegisterJoiningPlayer(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            return;
        }

        bool isNewPlayer = !playerRoomProgress.TryGetValue(playerName, out PlayerRoomProgress progress);
        if (isNewPlayer)
        {
            progress = new PlayerRoomProgress();
            playerRoomProgress[playerName] = progress;
        }

        if (isNewPlayer || (!progress.AwaitingCommittedCheckpoint && progress.CurrentCheckpoint.HasValue))
        {
            progress.ResetAttempt(committedCheckpoint);
        }

        progress.LastSeenUtc = DateTime.UtcNow;
    }

    private void AdoptRoomSyncSnapshot(RoomSyncUpdate snapshot)
    {
        RoomCheckpoint checkpoint = ToCheckpoint(snapshot.committedRoom, snapshot.respawnPoint);
        if (!checkpoint.IsValid)
        {
            return;
        }

        SetCommittedCheckpoint(checkpoint, false);

        foreach (PlayerRoomProgress progress in playerRoomProgress.Values)
        {
            progress.ResetAttempt(committedCheckpoint);
        }

        pendingRoomSyncSnapshot = null;
        pendingRoomSyncSenderId = uint.MaxValue;
        lastRoomSyncRequestFrame = counter;
        Status?.Push(new Message.Message(MessageType.Message, $"Team checkpoint synced: {checkpoint}", 2.0f));
    }

    private void ResetRoomProgressAttempt()
    {
        if (!ShouldSyncRoomProgress() || !committedCheckpoint.HasValue)
        {
            return;
        }

        foreach (PlayerRoomProgress progress in playerRoomProgress.Values)
        {
            progress.ResetAttempt(committedCheckpoint);
        }

        lastProgressBroadcastFrame = 0;
    }

    private void PruneStaleRoomProgress(string localPlayerName)
    {
        DateTime now = DateTime.UtcNow;
        List<string> stalePlayers = playerRoomProgress
            .Where(pair => pair.Key != localPlayerName && now - pair.Value.LastSeenUtc > ProgressTimeout)
            .Select(pair => pair.Key)
            .ToList();

        if (stalePlayers.Count == 0)
        {
            return;
        }

        foreach (string stalePlayer in stalePlayers)
        {
            playerRoomProgress.Remove(stalePlayer);
        }

        if (playerRoomProgress.TryGetValue(localPlayerName, out PlayerRoomProgress localProgress)
            && localProgress.CurrentCheckpoint.HasValue)
        {
            RecalculateCommittedCheckpoint(localProgress.CurrentCheckpoint.Value, true);
        }
    }

    private bool RecordPlayerCheckpoint(string playerName, RoomCheckpoint checkpoint)
    {
        if (string.IsNullOrEmpty(playerName) || !checkpoint.IsValid)
        {
            return false;
        }

        if (!playerRoomProgress.TryGetValue(playerName, out PlayerRoomProgress progress))
        {
            progress = new PlayerRoomProgress();
            playerRoomProgress[playerName] = progress;
            if (committedCheckpoint.HasValue)
            {
                progress.ResetAttempt(committedCheckpoint);
            }
        }

        if (!progress.RecordCheckpoint(checkpoint, committedCheckpoint))
        {
            return false;
        }

        RecalculateCommittedCheckpoint(checkpoint, false);
        return true;
    }

    private void RecalculateCommittedCheckpoint(RoomCheckpoint candidateCheckpoint, bool allowAnnounce)
    {
        List<PlayerRoomProgress> activePlayers = playerRoomProgress.Values
            .Where(progress => DateTime.UtcNow - progress.LastSeenUtc <= ProgressTimeout)
            .ToList();
        if (activePlayers.Count == 0)
        {
            return;
        }

        if (!committedCheckpoint.HasValue)
        {
            string localPlayerName = CNetComm.Instance.CnetClient?.PlayerInfo?.FullName;
            if (!string.IsNullOrEmpty(localPlayerName)
                && playerRoomProgress.TryGetValue(localPlayerName, out PlayerRoomProgress localProgress)
                && localProgress.CurrentCheckpoint.HasValue)
            {
                SetCommittedCheckpoint(localProgress.CurrentCheckpoint.Value, false);
            }
        }

        if (!committedCheckpoint.HasValue || !RoomProgressRules.CanCommit(candidateCheckpoint, activePlayers))
        {
            return;
        }

        SetCommittedCheckpoint(candidateCheckpoint, allowAnnounce);
    }

    private void SetCommittedCheckpoint(RoomCheckpoint checkpoint, bool announce)
    {
        if (!checkpoint.IsValid)
        {
            return;
        }

        committedCheckpoint = checkpoint;
        foreach (PlayerRoomProgress progress in playerRoomProgress.Values)
        {
            progress.ConsumeThrough(checkpoint);
        }

        if (announce)
        {
            Status?.Push(new Message.Message(MessageType.Message, $"Team checkpoint synced: {checkpoint}", 2.0f));
        }
    }

    private RoomCheckpoint? ResolveCurrentCheckpoint(Level level)
    {
        if (level?.Session == null || string.IsNullOrEmpty(level.Session.Level))
        {
            return null;
        }

        Vector2? respawnPoint = ResolveSummitCheckpointContact(level);
        if (respawnPoint.HasValue)
        {
            if (!level.Session.RespawnPoint.HasValue || level.Session.RespawnPoint.Value != respawnPoint.Value)
            {
                // Activated Summit flags normally stop changing the respawn point when revisited.
                level.Session.RespawnPoint = respawnPoint.Value;
                level.Session.HitCheckpoint = true;
                level.Session.UpdateLevelStartDashes();
            }
        }
        else
        {
            respawnPoint = level.Session.RespawnPoint;
        }

        if (!respawnPoint.HasValue && level.Session.LevelData?.Spawns != null && level.Session.LevelData.Spawns.Any())
        {
            respawnPoint = level.Session.LevelData.Spawns[0];
        }

        return respawnPoint.HasValue
            ? ToCheckpoint(level.Session.Level, respawnPoint.Value)
            : (RoomCheckpoint?)null;
    }

    private static Vector2? ResolveSummitCheckpointContact(Level level)
    {
        foreach (SummitCheckpoint checkpoint in level.Tracker.GetEntitiesTrackIfNeeded<SummitCheckpoint>())
        {
            Player player = checkpoint.CollideFirst<Player>();
            if (player != null && player.OnGround(1) && player.Speed.Y >= 0f)
            {
                return level.GetSpawnPoint(checkpoint.Position);
            }
        }

        return null;
    }

    private static RoomCheckpoint ToCheckpoint(string roomName, Vector2 respawnPoint)
        => new RoomCheckpoint(roomName, respawnPoint.X, respawnPoint.Y);

    private static Vector2 ToVector2(RoomCheckpoint checkpoint)
        => new Vector2(checkpoint.RespawnX, checkpoint.RespawnY);

    private void ApplyCommittedRoomRespawn(Level level)
    {
        if (!ShouldSyncRoomProgress() || level?.Session == null || !committedCheckpoint.HasValue)
        {
            return;
        }

        if (!ReferenceEquals(level.Session, currentGameSession) || GetAreaScope(level.Session) != syncMap)
        {
            return;
        }

        RoomCheckpoint checkpoint = committedCheckpoint.Value;
        level.Session.Level = checkpoint.Room;
        level.Session.RespawnPoint = ToVector2(checkpoint);
    }

    private void UpdateInput()
    {
        if (Settings.ToggleBind.Pressed)
        {
            Settings.Enabled = !Settings.Enabled;
            Status.Push(new Message.Message(MessageType.Message, $"Deathlink  {(Settings.Enabled ? "enabled" : "disabled")}", 2.0f));
        }

        if (Settings.ListPlayersBind.Pressed)
        {
            ListDeaths();
        }

        if (Settings.ResetDeathsBind.Pressed)
        {
            ResetDeathCounts();
        }

        if (Settings.ToggleCnetBind.Pressed)
        {
            CelesteNetClientModule.Settings.Connected = !CelesteNetClientModule.Settings.Connected;
        }
    }

    public void AnnounceDeath(string player, int team)
    {
        if (!ShouldAnnounceDeath(player, team)) return;

        if (team == 0)
        {
            Status.Push(new Message.Message(MessageType.Death, $"{player} killed everyone", 2.0f));
        }
        else
        {
            if (deathCounts.TryGetValue(player, out int count))
            {
                deathCounts[player] = count + 1;
            }
            else
            {
                deathCounts.Add(player, 1);
            }

            string output = "";
            if (Settings.Status.DisplayFormat == SubAnnounceModes.PlayerOnly)
            {
                output = $"{player} died!";
            }
            else if (Settings.Status.DisplayFormat == SubAnnounceModes.TeamOnly)
            {
                output = $"team {team} was killed!";
            }
            else if (Settings.Status.DisplayFormat == SubAnnounceModes.Both)
            {
                output = $"team {team} was killed by {player}!";
            }
            Status.Push(new Message.Message(MessageType.Death, output, 2.0f));
        }
    }

    public static void ListDeaths()
    {
        float time = 5.0f;
        Instance.Status.Push(new Message.Message(MessageType.None, $"Deaths:", time));
        foreach (var pair in deathCounts)
        {
            time += 0.1f;
            Instance.Status.Push(new Message.Message(MessageType.None, $"{pair.Key}: {pair.Value}", time));
        }
    }

    public static void ResetDeathCounts()
    {
        deathCounts.Clear();
        Instance.Status.Push(new Message.Message(MessageType.Message, $"Cleared death counts", 2.0f));
    }


    public enum LocationModes
    {
        Everywhere,
        SameMap,
        SameRoom,
    }

    public enum AnnounceModes
    {
        None,
        Self,
        Team,
        All,
    }

    public enum SubAnnounceModes
    {
        None,
        PlayerOnly,
        TeamOnly,
        Both,
    }
}
