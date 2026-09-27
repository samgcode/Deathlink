using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.IO;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Deathlink.Data
{
  public class RoomProgressUpdate : DataType<RoomProgressUpdate>
  {
    public DataPlayerInfo player;
    public string cnetChannel;
    public int team;
    public string map;
    public string room;
    public bool hasRespawnPoint;
    public Vector2 respawnPoint;

    static RoomProgressUpdate()
    {
      DataID = "deathlink_room_progress";
    }

    public RoomProgressUpdate()
    { }

    public RoomProgressUpdate(string room, Vector2 respawnPoint)
    {
      team = DeathlinkModule.Settings.Team;
      cnetChannel = CNetComm.Instance.CurrentChannel?.Name;
      map = DeathlinkModule.map;
      this.room = room;
      hasRespawnPoint = true;
      this.respawnPoint = respawnPoint;
    }

    public override DataFlags DataFlags => DataFlags.CoreType;

    public override MetaType[] GenerateMeta(DataContext ctx)
      => new MetaType[] {
        new MetaPlayerPrivateState(player),
      };

    public override void FixupMeta(DataContext ctx)
    {
      player = Get<MetaPlayerPrivateState>(ctx);
    }

    protected override void Read(CelesteNetBinaryReader reader)
    {
      team = reader.ReadInt32();
      cnetChannel = reader.ReadNetString();
      map = reader.ReadNetString();
      room = reader.ReadNetString();
      hasRespawnPoint = reader.ReadBoolean();
      if (hasRespawnPoint)
      {
        respawnPoint = new Vector2(reader.ReadSingle(), reader.ReadSingle());
      }
    }

    protected override void Write(CelesteNetBinaryWriter writer)
    {
      writer.Write(team);
      writer.WriteNetString(cnetChannel);
      writer.WriteNetString(map);
      writer.WriteNetString(room);
      writer.Write(hasRespawnPoint);
      if (hasRespawnPoint)
      {
        writer.Write(respawnPoint.X);
        writer.Write(respawnPoint.Y);
      }
    }

    public override string ToString()
      => $"Room progress from player: {player?.FullName}, checkpoint: {room} @ {respawnPoint}, team: {team}, cnet channel: {cnetChannel}";
  }
}
