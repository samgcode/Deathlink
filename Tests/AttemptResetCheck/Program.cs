using Celeste.Mod.Deathlink.Data;

string summitA = AreaScope.Create("Celeste/7-Summit", 0);
string summitB = AreaScope.Create("Celeste/7-Summit", 1);
string forsakenCityA = AreaScope.Create("Celeste/1-ForsakenCity", 0);
Assert(summitA != summitB, "A-side and B-side must never share checkpoint state");
Assert(!AreaScope.AllowsDeath(true, summitA, summitB), "Checkpoint sync must reject deaths from another side");
Assert(!AreaScope.AllowsDeath(true, summitA, forsakenCityA), "Checkpoint sync must reject deaths from another chapter");
Assert(AreaScope.AllowsDeath(true, summitA, summitA), "Checkpoint sync must allow deaths in the same chapter and side");
Assert(AreaScope.AllowsDeath(false, summitA, forsakenCityA), "Cross-map Deathlink must remain available when checkpoint sync is disabled");

RoomCheckpoint checkpointA = At("A", 0, 0);
RoomCheckpoint checkpointB = At("B", 100, 0);
RoomCheckpoint checkpointC = At("C", 200, 0);
Assert(!At("", 0, 0).IsValid, "A checkpoint must include a room");
Assert(!At("A", float.NaN, 0).IsValid, "A checkpoint must reject invalid network coordinates");

PlayerRoomProgress[] players = Enumerable.Range(0, 4)
    .Select(_ => new PlayerRoomProgress())
    .ToArray();
RoomCheckpoint committedCheckpoint = checkpointA;

foreach (PlayerRoomProgress player in players)
{
    Assert(player.RecordCheckpoint(checkpointA, null), "Players must start at A");
}
Commit(players, checkpointA);

foreach (PlayerRoomProgress player in players.Take(3))
{
    Assert(player.RecordCheckpoint(checkpointB, checkpointA), "Players 1-3 must reach B in attempt 1");
}

Assert(!CanCommit(players, checkpointB), "Player 4 is still at A");
ResetAttempt(players, checkpointA);

Assert(!players[0].RecordCheckpoint(checkpointB, checkpointA), "Pre-respawn B updates must be ignored");
foreach (PlayerRoomProgress player in players)
{
    Assert(player.RecordCheckpoint(checkpointA, checkpointA), "Every player must return to A");
}

foreach (PlayerRoomProgress player in new[] { players[0], players[1], players[3] })
{
    Assert(player.RecordCheckpoint(checkpointB, checkpointA), "Players 1, 2, and 4 must reach B in attempt 2");
}

Assert(!CanCommit(players, checkpointB), "Attempt 1 progress must not count in attempt 2");
ResetAttempt(players, checkpointA);

foreach (PlayerRoomProgress player in players)
{
    Assert(player.RecordCheckpoint(checkpointA, checkpointA), "Every player must return to A again");
    Assert(player.RecordCheckpoint(checkpointB, checkpointA), "Every player must reach B in the same attempt");
}

Assert(CanCommit(players, checkpointB), "B should commit when everyone reaches it without a death");
committedCheckpoint = checkpointB;
Commit(players, committedCheckpoint);

for (int i = 0; i < players.Length; i++)
{
    Assert(players[i].RecordCheckpoint(checkpointA, committedCheckpoint), "Players may return to A");
    if (i < players.Length - 1)
    {
        Assert(!CanCommit(players, checkpointA), "A must wait for every player");
        Assert(committedCheckpoint == checkpointB, "B must remain committed while anyone has not returned to A");
    }
}

Assert(CanCommit(players, checkpointA), "A should become shared when the final player returns");
committedCheckpoint = checkpointA;
Commit(players, committedCheckpoint);

RoomCheckpoint[] branchCheckpoints =
{
    At("X", 300, 0),
    At("Y", 400, 0),
    At("Z", 500, 0),
    At("W", 600, 0),
};
for (int i = 0; i < players.Length; i++)
{
    Assert(players[i].RecordCheckpoint(branchCheckpoints[i], committedCheckpoint), "Players may take different paths");
    Assert(players[i].RecordCheckpoint(checkpointC, committedCheckpoint), "Every path may converge at C");
}

Assert(CanCommit(players, checkpointC), "C should commit after all branches converge there");

RoomCheckpoint flag20 = At("summit", 2000, 100);
RoomCheckpoint flag19 = At("summit", 1900, 100);
PlayerRoomProgress[] summitPlayers = Enumerable.Range(0, 3)
    .Select(_ => new PlayerRoomProgress())
    .ToArray();
foreach (PlayerRoomProgress player in summitPlayers)
{
    Assert(player.RecordCheckpoint(flag20, null), "Players must start at flag 20");
}
Commit(summitPlayers, flag20);

Assert(flag20.Room == flag19.Room && flag20 != flag19, "Same-room flags must be distinct checkpoints");
Assert(summitPlayers[0].RecordCheckpoint(flag19, flag20), "Player 1 may reach flag 19 first");
Assert(summitPlayers[1].RecordCheckpoint(flag19, flag20), "Player 2 may reach flag 19 second");
Assert(!CanCommit(summitPlayers, flag19), "Flag 19 must wait for player 3");
Assert(summitPlayers[2].RecordCheckpoint(flag19, flag20), "Player 3 may reach flag 19 last");
Assert(CanCommit(summitPlayers, flag19), "Flag 19 must commit after every player reaches it asynchronously");
Commit(summitPlayers, flag19);

Assert(summitPlayers[0].RecordCheckpoint(flag20, flag19), "Player 1 may return to flag 20");
Assert(summitPlayers[1].RecordCheckpoint(flag20, flag19), "Player 2 may return to flag 20");
Assert(!CanCommit(summitPlayers, flag20), "Backward flag progress must wait for player 3");
Assert(summitPlayers[2].RecordCheckpoint(flag20, flag19), "Player 3 may return to flag 20 last");
Assert(CanCommit(summitPlayers, flag20), "The previous flag must become current after everyone returns");

RoomCheckpoint checkpointF = At("F", 700, 0);
PlayerRoomProgress joiningPlayer = new();
joiningPlayer.ResetAttempt(checkpointF);
Assert(!joiningPlayer.RecordCheckpoint(At("1", 0, 0), checkpointF), "A joining player must not replace the shared checkpoint with its local checkpoint");
Assert(joiningPlayer.AwaitingCommittedCheckpoint, "A joining player must wait for the received checkpoint");
Assert(joiningPlayer.RecordCheckpoint(checkpointF, checkpointF), "The joining player must be accepted at the received checkpoint");

Console.WriteLine("Checkpoint progress regression checks passed.");

static RoomCheckpoint At(string room, float x, float y)
    => new(room, x, y);

static void ResetAttempt(IEnumerable<PlayerRoomProgress> players, RoomCheckpoint committedCheckpoint)
{
    foreach (PlayerRoomProgress player in players)
    {
        player.ResetAttempt(committedCheckpoint);
    }
}

static void Commit(IEnumerable<PlayerRoomProgress> players, RoomCheckpoint checkpoint)
{
    foreach (PlayerRoomProgress player in players)
    {
        player.ConsumeThrough(checkpoint);
    }
}

static bool CanCommit(IReadOnlyCollection<PlayerRoomProgress> players, RoomCheckpoint candidateCheckpoint)
    => RoomProgressRules.CanCommit(candidateCheckpoint, players);

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
