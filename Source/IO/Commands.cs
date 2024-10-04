

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
  }
}
