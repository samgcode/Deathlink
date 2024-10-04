

using Celeste.Mod.Deathlink.Data;
using Monocle;

namespace Celeste.Mod.Deathlink.IO
{
  public class Commands
  {
    [Command("dl_kill", "dl_kill [0..100]: Kill team, leave empty to kill own team")]
    public static void KillTeamCommand(string arg)
    {
      if (!CNetComm.Instance.IsConnected) return;

      if (int.TryParse(arg, out int team))
      {
        if (team < 0 || team > 100)
        {
          System.Console.WriteLine("Team number must be in the range [0..100], or none for current team");
          return;
        }
        CNetComm.Instance.Send(new DeathlinkUpdate(team), true);
      }
      else
      {
        CNetComm.Instance.Send(new DeathlinkUpdate(), true);
      }
    }

    [Command("dl_debug_flag", "dl_debug_flag [true/(false)]: Set logger to debug or not")]
    public static void DebugFlagCommand(string arg)
    {
      if (bool.TryParse(arg, out bool flag) && flag)
      {
        Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Debug);
        return;
      }

      Logger.SetLogLevel(nameof(DeathlinkModule), LogLevel.Info);
    }
  }
}
