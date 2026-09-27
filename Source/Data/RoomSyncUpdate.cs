using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.IO;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.Deathlink.Data
{
  public class RoomSyncUpdate : DataType<RoomSyncUpdate>
  {
    public DataPlayerInfo player;
    public bool isRequest;
    public uint targetPlayerId;
    public string cnetChannel;
    public int team;
    public string map;
    public string committedRoom;
    public bool hasRespawnPoint;
    public Vector2 respawnPoint;

    static RoomSyncUpdate()
    {
      DataID = "deathlink_room_sync";
    }

    public RoomSyncUpdate()
    { }

    private RoomSyncUpdate(bool isRequest, uint targetPlayerId, string committedRoom, Vector2? respawnPoint)
    {
      this.isRequest = isRequest;
      this.targetPlayerId = targetPlayerId;
      this.committedRoom = committedRoom;
      team = DeathlinkModule.Settings.Team;
      cnetChannel = CNetComm.Instance.CurrentChannel?.Name;
      map = DeathlinkModule.map;
      hasRespawnPoint = respawnPoint.HasValue;
      this.respawnPoint = respawnPoint ?? Vector2.Zero;
    }

    public static RoomSyncUpdate CreateRequest()
      => new RoomSyncUpdate(true, 0, null, null);

    public static RoomSyncUpdate CreateSnapshot(uint targetPlayerId, string committedRoom, Vector2? respawnPoint)
      => new RoomSyncUpdate(false, targetPlayerId, committedRoom, respawnPoint);

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
      isRequest = reader.ReadBoolean();
      targetPlayerId = reader.ReadUInt32();
      team = reader.ReadInt32();
      cnetChannel = reader.ReadNetString();
      map = reader.ReadNetString();
      committedRoom = reader.ReadNetString();
      hasRespawnPoint = reader.ReadBoolean();
      if (hasRespawnPoint)
      {
        respawnPoint = new Vector2(reader.ReadSingle(), reader.ReadSingle());
      }
    }

    protected override void Write(CelesteNetBinaryWriter writer)
    {
      writer.Write(isRequest);
      writer.Write(targetPlayerId);
      writer.Write(team);
      writer.WriteNetString(cnetChannel);
      writer.WriteNetString(map);
      writer.WriteNetString(committedRoom ?? "");
      writer.Write(hasRespawnPoint);
      if (hasRespawnPoint)
      {
        writer.Write(respawnPoint.X);
        writer.Write(respawnPoint.Y);
      }
    }

    public override string ToString()
      => isRequest
        ? $"Room sync request from player: {player?.FullName}, team: {team}, cnet channel: {cnetChannel}"
        : $"Room sync snapshot from player: {player?.FullName}, room: {committedRoom}, target: {targetPlayerId}";
  }
}
