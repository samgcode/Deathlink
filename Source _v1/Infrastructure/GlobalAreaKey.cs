using Celeste.Mod.CelesteNet;
using Celeste.Mod.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public struct GlobalAreaKey
  {

    public static GlobalAreaKey Overworld
    {
      get
      {
        return new GlobalAreaKey("Overworld");
      }
    }

    #region Raw Data

    private readonly string _sid;
    private readonly AreaKey? _localKey;
    private readonly AreaData _areaData;
    private string _versionString;  // lazily populated
    private ModContent _modContent;  // lazily populated
    private string _cachedDisplayName;

    #endregion

    #region Useful Properties

    public AreaKey? Local { get { return _localKey; } }
    public AreaData Data { get { return _areaData; } }
    public string SID { get { return _sid; } }
    public Version LocalVersion
    {
      get
      {
        if (IsOverworld || !ExistsLocal || IsVanilla) return Celeste.Instance.Version;
        else return ModContent?.Mod?.Version ?? new Version(1, 0, 0);
      }
    }
    public string VersionString
    {
      get
      {
        if (string.IsNullOrEmpty(_versionString)) _versionString = LocalVersion.ToString();
        return _versionString;
      }
    }
    public Version Version { get { return new Version(VersionString); } }
    public AreaMode Mode { get { return ExistsLocal ? Local.Value.Mode : AreaMode.Normal; } }
    public MapMeta ModeMeta { get { return !ExistsLocal ? null : _areaData.GetModeMeta(Mode); } }
    public MapMetaModeProperties ModeMetaProperties { get { return !ExistsLocal ? null : _areaData.GetModeMeta(Mode)?.Modes[(int)Mode]; } }
    public ModContent ModContent
    {
      get
      {
        if (_localKey == null) return null;
        if (_modContent == null) _modContent = Util.GetModContent(this);
        return _modContent;
      }
    }
    public bool VersionMatchesLocal { get { return Version == LocalVersion; } }
    public bool ExistsLocal { get { return _localKey != null; } }
    public bool IsOverworld { get { return _localKey == null && _sid == "Overworld"; } }
    public bool IsVanilla { get { return ExistsLocal && Local?.LevelSet == "Celeste"; } }
    public string DisplayName
    {
      get
      {
        string dname = "";
        if (IsOverworld)
        {
          dname = Dialog.Clean("GhostToast_Deathlink_Overworld");
        }
        else if (ExistsLocal)
        {
          dname = Dialog.Get(Data.Name) + GetTranslatedSide(_localKey?.Mode);
        }
        else if (!string.IsNullOrEmpty(_cachedDisplayName))
        {
          dname = _cachedDisplayName;
        }
        else
        {
          return "<Map Not Installed>";
        }
        _cachedDisplayName = dname;
        return dname;
      }
    }
    internal string CachedDisplayName { get { return _cachedDisplayName; } }

    #endregion

    #region Constructors

    public GlobalAreaKey(string SID) : this(SID, AreaMode.Normal, null, "") { }
    public GlobalAreaKey(string SID, AreaMode mode, string version, string dispName)
    {
      _sid = SID;
      _localKey = null;
      _areaData = null;
      _modContent = null;
      _versionString = version;
      _cachedDisplayName = dispName;
      if (SID != "Overworld" && AreaData.Areas != null)
      {
        foreach (AreaData d in AreaData.Areas)
        {
          if (d.SID == SID)
          {
            _areaData = d;
            _localKey = new AreaKey(d.ID, mode);
          }
        }
      }
    }
    public GlobalAreaKey(AreaKey localKey)
    {
      _localKey = localKey;
      _sid = localKey.SID;
      _areaData = AreaData.Areas[localKey.ID];
      _modContent = null;
      _versionString = null;
      _cachedDisplayName = "";  // All fields must be initialized before using 'this'
      _cachedDisplayName = DisplayName;
    }
    public GlobalAreaKey(int localID, AreaMode mode = AreaMode.Normal) : this()
    {
      _areaData = AreaData.Areas[localID];
      _localKey = _areaData.ToKey(mode);
      _sid = _areaData.SID;
      _modContent = null;
      _versionString = null;
    }

    #endregion

    #region Helpers and Overrides

    private static string GetTranslatedSide(AreaMode? mode)
    {
      switch (mode)
      {
        case null:
        case AreaMode.Normal:
          return "";
        case AreaMode.BSide:
          return " (" + Dialog.Get("OVERWORLD_REMIX") + ")";
        case AreaMode.CSide:
          return " (" + Dialog.Get("OVERWORLD_REMIX2") + ")";
        default:
          return " (" + mode.ToString() + ")";
      }
    }

    public override bool Equals(object obj)
    {
      if (obj is GlobalAreaKey k)
      {
        return k.SID == SID && k.Mode == Mode && k.VersionString == VersionString;
      }
      return false;
    }

    public override int GetHashCode()
    {
      return (SID + Mode.ToString() + VersionString).GetHashCode();
    }

    #endregion
  }

  public static class GlobalAreaKeyRelatedExtensions
  {

    public static GlobalAreaKey ReadAreaKey(this CelesteNetBinaryReader reader)
    {
      string sid = reader.ReadString();
      AreaMode mode = (AreaMode)Enum.Parse(typeof(AreaMode), reader.ReadString());
      string version = reader.ReadString();
      string cachedDispName = reader.ReadString();
      return new GlobalAreaKey(sid, mode, version, cachedDispName);
    }

    public static void Write(this CelesteNetBinaryWriter writer, GlobalAreaKey area)
    {
      writer.Write(area.SID);
      writer.Write(area.Mode.ToString());
      writer.Write(area.VersionString);
      writer.Write(area.CachedDisplayName ?? "");
    }
  }

}
