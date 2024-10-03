using System;

namespace Celeste.Mod.Deathlink;

public class DeathlinkModuleSettings : EverestModuleSettings
{

  [SettingRange(1, 100)]
  public int Team { get; set; } = 1;

  [SettingName("Kill Others")]
  // [SettingName("modoptions_deathlink_kill_others")]
  public bool KillOthers { get; set; } = false;

  [SettingName("Receive Deaths")]
  // [SettingName("modoptions_deathlink_receive_deaths")]
  public bool ReceiveDeaths { get; set; } = false;
}
