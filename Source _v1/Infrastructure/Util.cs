using Celeste.Mod.Deathlink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public static class Util
  {

    internal static string DLL { get { return CleanDLL(DeathlinkModule.Instance.Metadata); } }
    internal static string CleanDLL(EverestModuleMetadata meta)
    {
      string ret;
      if (string.IsNullOrEmpty(meta.DLL)) ret = meta.DLL;
      else if (string.IsNullOrEmpty(meta.PathDirectory)) ret = meta.DLL;
      else if (meta.PathDirectory.Length + 1 >= meta.DLL.Length) ret = meta.DLL;  // Probably impossible. But probably is not a promise.
      else ret = meta.DLL.Substring(meta.PathDirectory.Length + 1);
      return ret?.Replace('\\', '/');
    }

    internal static MapData GetMapDataForMode(GlobalAreaKey area)
    {
      if (!area.ExistsLocal) return null;
      return area.Data.Mode[(int)area.Mode].MapData;
    }

    internal static LevelSetStats GetSetStats(string levelSet)
    {
      if (string.IsNullOrEmpty(levelSet)) return null;
      return SaveData.Instance.GetLevelSetStatsFor(levelSet);
    }

    internal static ModContent GetModContent(GlobalAreaKey area)
    {
      string path = string.Format("Maps/{0}", area.Data.Mode[(int)area.Mode].Path);
      foreach (ModContent content in Everest.Content.Mods)
      {
        if (content.Map.ContainsKey(path)) return content;
      }
      return null;
    }

    internal static double TimeToSeconds(long ticks)
    {
      TimeSpan timeSpan = TimeSpan.FromTicks(ticks);
      return timeSpan.TotalSeconds;
    }

  }
}
