using Monocle;
using Microsoft.Xna.Framework;
using Celeste.Mod.CoopHelper.Entities;
using Celeste.Mod.CoopHelper.Entities.Helper;
using Celeste.Mod.CoopHelper.Infrastructure;

namespace Celeste.Mod.Deathlink
{
  class CustomSessionPickerEntity : SessionPickerEntity
  {
    Player player;
    SessionPickerHUD hud;
    SessionPickerAvailabilityInfo availabilityInfo;
    private string roomNameWithOverride;
    private int localID;

    private EntityID ID => new(PlayerState.Mine?.CurrentMap.SID + roomNameWithOverride, localID);

    int playersRequired = 2;

    public CustomSessionPickerEntity(EntityData data, Vector2 offset) : base(data, offset)
    {
      availabilityInfo = new SessionPickerAvailabilityInfo();

      roomNameWithOverride = data.Level?.Name ?? PlayerState.Mine?.CurrentRoom;
      localID = data.ID;
      playersRequired = data.Int("playersRequired", 2);
    }

    public new void Open(Player player)
    {
      Logger.Log(LogLevel.Info, "Deathlink", $"Open, {playersRequired}");

      if (this.hud != null) return;  // Already open
      this.hud = new SessionPickerHUD(this.availabilityInfo, playersRequired, this.ID, null, Close);
      Scene.Add(hud);
      player.StateMachine.State = Player.StDummy;
      this.player = player;
      Audio.Play("event:/ui/game/pause");
    }
  }

}
