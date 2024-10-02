using Celeste.Mod.CelesteNet;
using System.Collections.Generic;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public interface ISynchronizable
  {
    EntityID GetID();
    void WriteState(CelesteNetBinaryWriter w);
    void ApplyState(object state);

    bool CheckRecurringUpdate();
  }

  public struct SynchronizableComparer : IEqualityComparer<ISynchronizable>
  {
    public bool Equals(ISynchronizable x, ISynchronizable y)
    {
      return x == null ? y == null : x.GetID().Equals(y.GetID());
    }

    public int GetHashCode(ISynchronizable obj)
    {
      return obj?.GetID().GetHashCode() ?? 0;
    }
  }
}
