

using Celeste.Mod.Deathlink.Data;
using Monocle;

namespace Celeste.Mod.Deathlink.IO
{
  public class Commands
  {
    [Command("dl_send", "Send a message to the Deathlink server")]
    public static void TestCnetHooks(string arg)
    {
      bool.TryParse(arg, out bool sendToSelf);
      CNetComm.Instance.Send(new DeathlinkUpdate(), sendToSelf);
    }

    [Command("dl_text_test", "Send a message to the Deathlink server")]
    public static void TestCNetText(string arg)
    {
      string[] args = arg.Split(':');
      float.TryParse(args[1], out float time);

      if (CNetComm.Instance.IsConnected)
      {
        CNetComm.Instance.CnetContext.Status.Set(args[0], time, false, false);
      }
    }
  }
}
