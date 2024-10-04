

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

      int.TryParse(arg, out int team);

      if (team < 0 || team > 100)
      {
        return;
      }
      Logger.Log(LogLevel.Info, "Deathlink", $"Killing team {team}");
      CNetComm.Instance.Send(new DeathlinkUpdate(team), true);

    }

    [Command("dl_deaths", "dl_deaths: Lists the death counts of all players")]
    public static void ListDeathsCommand(string arg)
    {
      DeathlinkModule.ListDeaths();
    }

    [Command("dl_reset_deaths", "dl_reset_deaths: Resets the death counts of all players")]
    public static void ResetDeathsCommand(string arg)
    {
      DeathlinkModule.ResetDeathCounts();
    }
  }
}
