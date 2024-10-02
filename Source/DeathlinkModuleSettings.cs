using System;

namespace Celeste.Mod.Deathlink;

public class DeathlinkModuleSettings : EverestModuleSettings
{

  [SettingRange(2, 100)]
  public int Players { get; set; } = 2;

  public TextMenu.Button StartDeathlink { get; set; } = null;

  public void CreateStartDeathlinkEntry(TextMenu menu, bool inGame)
  {
    TextMenu.Button item = CreateMenuButton(menu, "START", null, () =>
    {
      DeathlinkModule.Instance.Start();
    });
  }

  public TextMenu.Button CreateMenuButton(TextMenu menu, string dialogLabel, Func<string, string> dialogTransform, Action onPress)
  {
    string label = $"modoptions_deathlink_{dialogLabel}".DialogClean();
    TextMenu.Button item = new TextMenu.Button(dialogTransform?.Invoke(label) ?? label);
    item.Pressed(onPress);
    menu.Add(item);
    return item;
  }
}
