using System.Globalization;

namespace Celeste.Mod.Deathlink.Data
{
  internal static class AreaScope
  {
    public static string Create(string sid, int mode)
      => string.IsNullOrEmpty(sid)
        ? null
        : $"{sid}#{mode.ToString(CultureInfo.InvariantCulture)}";

    public static bool AllowsDeath(bool checkpointSyncEnabled, string localScope, string remoteScope)
      => !checkpointSyncEnabled
        || (!string.IsNullOrEmpty(localScope) && localScope == remoteScope);
  }
}
