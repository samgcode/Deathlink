using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.IO;

namespace Celeste.Mod.Deathlink.Data
{
  public class DeathlinkUpdate : DataType<DeathlinkUpdate>
  {
    public DataPlayerInfo player;
    public string cnetChannel;
    public int team;
    public DeathlinkModule.LocationModes locationMode;
    public string map;
    public string room;

    static DeathlinkUpdate()
    {
      DataID = "deathlink_update";
    }

    public DeathlinkUpdate() : this(-1)
    { }

    public DeathlinkUpdate(int team)
    {
      this.team = team != -1 ? team : DeathlinkModule.Settings.Team;
      cnetChannel = CNetComm.Instance.CurrentChannel?.Name;
      map = DeathlinkModule.map;
      room = DeathlinkModule.room;
      locationMode = DeathlinkModule.Settings.Location.LocationMode;
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
      locationMode = (DeathlinkModule.LocationModes)reader.ReadInt32();
    }

    protected override void Write(CelesteNetBinaryWriter writer)
    {
      writer.Write(team);
      writer.WriteNetString(cnetChannel);
      writer.WriteNetString(map);
      writer.WriteNetString(room);
      writer.Write((int)locationMode);
    }

    public override string ToString()
      => $"Death from player: {player.FullName}, on team: {team}, cnet channel: {cnetChannel}";
  }
}
