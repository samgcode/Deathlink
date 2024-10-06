using System;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Celeste.Mod.Deathlink.IO;
using Celeste.Mod.Deathlink.Data;
using System.Collections.Generic;

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

    public static string map;
    public static string room;

    public DeathlinkModule()
    {
        Instance = this;
    }


    public override void Load()
    {
        Celeste.Instance.Components.Add(Comm = new CNetComm(Celeste.Instance));

        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
        Logger.Log(LogLevel.Debug, "Deathlink", "Deathlink loaded!");

        CNetComm.OnReceiveDeathlinkUpdate += OnReceiveDeathlinkUpdateHandler;

        hook_Player_orig_Die = new Hook(
                typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
                typeof(DeathlinkModule).GetMethod("OnPlayerDie"));

        On.Celeste.LevelLoader.StartLevel += OnLoadLevel;
        On.Celeste.Player.OnTransition += OnPlayerTransition;

    }

    public override void Unload()
    {
        Celeste.Instance.Components.Remove(Comm);
        Comm = null;

        CNetComm.OnReceiveDeathlinkUpdate -= OnReceiveDeathlinkUpdateHandler;

        hook_Player_orig_Die?.Dispose();
        hook_Player_orig_Die = null;

        On.Celeste.LevelLoader.StartLevel -= OnLoadLevel;
        On.Celeste.Player.OnTransition -= OnPlayerTransition;
    }

    static bool ShouldRecieveDeath(int otherTeam, string otherMap, string otherRoom, LocationModes otherLocationMode)
    {
        bool locationFlag = (Settings.LocationMode == LocationModes.Everywhere) ||
                        (Settings.LocationMode == LocationModes.SameMap && otherMap == map) ||
                        (Settings.LocationMode == LocationModes.SameRoom && otherMap == map && otherRoom == room);

        bool otherLocationFlag = otherLocationMode == LocationModes.Everywhere ||
                            (otherLocationMode == LocationModes.SameMap && otherMap == map) ||
                            (otherLocationMode == LocationModes.SameRoom && otherMap == map && otherRoom == room);

        return Settings.ReceiveDeaths && (otherTeam == 0 || otherTeam == Settings.Team) && locationFlag && otherLocationFlag;
    }

    static bool ShouldSendDeath()
    {
        return Settings.KillOthers && Instance.propagate;
    }

    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        Logger.Log(LogLevel.Info, "Deathlink", $"Player died {ShouldSendDeath()}");
        Instance.should_die = false;
        if (Settings.Enabled && ShouldSendDeath())
        {
            if (CNetComm.Instance.IsConnected)
            {
                Instance.AnnounceDeath(CNetComm.Instance.CnetClient.PlayerInfo.FullName, Settings.Team);
                CNetComm.Instance.Send(new DeathlinkUpdate(), false);
            }
        }
        Instance.propagate = true;
        // Now actually do the thing
        return orig(self, direction, ifInvincible, registerStats);
    }

    public static void OnLoadLevel(On.Celeste.LevelLoader.orig_StartLevel orig, LevelLoader self)
    {
        map = self.Level.Session.Area.SID;
        room = self.Level.Session.Level;
        Logger.Log(LogLevel.Info, "Deathlink", $"Loaded level {map} {room}");
        orig(self);
    }

    public static void OnPlayerTransition(On.Celeste.Player.orig_OnTransition orig, Player self)
    {
        Session session = self.SceneAs<Level>().Session;
        map = session.Area.SID;
        room = session.Level;
        Logger.Log(LogLevel.Info, "Deathlink", $"Loaded level {map} {room}");
        orig(self);
    }

    public void OnReceiveDeathlinkUpdateHandler(DeathlinkUpdate data)
    {
        if (Settings.Enabled)
        {
            Logger.Log(LogLevel.Debug, "Deathlink", $"Received deathlink update: {data}");
            AnnounceDeath(data.player.FullName, data.team);
            if (ShouldRecieveDeath(data.team, data.map, data.room, data.locationMode))
            {
                propagate = false;
                should_die = true;
                Update();
            }
        }
    }

    public void Update()
    {
        // Logger.Log(LogLevel.Info, "Deathlink", $"Update {should_die}");
        if (should_die)
        {
            Level level = Engine.Scene as Level;

            if (level?.Transitioning == false)
            {
                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    player.Die(Vector2.Zero);
                }
                else
                {
                    Logger.Log(LogLevel.Debug, "Deathlink", "Player not found");
                }
            }
        }
    }

    public void AnnounceDeath(string player, int team)
    {
        if (!CNetComm.Instance.IsConnected) return;

        if (team == 0)
        {
            CNetComm.Instance.CnetContext.Status.Set($"{player}: killed everyone", 2.0f, false, false);
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
            CNetComm.Instance.CnetContext.Status.Set($"team {team} was killed by {player}!", 2.0f, false, false);
        }
    }

    public static void ListDeaths()
    {
        if (!CNetComm.Instance.IsConnected) return;
        string output = "deaths:\n";
        foreach (var pair in deathCounts)
        {
            output += $"{pair.Key}: {pair.Value}\n";
        }
        // output += "test1\ntest2\ntest3\ntest4\ntest5\ntest6\ntest7\ntest8\ntest9";
        CNetComm.Instance.CnetContext.Status.Set(output, 5.0f, false, false);
    }

    public static void ResetDeathCounts()
    {
        foreach (KeyValuePair<string, int> entry in deathCounts)
        {
            deathCounts[entry.Key] = 0;
        }
    }


    public enum LocationModes
    {
        Everywhere,
        SameMap,
        SameRoom,
    }
}
