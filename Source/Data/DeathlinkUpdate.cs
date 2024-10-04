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

    static DeathlinkUpdate()
    {
      DataID = "deathlink_update";
    }

    public DeathlinkUpdate(int team = -1)
    {
      team = team != -1 ? team : DeathlinkModule.Settings.Team;
      cnetChannel = CNetComm.Instance.CurrentChannel?.Name;
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
    }

    protected override void Write(CelesteNetBinaryWriter writer)
    {
      writer.Write(team);
      writer.WriteNetString(cnetChannel);
    }

    public override string ToString()
      => $"Death from player: {player.FullName}, on team: {team}, cnet channel: {cnetChannel}";
  }
}
