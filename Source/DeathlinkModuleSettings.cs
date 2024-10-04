using System;

namespace Celeste.Mod.Deathlink;

public class DeathlinkModuleSettings : EverestModuleSettings
{
  [SettingName("DEATHLINK_ENABLE")]
  [SettingSubHeader("DEATHLINK_ENABLE_DESC")]
  public bool Enabled { get; set; } = false;

  [SettingName("DEATHLINK_KILL_OTHERS")]
  [SettingSubHeader("DEATHLINK_KILL_OTHERS_DESC")]
  public bool KillOthers { get; set; } = true;

  [SettingName("DEATHLINK_RECEIVE_DEATHS")]
  [SettingSubHeader("DEATHLINK_RECEIVE_DEATHS_DESC")]
  public bool ReceiveDeaths { get; set; } = true;

  [SettingName("DEATHLINK_TEAM")]
  [SettingSubHeader("DEATHLINK_TEAM_DESC")]
  [SettingRange(1, 100)]
  public int Team { get; set; } = 1;
}
