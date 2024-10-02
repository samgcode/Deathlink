using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.IO;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public class PlayerState
  {

    public static PlayerState Mine { get; private set; }

    static PlayerState()
    {
      Mine = new PlayerState()
      {
        Pid = PlayerID.MyID,
        CurrentMap = GlobalAreaKey.Overworld,
        CurrentRoom = "",
        RespawnPoint = Vector2.Zero,
        LastUpdateReceived = DateTime.Now,
        LastUpdateSent = DateTime.Now,
      };
    }

    internal static void OnConnected()
    {
      Mine.Pid = PlayerID.MyID;
      Mine.SendUpdateImmediate();
    }

    #region Remote State Information

    private static readonly float heartbeatTime = 30;  // Send fresh updates every 30 seconds even if nothing changed
    private static readonly float purgeTime = 600;    // Data is stale enough to purge after 600 seconds (10 minutes)

    private static Dictionary<PlayerID, PlayerState> _playerStates = new Dictionary<PlayerID, PlayerState>();
    private static BidirectionalDictionary<PlayerID, uint> _idDictionary = new BidirectionalDictionary<PlayerID, uint>();

    /// <summary>
    /// Gets the PlayerState for the given player, or null if it is not known
    /// </summary>
    /// <param name="id">ID of the player</param>
    /// <returns>Player's state if known, otherwise null</returns>
    public static PlayerState Get(PlayerID id)
    {
      return _playerStates.ContainsKey(id) ? _playerStates[id] : null;
    }

    internal static IEnumerable<PlayerState> All
    {
      get
      {
        foreach (PlayerState ps in _playerStates.Values)
        {
          yield return ps;
        }
      }
    }

    internal static void OnPlayerStateReceived(Data.DataPlayerState data)
    {
      PlayerID id = data.senderID;
      PlayerState state = data.newState;
      uint cnetID = data.player.ID;
      // update id map
      if (!_idDictionary.Contains(id, cnetID))
      {
        _idDictionary.Add(id, cnetID);
      }
      // update state map
      if (_playerStates.ContainsKey(id))
      {
        _playerStates[id].ApplyUpdate(state);
      }
      else
      {
        _playerStates.Add(id, state);
      }
    }

    internal static void OnConnectionDataReceived(DataConnectionInfo data)
    {
      if (_idDictionary.Contains(data.Player.ID))
      {
        PlayerID id = _idDictionary.Reverse[data.Player.ID];
        if (_playerStates.ContainsKey(id))
        {
          _playerStates[id].SetPing(data.TCPPingMs, data.UDPPingMs);
        }
      }
    }

    internal static void PurgeStale()
    {
      List<PlayerID> toRemove = new List<PlayerID>();
      foreach (KeyValuePair<PlayerID, PlayerState> v in _playerStates)
      {
        if ((DateTime.Now - v.Value.LastUpdateReceived).TotalSeconds > purgeTime)
        {
          toRemove.Add(v.Key);
        }
      }
      foreach (PlayerID id in toRemove)
      {
        _playerStates.Remove(id);
        _idDictionary.Remove(id);
      }
    }

    #endregion

    /// <summary>
    /// Instant of the last outbound update. Not synced.
    /// </summary>
    public DateTime LastUpdateSent { get; private set; }
    /// <summary>
    /// Instant of the last incoming update. Not synced
    /// </summary>
    public DateTime LastUpdateReceived { get; private set; }

    public Vector2 RespawnPoint { get; private set; }
    public GlobalAreaKey CurrentMap { get; private set; }
    public string CurrentRoom { get; private set; }
    public EntityID? ActivePicker { get; set; }
    public PlayerID Pid { get; private set; }
    /// <summary>
    /// Last Measured UDP ping time (reliable/slow packets) in ms (or 300 if unknown)
    /// </summary>
    public int Ping_UDP { get; private set; } = 300;
    /// <summary>
    /// Last Measured TCP ping time (unreliable/fast packets) in ms (or 100 if unknown)
    /// </summary>
    public int Ping_TCP { get; private set; } = 100;

    public static PlayerState Default
    {
      get
      {
        return new PlayerState();
      }
    }

    private PlayerState()
    {
      Pid = default(PlayerID);
      CurrentMap = GlobalAreaKey.Overworld;
      CurrentRoom = "";
      RespawnPoint = Vector2.Zero;
      LastUpdateReceived = DateTime.Now;
      LastUpdateSent = DateTime.Now;
    }

    public PlayerState(Player p)
    {
      Session s = p.SceneAs<Level>().Session;
      Pid = PlayerID.MyID;
      CurrentMap = new GlobalAreaKey(s.Area);
      CurrentRoom = s.Level;
      RespawnPoint = s.RespawnPoint ?? Vector2.Zero;
      LastUpdateReceived = DateTime.Now;
      LastUpdateSent = DateTime.Now;
    }

    internal PlayerState(CelesteNetBinaryReader r)
    {
      Pid = r.ReadPlayerID();
      CurrentMap = r.ReadAreaKey();
      CurrentRoom = r.ReadString();
      RespawnPoint = r.ReadVector2();
      bool hasActivePicker = r.ReadBoolean();
      ActivePicker = hasActivePicker ? ReadEntityID(r) : null;
      LastUpdateReceived = DateTime.Now;
      LastUpdateSent = DateTime.Now;
    }

    public static EntityID ReadEntityID(CelesteNetBinaryReader r)
    {
      string level = r.ReadString();
      int id = r.ReadInt32();
      return new EntityID(level, id);
    }

    public void SetPing(int tcp, int? udp)
    {
      Ping_TCP = tcp;
      Ping_UDP = udp ?? tcp;
    }

    public void ApplyUpdate(PlayerState newState)
    {
      RespawnPoint = newState.RespawnPoint;
      CurrentRoom = newState.CurrentRoom;
      Pid = newState.Pid;
      LastUpdateReceived = DateTime.Now;
    }

    public void SendUpdateImmediate()
    {
      if (!Pid.Equals(PlayerID.MyID)) return;  // Safeguard against broadcasting others' statuses
      LastUpdateSent = DateTime.Now;
      CNetComm.Instance.Send(new Data.DataPlayerState()
      {
        newState = this,
      }, false);
    }

    public void CheckSendHeartbeat()
    {
      if ((DateTime.Now - LastUpdateSent).TotalSeconds < heartbeatTime) return;  // Enforce update frequency
      SendUpdateImmediate();
    }

    // Functions to update state

    public void ConnectedToCnet()
    {
      Pid = PlayerID.MyID;
      SendUpdateImmediate();
    }

    public void EnterMap(GlobalAreaKey area, string room = "")
    {
      Logger.Log(LogLevel.Info, "Deathlink", $"Entering map '{area.SID}', room '{room}'. Previous map was '{CurrentMap.SID}'");
      CurrentMap = area;
      CurrentRoom = room;
      RespawnPoint = Vector2.Zero;
    }

    public void EnterMap(AreaKey area, string room = "") => EnterMap(new GlobalAreaKey(area), room);

    internal void EnterOverworld() => EnterMap(GlobalAreaKey.Overworld);

    internal void UpdateRespawn(Vector2 point)
    {
      RespawnPoint = point;
    }

    internal void EnterRoom(string room, Vector2 respawnPoint)
    {
      CurrentRoom = room;
      RespawnPoint = respawnPoint;
    }
  }

  public static class PlayerStateExtensions
  {

    public static PlayerState ReadPlayerState(this CelesteNetBinaryReader r)
    {
      return new PlayerState(r);
    }

    public static void Write(this CelesteNetBinaryWriter w, PlayerState s)
    {
      w.Write(s.Pid);
      w.Write(s.CurrentMap);
      w.Write(s.CurrentRoom ?? "");
      w.Write(s.RespawnPoint);
      if (s.ActivePicker != null)
      {
        w.Write(true);
        w.Write(s.ActivePicker.Value);
      }
      else
      {
        w.Write(false);
      }
    }

    public static void Write(this CelesteNetBinaryWriter w, EntityID id)
    {
      w.Write(id.Level ?? "");
      w.Write(id.ID);
    }
  }
}
