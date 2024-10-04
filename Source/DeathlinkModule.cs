using System;
using System.Reflection;
using Monocle;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Celeste.Mod.Deathlink.IO;
using Celeste.Mod.Deathlink.Data;

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

        hook_Player_orig_Die = new Hook(
                typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance),
                typeof(DeathlinkModule).GetMethod("OnPlayerDie"));
    }

    public override void Unload()
    {
        Celeste.Instance.Components.Remove(Comm);
        Comm = null;

        CNetComm.OnReceiveDeathlinkUpdate -= OnReceiveDeathlinkUpdateHandler;

        hook_Player_orig_Die?.Dispose();
        hook_Player_orig_Die = null;
    }


    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        if (Settings.KillOthers)
        {
            if (Instance.propagate)
            {
                if (CNetComm.Instance.IsConnected)
                {
                    Instance.announceDeath(CNetComm.Instance.CnetClient.PlayerInfo.FullName, Settings.Team);
                    CNetComm.Instance.Send(new DeathlinkUpdate(), false);
                }
            }
            Instance.propagate = true;
        }
        // Now actually do the thing
        return orig(self, direction, ifInvincible, registerStats);
    }

    public void OnReceiveDeathlinkUpdateHandler(DeathlinkUpdate data)
    {
        Logger.Log(LogLevel.Info, "Deathlink", $"Received deathlink update: {data}");
        if (Settings.ReceiveDeaths)
        {
            announceDeath(data.player.FullName, data.team);
            if (data.team == Settings.Team)
            {
                propagate = false;

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

    public void announceDeath(string player, int team)
    {
        if (CNetComm.Instance.IsConnected)
        {
            CNetComm.Instance.CnetContext.Status.Set($"team {team} was killed by {player}!", 2.0f, false, false);
        }
    }
}
