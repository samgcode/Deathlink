using Celeste.Mod.CelesteNet;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Deathlink.Infrastructure
{
  public class SessionSyncState
  {
    public PlayerID player;
    public DateTime instant;
    public string room;
    public bool dead;
    public List<Tuple<EntityID, Vector2>> collectedStrawbs;
    public bool cassette;
    public bool heart;
    public string heartPoem;
    internal bool heartEndsLevel;
    internal bool GoldenDeath;
  }

  public class SessionSynchronizer : Component, ISynchronizable
  {
    private static DateTime lastTriggeredDeathLocal = DateTime.MinValue;
    private static DateTime lastTriggeredDeathRemote = DateTime.MinValue;
    private static bool CurrentDeathIsSecondary = false;

    public static readonly string IDString = "%SESSIONSYNC%";

    private object basicFlagsLock = new object();
    private bool deathPending = false;
    private bool deathPendingIsGolden;
    private bool cassettePending = false;
    private bool heartPending = false;
    private string heartPoem;
    private bool heartEndsLevel;
    public List<Tuple<EntityID, Vector2>> newlyCollectedStrawbs = new List<Tuple<EntityID, Vector2>>();
    private bool levelEndTriggeredRemotely = false;

    public Player playerEntity { get; private set; }

    public SessionSynchronizer(Player p, bool active, bool visible) : base(active, visible)
    {
      playerEntity = p;
    }

    internal void PlayerDied(bool isGoldenDeath)
    {
      if (!CurrentDeathIsSecondary && DeathlinkModule.Settings?.Enabled == true)
      {
        lock (basicFlagsLock)
        {
          deathPending = true;
          deathPendingIsGolden = isGoldenDeath;
          lastTriggeredDeathLocal = DateTime.Now;
        }
      }
    }


    /// <summary>
    /// Determines whether the player is carrying a golden strawberry
    /// </summary>
    /// <returns>true if the player is carrying a golden strawberry, else false</returns>
    private bool PlayerHasGolden()
    {
      return EntityAs<Player>()?.Leader?.Followers?.Any((Follower f) => f.Entity is Strawberry strawb && strawb.Golden) ?? false;
    }

    /// <summary>
    /// Determines whether an incoming death message should cause the local player to die too
    /// </summary>
    /// <param name="sss">The incoming update object</param>
    /// <returns>true if the local player should die, else false</returns>
    private bool ShouldSyncReceivedDeath(SessionSyncState sss)
    {
      return sss.dead
        && PlayerState.Mine?.CurrentRoom != null
        && (DeathlinkModule.Settings?.Enabled == true
          || (sss.GoldenDeath && PlayerHasGolden()))
        && (sss.instant - lastTriggeredDeathRemote).TotalMilliseconds > 1000;
    }

    /// <summary>
    /// This function is called when an update is received with a matching entity ID.
    /// This is the primary function responsible for handling session-level synchronization.
    /// </summary>
    /// <param name="state"></param>
    public void ApplyState(object state)
    {
      // Something weird happened?
      if (state is not SessionSyncState dss) return;
      // quit if it's my ID
      if (dss.player.Equals(PlayerID.MyID)) return;

      // okay, now ACTUALLY apply the incoming updates :)

      Level level = SceneAs<Level>();

      // death sync
      if (ShouldSyncReceivedDeath(dss) && level?.Transitioning == false)
      {
        CurrentDeathIsSecondary = true;  // Prevents death signals from just bouncing back & forth forever
        EntityAs<Player>()?.Die(Vector2.Zero, true, true);
        CurrentDeathIsSecondary = false;
        lastTriggeredDeathRemote = dss.instant;
      }
    }

    private IEnumerator RemoteHeartCollectionRoutine(Level level, Entity coroutineEnity, Player player, AreaKey area, string poemID, bool completeArea)
    {
      while (level.Transitioning)
      {
        yield return null;
      }

      // Setup
      player.Depth = Depths.FormationSequences;
      level.Frozen = true;
      level.CanRetry = false;
      level.FormationBackdrop.Display = true;

      // Immediate actions
      if (completeArea)
      {
        List<Strawberry> list = new List<Strawberry>();
        foreach (Follower follower in player.Leader.Followers)
        {
          if (follower.Entity is Strawberry)
          {
            list.Add(follower.Entity as Strawberry);
          }
        }
        foreach (Strawberry item in list)
        {
          item.OnCollect();
        }
      }

      // Animation
      string poemText = null;
      if (!string.IsNullOrEmpty(poemID))
      {
        poemText = Dialog.Clean("poem_" + poemID);
      }
      Poem poem = new Poem(poemText, (int)area.Mode, string.IsNullOrEmpty(poemText) ? 1f : 0.6f);
      poem.Alpha = 0f;
      Scene.Add(poem);
      for (float t3 = 0f; t3 < 1f; t3 += Engine.RawDeltaTime)
      {
        poem.Alpha = Ease.CubeOut(t3);
        yield return null;
      }

      // Animation finished
      while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed)
      {
        yield return null;
      }
      //sfx.Source.Param("end", 1f);
      if (!completeArea)
      {
        level.FormationBackdrop.Display = false;
        for (float t3 = 0f; t3 < 1f; t3 += Engine.RawDeltaTime * 2f)
        {
          poem.Alpha = Ease.CubeIn(1f - t3);
          yield return null;
        }

        // Cleanup
        player.Depth = Depths.Player;
        level.Frozen = false;
        level.CanRetry = true;
        level.FormationBackdrop.Display = false;
        if (poem != null)
        {
          poem.RemoveSelf();
        }
        coroutineEnity.RemoveSelf();
      }
      else
      {
        FadeWipe fadeWipe = new FadeWipe(level, wipeIn: false);
        fadeWipe.Duration = 3.25f;
        yield return fadeWipe.Duration;
        level.CompleteArea(spotlightWipe: false, skipScreenWipe: true, skipCompleteScreen: false);
      }
    }

    public EntityID GetID() => GetIDStatic();

    public static EntityID GetIDStatic() => new EntityID(IDString, 99999);

    public bool CheckRecurringUpdate() => false;

    public void WriteState(CelesteNetBinaryWriter w)
    {
      lock (basicFlagsLock)
      {
        w.Write(deathPending);
        deathPending = false;
        w.Write(deathPendingIsGolden);
        deathPendingIsGolden = false;
        w.Write(cassettePending);
        cassettePending = false;
        w.Write(heartPending);
        heartPending = false;
        w.Write(heartEndsLevel);
        w.Write(heartPoem ?? "");
        heartPoem = "";
      }
      w.Write(PlayerID.MyID);
      w.Write(lastTriggeredDeathLocal);
      w.Write(PlayerState.Mine?.CurrentRoom ?? "");
      lock (newlyCollectedStrawbs)
      {
        w.Write(newlyCollectedStrawbs.Count);
        foreach (Tuple<EntityID, Vector2> tup in newlyCollectedStrawbs)
        {
          w.Write(tup.Item1);
          w.Write(tup.Item2);
        }
        newlyCollectedStrawbs.Clear();
      }
    }

    public static SessionSyncState ParseState(CelesteNetBinaryReader r)
    {
      SessionSyncState state = new SessionSyncState
      {
        dead = r.ReadBoolean(),
        GoldenDeath = r.ReadBoolean(),
        cassette = r.ReadBoolean(),
        heart = r.ReadBoolean(),
        heartEndsLevel = r.ReadBoolean(),
        heartPoem = r.ReadString(),
        player = r.ReadPlayerID(),
        instant = r.ReadDateTime(),
        room = r.ReadString(),
      };
      List<Tuple<EntityID, Vector2>> strawbs = new List<Tuple<EntityID, Vector2>>();
      int count = r.ReadInt32();
      for (int i = 0; i < count; i++)
      {
        strawbs.Add(new Tuple<EntityID, Vector2>(
          r.ReadEntityID(),
          r.ReadVector2()));
      }
      state.collectedStrawbs = strawbs;
      return state;
    }

  }
}
