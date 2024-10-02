using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Data
{
  public class DataPlayerState : DataType<DataPlayerState>
  {
    public DataPlayerInfo player;

    public PlayerID senderID;
    public PlayerState newState;

    static DataPlayerState()
    {
      DataID = "GhostToast_Deathlink_PlayerState_" + DeathlinkModule.ProtocolVersion;
    }

    public DataPlayerState()
    {
      senderID = PlayerID.MyID;
    }

    public override DataFlags DataFlags { get { return DataFlags.None; } }

    public override void FixupMeta(DataContext ctx)
    {
      player = Get<MetaPlayerPrivateState>(ctx);
    }

    public override MetaType[] GenerateMeta(DataContext ctx)
    {
      return new MetaType[] { new MetaPlayerPrivateState(player) };
    }

    protected override void Read(CelesteNetBinaryReader reader)
    {
      senderID = reader.ReadPlayerID();
      newState = reader.ReadPlayerState();
    }

    protected override void Write(CelesteNetBinaryWriter writer)
    {
      writer.Write(senderID);
      writer.Write(newState);
    }
  }
}
