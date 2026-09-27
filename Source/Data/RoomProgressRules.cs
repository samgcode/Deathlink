using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.Deathlink.Data
{
  internal static class RoomProgressRules
  {
    public static bool CanCommit(
      RoomCheckpoint candidateCheckpoint,
      IReadOnlyCollection<PlayerRoomProgress> activePlayers)
    {
      return candidateCheckpoint.IsValid
        && activePlayers.Count > 0
        && activePlayers.All(progress => progress.CheckpointVisits.Contains(candidateCheckpoint));
    }
  }
}
