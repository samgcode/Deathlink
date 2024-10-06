using System;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.Deathlink;

public class DeathlinkModuleSettings : EverestModuleSettings
{
  [SettingName("DEATHLINK_ENABLE")]
  [SettingSubText("DEATHLINK_ENABLE_DESC")]
  public bool Enabled { get; set; } = false;

  [SettingName("DEATHLINK_KILL_OTHERS")]
  [SettingSubText("DEATHLINK_KILL_OTHERS_DESC")]
  public bool KillOthers { get; set; } = true;

  [SettingName("DEATHLINK_RECEIVE_DEATHS")]
  [SettingSubText("DEATHLINK_RECEIVE_DEATHS_DESC")]
  public bool ReceiveDeaths { get; set; } = true;

  [SettingName("DEATHLINK_LOCATION_MODE")]
  [SettingSubText("DEATHLINK_LOCATION_MODE_DESC")]
  public DeathlinkModule.LocationModes LocationMode { get; set; } = DeathlinkModule.LocationModes.Everywhere;

  [SettingName("DEATHLINK_ANNOUNCE_MODE")]
  [SettingSubText("DEATHLINK_ANNOUNCE_MODE_DESC")]
  public DeathlinkModule.AnnounceModes AnnounceMode { get; set; } = DeathlinkModule.AnnounceModes.Team;

  [SettingName("DEATHLINK_TEAM")]
  [SettingSubText("DEATHLINK_TEAM_DESC")]
  [SettingRange(1, 100)]
  public int Team { get; set; } = 1;

  public TextMenu.Button ShowDeaths { get; set; } = null;

  #region Key Bindings

  [SettingName("DEATHLINK_TOGGLE_BIND")]
  [DefaultButtonBinding(0, 0)]
  public ButtonBinding ToggleBind { get; set; }

  [SettingName("DEATHLINK_LIST_BIND")]
  [DefaultButtonBinding(0, 0)]
  public ButtonBinding ListPlayersBind { get; set; }

  #endregion


  public void CreateShowDeathsEntry(TextMenu menu, bool inGame)
  {
    TextMenu.Button item = CreateMenuButton(menu, "ShowDeaths", null, () =>
    {
      DeathlinkModule.ListDeaths();
    });
  }

  public TextMenu.Button CreateMenuButton(TextMenu menu, string dialogLabel, Func<string, string> dialogTransform, Action onPress)
  {
    string label = $"DEATHLINK_{dialogLabel}".DialogClean();
    TextMenu.Button item = new TextMenu.Button(dialogTransform?.Invoke(label) ?? label);
    item.Pressed(onPress);
    menu.Add(item);
    return item;
  }
}
