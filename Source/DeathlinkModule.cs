using System;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;
using Celeste.Mod.CoopHelper;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Entities.Helper;
using Celeste.Mod.CoopHelper.Infrastructure;
using Celeste.Mod.CoopHelper.Module;
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

    private SessionPickerHUD hud;
    private SessionPickerAvailabilityInfo availabilityInfo;
    private string roomNameWithOverride;
    private int localID;

    private Player player;

    // private EntityID ID => new(PlayerState.Mine?.CurrentMap.SID + roomNameWithOverride, localID);
    private EntityID ID => new("debug", 0);

    public delegate void MakeSession(
            Session currentSession,
            PlayerID[] players,
            CoopSessionID? id = null,
            int? dashes = null,
            DeathSyncMode deathMode = DeathSyncMode.SameRoomOnly,
            string ability = "",
            string skin = "");

    public delegate void LeaveSession(Session currentSession);

    public delegate void Open(Player player);

    public static FieldInfo test;
    public static BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    public static Type sessionPickerHUDCloseArgs;
    public static Type CoopModule;
    public static Type sessionPickerEntity;


    public DeathlinkModule()
    {
        Instance = this;
    }


    public override void Load()
    {

        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
        Logger.Log(LogLevel.Info, "Deathlink", "Deathlink loaded!");

        EverestModuleMetadata coopHelper = new()
        {
            Name = "CoopHelper",
            Version = new Version(1, 0, 6)
        };

        if (Everest.Loader.TryGetDependency(coopHelper, out EverestModule coopModule))
        {
            Assembly coopAssembly = coopModule.GetType().Assembly;
            sessionPickerHUDCloseArgs = coopAssembly.GetType("Celeste.Mod.CoopHelper.Entities.SessionPickerHUDCloseArgs");
            CoopModule = coopAssembly.GetType("Celeste.Mod.CoopHelper.CoopHelperModule");
            sessionPickerEntity = coopAssembly.GetType("Celeste.Mod.CoopHelper.Entities.SessionPickerEntity");
        }
    }

    public override void Unload()
    {

    }


    public void Start(int players = -1)
    {
        Logger.Log(LogLevel.Info, "Deathlink", "Starting Deathlink");

        Level level = Engine.Scene as Level;

        if (level.Paused)
        {
            level.Pause(1);
        }

        player = level?.Tracker?.GetEntity<Player>();

        Vector2 roomPos = level.Bounds.Location.ToVector2();
        EntityData data = new EntityData();
        data.Position = player.Position - roomPos - Vector2.UnitY * 16f;
        data.ID = 0;

        availabilityInfo = new SessionPickerAvailabilityInfo();

        roomNameWithOverride = data.Level?.Name ?? PlayerState.Mine?.CurrentRoom;
        localID = data.ID;


        Logger.Log(LogLevel.Info, "Deathlink", "Opening hud");
        if (hud != null) return;  // Already open
        hud = new SessionPickerHUD(availabilityInfo, players == -1 ? Settings.Players : players, ID, null, CloseHUD);
        Engine.Scene.Add(hud);
        player.StateMachine.State = Player.StDummy;
        Audio.Play("event:/ui/game/pause");
    }

    public void CloseHUD(SessionPickerHUDCloseArgs args)
    {
        Logger.Log(LogLevel.Info, "Deathlink", "Closing hud");
        if (hud == null) return;  // Already closed
        player.StateMachine.State = Player.StNormal;
        Engine.Scene.Remove(hud);
        hud = null;
        Audio.Play("event:/ui/game/unpause");
        Session currentSession = (Engine.Scene as Level)?.Session;

        bool createNew = (bool)sessionPickerHUDCloseArgs.GetField("CreateNewSession", flags).GetValue(args);
        if (createNew == true && currentSession != null)
        {
            CoopSessionID? id = sessionPickerHUDCloseArgs.GetField("ID", flags).GetValue(args) as CoopSessionID?;
            PlayerID[] players = sessionPickerHUDCloseArgs.GetField("Players", flags).GetValue(args) as PlayerID[];

            Logger.Log(LogLevel.Info, "Deathlink", $"Closing picker with session ID {id}");
            MakeCoopSession(currentSession, players, id);
        }
        else
        {
            Logger.Log(LogLevel.Info, "Deathlink", $"Closing picker with no session");
        }
        availabilityInfo.ResetPending();
    }

    internal void MakeCoopSession(Session currentSession, PlayerID[] players, CoopSessionID? id = null)
    {
        ((MakeSession)CoopModule.GetField("MakeSession", flags).GetValue(CoopHelperModule.Instance)).Invoke(currentSession, players, id, null, DeathSyncMode.Everywhere, null, null);
    }

    [Command("dl_test", "Spawn a Co-op Helper Session Picker")]
    public static void SpawnSessionPicker(string arg)
    {
        Instance.Start(int.Parse(arg));
    }

    // [Command("dl_pause", "Spawn a Co-op Helper Session Picker")]
    // public static void pause(string arg)
    // {
    //     Level level = Engine.Scene as Level;
    //     level.Pause(2, false, false);
    // }

    [Command("dl_ss", "Spawn a Co-op Helper Session Picker")]
    public static void SpawnSessionPicker_temp(string arg)
    {
        Level level = Engine.Scene as Level;
        Player player = level?.Tracker?.GetEntity<Player>();
        if (player != null)
        {
            Vector2 roomPos = level.Bounds.Location.ToVector2();
            EntityData ed = new EntityData();
            ed.Position = player.Position - roomPos - Vector2.UnitY * 16f;
            ed.ID = 0;
            ed.Values = new Dictionary<string, object>();
            ed.Values.Add("removeIfSessionExists", false);
            // ed.Values.Add("idOverride", "debugCMD:0");
            ed.Values.Add("deathSyncMode", "everywhere");
            string[] subArgs = arg?.Split(',');
            if (subArgs != null)
            {
                foreach (string subarg in subArgs)
                {
                    string[] split = subarg?.Split(':');
                    if (split?.Length != 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1])) continue;
                    ed.Values.Add(split[0], split[1]);
                }
            }
            SessionPickerEntity entity = new SessionPickerEntity(ed, roomPos);
            sessionPickerEntity.GetField("PlayersNeeded", flags).SetValue(entity, Settings.Players);

            level.Add(entity);
        }
    }
}
