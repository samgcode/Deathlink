using System;
using System.Collections.Generic;

namespace Celeste.Mod.Deathlink.Data
{
  internal class PlayerRoomProgress
  {
    public RoomCheckpoint? CurrentCheckpoint;
    public DateTime LastSeenUtc;
    public bool AwaitingCommittedCheckpoint;
    public List<RoomCheckpoint> CheckpointVisits { get; } = new List<RoomCheckpoint>();

    public bool RecordCheckpoint(RoomCheckpoint checkpoint, RoomCheckpoint? committedCheckpoint)
    {
      LastSeenUtc = DateTime.UtcNow;
      if (!checkpoint.IsValid)
      {
        return false;
      }

      if (AwaitingCommittedCheckpoint)
      {
        if (!committedCheckpoint.HasValue || checkpoint != committedCheckpoint.Value)
        {
          return false;
        }

        AwaitingCommittedCheckpoint = false;
      }

      if (!CurrentCheckpoint.HasValue || CurrentCheckpoint.Value != checkpoint)
      {
        CurrentCheckpoint = checkpoint;
        CheckpointVisits.Add(checkpoint);
      }

      return true;
    }

    public void ConsumeThrough(RoomCheckpoint checkpoint)
    {
      int checkpointIndex = CheckpointVisits.LastIndexOf(checkpoint);
      if (checkpointIndex >= 0)
      {
        CheckpointVisits.RemoveRange(0, checkpointIndex + 1);
      }
    }

    public void ResetAttempt(RoomCheckpoint? committedCheckpoint)
    {
      CurrentCheckpoint = null;
      CheckpointVisits.Clear();
      AwaitingCommittedCheckpoint = committedCheckpoint.HasValue;
    }
  }
}
