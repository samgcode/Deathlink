using Celeste.Mod.CelesteNet;
using Celeste.Mod.Deathlink.IO;
using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public struct PlayerID
  {
    public static PlayerID MyID
    {
      get
      {
        CNetComm comm = CNetComm.Instance;
        int macHash = LocalMACAddressHash;
        string name = GetName();
        uint id = comm?.CnetID ?? uint.MaxValue;
        return new PlayerID(macHash, id, name);
      }
    }
    public static int LocalMACAddressHash
    {
      get
      {
        if (_localMACHash == null) SearchMACAddress();
        return _localMACHash ?? 0;
      }
    }
    private static int? _localMACHash = null;
    private static string _lastKnownName;
    private static string GetName()
    {
      string name = CNetComm.Instance?.CnetClient?.PlayerInfo?.Name;
      if (string.IsNullOrEmpty(name))
      {
        name = _lastKnownName;
      }
      _lastKnownName = name;
      return _lastKnownName;
    }
    private static void SearchMACAddress()
    {
      try
      {
        _localMACHash = (
          from nic in NetworkInterface.GetAllNetworkInterfaces()
          where nic.OperationalStatus == OperationalStatus.Up
          select nic.GetPhysicalAddress().ToString()
        )?.FirstOrDefault()?.GetHashCode();
      }
      catch (Exception e)
      {
        Logger.Log("Deathlink", "Could not get MAC address: " + e.Message);
      }
    }

    public PlayerID(int? addrHash, uint cnetID, string name)
    {
      MacAddressHash = addrHash;
      CNetID = cnetID;
      Name = name;
    }
    public PlayerID(PlayerID orig)
    {
      MacAddressHash = orig.MacAddressHash;
      CNetID = orig.CNetID;
      Name = orig.Name;
    }
    public int? MacAddressHash { get; private set; }
    public string Name { get; private set; }
    public uint CNetID { get; private set; }

    public bool MatchAndUpdate(PlayerID id)
    {
      if (this.Equals(id))
      {
        CNetID = id.CNetID;
        return true;
      }
      return false;
    }

    public bool IsDefault()
    {
      return MacAddressHash == null && string.IsNullOrEmpty(Name);
    }

    public override bool Equals(object obj)
    {
      return obj != null && obj is PlayerID id && id.MacAddressHash == MacAddressHash && id.Name == Name;
    }

    public override int GetHashCode()
    {
      return ((MacAddressHash ?? 0) + Name).GetHashCode();
    }
  }

  public static class PlayerIDExt
  {
    public static PlayerID ReadPlayerID(this CelesteNetBinaryReader r)
    {
      bool hasmac = r.ReadBoolean();
      int? mac = hasmac ? (int?)r.ReadInt32() : null;
      string name = r.ReadString();
      uint cnetid = r.ReadUInt32();
      return new PlayerID(mac, cnetid, name);
    }
    public static void Write(this CelesteNetBinaryWriter w, PlayerID id)
    {
      if (id.MacAddressHash == null)
      {
        w.Write(false);
      }
      else
      {
        w.Write(true);
        w.Write(id.MacAddressHash ?? 0);
      }
      w.Write(id.Name ?? "");
      w.Write(id.CNetID);
    }
  }
}
