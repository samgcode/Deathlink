using System;

namespace Celeste.Mod.Deathlink.Data
{
  internal readonly struct RoomCheckpoint : IEquatable<RoomCheckpoint>
  {
    public string Room { get; }
    public float RespawnX { get; }
    public float RespawnY { get; }

    public bool IsValid
      => !string.IsNullOrEmpty(Room)
        && !float.IsNaN(RespawnX)
        && !float.IsInfinity(RespawnX)
        && !float.IsNaN(RespawnY)
        && !float.IsInfinity(RespawnY);

    public RoomCheckpoint(string room, float respawnX, float respawnY)
    {
      Room = room;
      RespawnX = respawnX;
      RespawnY = respawnY;
    }

    public bool Equals(RoomCheckpoint other)
      => Room == other.Room
        && RespawnX.Equals(other.RespawnX)
        && RespawnY.Equals(other.RespawnY);

    public override bool Equals(object obj)
      => obj is RoomCheckpoint other && Equals(other);

    public override int GetHashCode()
    {
      unchecked
      {
        int hash = Room == null ? 0 : StringComparer.Ordinal.GetHashCode(Room);
        hash = (hash * 397) ^ RespawnX.GetHashCode();
        return (hash * 397) ^ RespawnY.GetHashCode();
      }
    }

    public static bool operator ==(RoomCheckpoint left, RoomCheckpoint right)
      => left.Equals(right);

    public static bool operator !=(RoomCheckpoint left, RoomCheckpoint right)
      => !left.Equals(right);

    public override string ToString()
      => $"{Room} @ ({RespawnX:0.##}, {RespawnY:0.##})";
  }
}
