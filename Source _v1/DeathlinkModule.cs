using System;
using System.Reflection;
using Celeste.Mod.Deathlink.IO;
using FMOD;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.ModInterop;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace Celeste.Mod.Deathlink;

public class DeathlinkModule : EverestModule
{
    public static readonly string ProtocolVersion = "1_0_5";

    public static string AssemblyVersion
    {
        get
        {
            if (string.IsNullOrEmpty(_version))
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                _version = fvi.FileVersion;
            }
            return _version;
        }
    }
    private static string _version = null;

    public static DeathlinkModule Instance { get; private set; }

    public override Type SettingsType => typeof(DeathlinkModuleSettings);
    public static DeathlinkModuleSettings Settings => (DeathlinkModuleSettings)Instance._Settings;

    public override Type SessionType => typeof(DeathlinkModuleSession);
    public static DeathlinkModuleSession Session => (DeathlinkModuleSession)Instance._Session;

    public override Type SaveDataType => typeof(DeathlinkModuleSaveData);
    public static DeathlinkModuleSaveData SaveData => (DeathlinkModuleSaveData)Instance._SaveData;

    private CNetComm Comm;

    public static Hook hook_Player_orig_Die;

    public delegate void OnSessionInfoChangedHandler();
    public static event OnSessionInfoChangedHandler OnSessionInfoChanged;

    internal DeathlinkModuleSession CachedSession = null;

    public DeathlinkModule()
    {
        Instance = this;
    }

    public override void Load()
    {
        Celeste.Instance.Components.Add(Comm = new CNetComm(Celeste.Instance));

        hook_Player_orig_Die = new Hook(typeof(Player).GetMethod("orig_Die", BindingFlags.Instance | BindingFlags.Public), typeof(DeathlinkModule).GetMethod("OnPlayerDie"));

        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
        Logger.Log(LogLevel.Info, "Deathlink", "Deathlink loaded!");
    }

    public override void Unload()
    {
        Celeste.Instance.Components.Remove(Comm);
        Comm = null;

        hook_Player_orig_Die.Dispose();
        hook_Player_orig_Die = null;
    }

    private void CacheSession()
    {
        CachedSession = Session;
    }

    public static PlayerDeadBody OnPlayerDie(Func<Player, Vector2, bool, bool, PlayerDeadBody> orig, Player self, Vector2 direction, bool ifInvincible, bool registerStats)
    {
        Logger.Log(LogLevel.Info, "Deathlink", "Player died");

        // check whether we'll *actually* die first...
        Session session = self.level.Session;
        bool flag = !ifInvincible && global::Celeste.SaveData.Instance.Assists.Invincible;
        if (!self.Dead && !flag && self.StateMachine.State != Player.StReflectionFall)
        {
            // Cache off session data to restore after golden death
            bool hasGolden = self.Leader?.Followers?.Any((Follower f) => f.Entity is Strawberry strawb && strawb.Golden) ?? false;
            if (hasGolden)
            {
                Instance.CacheSession();
            }
            // Send sync info
            self.Get<SessionSynchronizer>()?.PlayerDied(hasGolden);
        }

        return orig(self, direction, ifInvincible, registerStats);
    }
}
