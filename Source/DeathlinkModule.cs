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
using Celeste.Mod.CoopHelper.IO;
using Celeste.Mod.CoopHelper.Data;
using MonoMod.RuntimeDetour;

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
    public bool propagate = true;
    private static int lastDeath = -1;

    public DeathlinkModule()
    {
        Instance = this;
    }


    public override void Load()
    {

        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
        Logger.Log(LogLevel.Info, "Deathlink", "Deathlink loaded!");

        hook_Player_orig_Die = new Hook(
                typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
                typeof(DeathlinkModule).GetMethod("OnPlayerDie"));

        CNetComm.OnReceivePlayerState += OnPlayerStatusUpdate;
    }

    public override void Unload()
    {
        RemoveHooks();
    }

    private void RemoveHooks()
    {
        hook_Player_orig_Die?.Dispose();
        hook_Player_orig_Die = null;

        CNetComm.OnReceivePlayerState -= OnPlayerStatusUpdate;
    }


    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        if (Settings.KillOthers)
        {
            if (Instance.propagate)
            {
                lastDeath++;
                string payload = $"DEATH:{lastDeath}:{PlayerState.Mine.Pid.CNetID}:{PlayerState.Mine.Pid.Name}0";
                Logger.Log(LogLevel.Info, "Deathlink", $"Sending player data: {payload}");

                PlayerState.Mine.ActivePicker = new EntityID(payload, 0);
                PlayerState.Mine.SendUpdateImmediate();

            }
            Instance.propagate = true;
        }

        // Now actually do the thing
        return orig(self, direction, ifInvincible, registerStats);
    }

    public static int GetPlayerID()
    {
        int myPlayerID = int.Parse(PlayerState.Mine.Pid.CNetID.ToString());
        Logger.Log(LogLevel.Info, "Deathlink", $"My player ID: {myPlayerID}");
        return myPlayerID;
    }

    private void OnPlayerStatusUpdate(DataPlayerState data)
    {
        if (Settings.ReceiveDeaths)
        {
            if (data.newState.ActivePicker == null)
            {
                Logger.Log(LogLevel.Info, "Deathlink", $"new state was null");
                return;
            }
            string[] args = data.newState.ActivePicker.Value.ToString().Split(':');
            if (args[0] != "DEATH") return;

            Instance.propagate = false;

            int count = int.Parse(args[1]);
            int pid = int.Parse(args[2]);
            Logger.Log(LogLevel.Info, "Deathlink", $"Received Death({count}) from player: {args[3]}({pid})");

            if (count == lastDeath + 1)
            {
                Logger.Log(LogLevel.Info, "Deathlink", $"applying death");

                lastDeath = count;

                Player player = Engine.Scene.Tracker.GetEntity<Player>();
                if (player != null)
                {
                    player.Die(Vector2.Zero);
                }
                else
                {
                    Logger.Log(LogLevel.Error, "Deathlink", "Player not found");
                }

            }
        }
    }

    [Command("dl_kill", "Kill other players")]
    public static void TestCnetHooks(string arg)
    {
        lastDeath++;
        string payload = $"DEATH:{lastDeath}:{PlayerState.Mine.Pid.CNetID}:{PlayerState.Mine.Pid.Name}0";
        Logger.Log(LogLevel.Info, "Deathlink", $"Sending player data: {payload}");

        PlayerState.Mine.ActivePicker = new EntityID(payload, 0);
        PlayerState.Mine.SendUpdateImmediate();

        Instance.propagate = true;
    }
}
