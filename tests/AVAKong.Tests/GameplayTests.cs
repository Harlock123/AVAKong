using AVAKong.Engine;
using AVAKong.Models;
using AVAKong.Views;

namespace AVAKong.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kong-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A game under way on the given stage, with nothing thrown or spawned.</summary>
    private GameEngine Quiet(int level = 1, int stage = 0, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(level, stage);
        g.Quiet();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    private static readonly Controls Right = new(1, 0, false), Left = new(-1, 0, false), Up = new(0, -1, false), Down = new(0, 1, false), Jump = new(0, 0, true);

    /// <summary>Platforms reachable from one by ladders and by walking (or hopping a rivet's gap) along a floor.</summary>
    private static HashSet<Platform> Reachable(Stage s, Platform from)
    {
        var seen = new HashSet<Platform> { from };
        var queue = new Queue<Platform>(seen);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            var next = s.Ladders.Where(l => !l.Broken && (l.Lower == p || l.Upper == p)).Select(l => l.Lower == p ? l.Upper : l.Lower)
                .Concat(s.Platforms.Where(q => !q.Lift && (Math.Abs(q.X0 - p.X1) <= 10 && Math.Abs(q.YAt(q.X0) - p.YAt(p.X1)) <= 3
                                                         || Math.Abs(p.X0 - q.X1) <= 10 && Math.Abs(q.YAt(q.X1) - p.YAt(p.X0)) <= 3)));
            foreach (var n in next)
                if (seen.Add(n)) queue.Enqueue(n);
        }
        return seen;
    }

    [Fact]
    public void Every_stage_can_be_climbed()
    {
        foreach (var kind in Enum.GetValues<StageKind>())
        {
            var s = Stage.Build(kind);
            var start = s.Platforms.Single(p => p.Covers(s.Start.X) && Math.Abs(p.YAt(s.Start.X) - s.Start.Y) < 1);
            var reach = Reachable(s, start);
            if (kind == StageKind.Elevators)
                // Up there only by riding the lifts: but from the islands the ladders lead on to the top.
                Assert.Contains(s.Goal, Reachable(s, s.Platforms.First(p => p.YAt(110) == 152 || p.Y0 == 152)));
            else if (kind == StageKind.Rivets)
                Assert.All(s.Rivets, r => Assert.Contains(s.Platforms.First(p => !p.Plug && p.Covers(r.X - 6) && p.Y0 == r.Plug.Y0), reach));
            else
                Assert.Contains(s.Goal, reach);
            Assert.All(s.Ladders, l => Assert.True(l.Bottom - l.Top >= 20, $"{kind} ladder at {l.X}"));
        }
        Assert.Equal(8, Stage.Build(StageKind.Rivets).Rivets.Count);
        Assert.Equal([StageKind.Girders, StageKind.Rivets], Stage.Sequence(1));
        Assert.Equal(5, Stage.Sequence(4).Length);
        Assert.Equal(StageKind.Rivets, Stage.Sequence(9)[^1]);
    }

    [Fact]
    public void A_game_starts_with_how_high_then_the_girders()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.StartGame();
        Assert.Equal(Phase.Intro, g.Phase);
        Assert.True(g.ShowHowHigh);
        Assert.Contains(Sfx.HowHigh, heard);
        Assert.Equal(StageKind.Girders, g.Stage.Kind);
        Assert.Equal(25, g.Metres);
        Assert.Equal(3, g.Lives);
        Assert.Equal(5000, g.Bonus);
        Steps(g, (int)(60 * GameEngine.IntroSeconds) + 2);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Contains(Sfx.MusicGirders, heard);
    }

    [Fact]
    public void Walking_follows_the_slope_and_off_the_end_is_a_fall()
    {
        var g = Quiet();
        var h = g.Hero;
        g.PutHero(130, 248);
        double before = h.Feet;
        Steps(g, 60, Right);
        Assert.True(h.X > 170);
        Assert.True(h.Feet < before, "the bottom girder rises to the right");
        Assert.Equal(h.On!.YAt(h.X), h.Feet, 6);

        // Off the end of a girder onto the one below: too far to survive.
        g.PutHero(200, 220);
        Steps(g, 60, Right);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("fell", g.LastDeath);
    }

    [Fact]
    public void A_jump_goes_up_about_nineteen_pixels_and_comes_back_down()
    {
        var g = Quiet();
        var h = g.Hero;
        g.PutHero(60, 248);
        g.Step(Jump);
        Assert.Equal(HeroState.Jumping, h.State);
        double top = h.Feet;
        int steps = 1;
        while (h.State == HeroState.Jumping && steps < 120) { g.Step(default); top = Math.Min(top, h.Feet); steps++; }
        Assert.InRange(248 - top, 17, 20);
        Assert.InRange(steps / 60.0, 0.5, 0.6);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(60, h.X, 3);

        // With a run-up it carries on the way it was going.
        g.Step(new Controls(1, 0, true));
        while (h.State == HeroState.Jumping) g.Step(default);
        Assert.InRange(h.X - 60, 22, 28);
    }

    [Fact]
    public void Ladders_go_up_and_down_but_broken_ones_cannot_be_climbed()
    {
        var g = Quiet();
        var h = g.Hero;
        g.PutHero(184, 248);
        Steps(g, 1, Up);
        Assert.Equal(HeroState.Climbing, h.State);
        Steps(g, 60 * 2, Up);
        Assert.Equal(HeroState.Walking, h.State);
        Assert.Equal(g.Stage.Ladders.First(l => l.X == 184 && l.Lower.Y0 == 248).Upper, h.On);
        Steps(g, 60 * 2, Down);
        Assert.Equal(HeroState.Walking, h.State);
        Assert.Equal(h.On!.YAt(184), h.Feet, 6);
        Assert.True(h.Feet > 240, "back on the bottom girder");

        g.PutHero(80, 248); // the broken ladder
        Steps(g, 30, Up);
        Assert.Equal(HeroState.Walking, h.State);
        Assert.Equal(248, h.Feet);
    }

    [Fact]
    public void A_barrel_rolls_downhill_and_jumping_it_scores()
    {
        var g = Quiet();
        var b = g.PutBarrel(30, 220, 1);
        double y = b.Y;
        Steps(g, 30);
        Assert.True(b.X > 30 && b.Y > y, "rolling downhill to the right");

        g = Quiet();
        g.PutHero(150, 248);
        g.PutBarrel(180, 248, -1);
        // Jump as it comes: about 18 pixels away.
        while (g.Barrels[0].X - g.Hero.X > 18) g.Step(default);
        g.Step(Jump);
        Steps(g, 60);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(100, g.Score);
    }

    [Fact]
    public void A_barrel_that_reaches_the_hero_costs_a_life_and_the_stage_starts_again()
    {
        var g = Quiet();
        g.PutHero(150, 248);
        g.PutBarrel(190, 248, -1);
        Steps(g, 60);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("barrel", g.LastDeath);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(2, g.Lives);
        Assert.Equal(Phase.Intro, g.Phase);
        Assert.False(g.ShowHowHigh);
        Assert.Equal(g.Stage.Start.X, g.Hero.X);
    }

    [Fact]
    public void The_apes_first_barrel_goes_straight_down_and_lights_the_drum()
    {
        var g = new GameEngine(2, new HighScoreTable(_dir));
        g.StartGame();
        g.Go();
        g.PutHero(200, 213); // out of the way, on the second girder
        for (int i = 0; i < 60 * 12 && !g.DrumLit; i++) g.Step(default);
        Assert.True(g.DrumLit);
        Assert.NotEmpty(g.Fireballs);
    }

    [Fact]
    public void The_hammer_smashes_barrels_for_points_but_stops_the_hero_climbing()
    {
        var g = Quiet();
        var hammer = g.Stage.Pickups.First(p => p.IsHammer);
        g.PutHero(hammer.Pos.X - 2, hammer.Pos.Y + 22);
        g.Step(Jump);
        Steps(g, 40);
        Assert.True(hammer.Taken);
        Assert.True(g.HammerLeft > 9);
        var b = g.PutBarrel(g.Hero.X + 40, g.Hero.Feet, -1);
        g.Hero.Facing = 1;
        Steps(g, 60);
        Assert.True(b.Dead);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Contains(g.Score, new[] { 300, 800 });
        // No climbing or jumping with the hammer.
        var ladder = g.Stage.Ladders.First(l => !l.Broken && l.Lower == g.Hero.On);
        g.PutHero(ladder.X, ladder.Bottom);
        Steps(g, 10, Up);
        Assert.Equal(HeroState.Walking, g.Hero.State);
        Steps(g, 60 * 10);
        Assert.Equal(0, g.HammerLeft);
    }

    [Fact]
    public void Her_belongings_are_worth_more_on_later_levels()
    {
        var g = Quiet();
        var item = g.Stage.Pickups.First(p => !p.IsHammer);
        g.PutHero(item.Pos.X - 10, item.Pos.Y + 7);
        Steps(g, 30, Right);
        Assert.True(item.Taken);
        Assert.Equal(300, g.Score);
        Assert.Equal(800, GameEngine.ItemPoints(3));
    }

    [Fact]
    public void The_bonus_runs_down_and_reaching_the_captive_banks_it()
    {
        var g = Quiet();
        Steps(g, 60 * 4 + 2);
        Assert.Equal(4800, g.Bonus);
        var ladder = g.Stage.Ladders.Single(l => l.Upper == g.Stage.Goal);
        g.PutHero(ladder.X, ladder.Bottom);
        Steps(g, 60 * 3, Up);
        Assert.Equal(Phase.StageClear, g.Phase);
        Assert.Equal(4800, g.Score);
        Steps(g, (int)(60 * GameEngine.StageClearSeconds) + 2);
        Assert.Equal(StageKind.Rivets, g.Stage.Kind);
        Assert.Equal(50, g.Metres);
        Assert.True(g.ShowHowHigh);

        g = Quiet();
        g.SetBonus(100);
        Steps(g, 60 * 2 + 2);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("time", g.LastDeath);
    }

    [Fact]
    public void Pulling_every_rivet_brings_the_ape_down_and_the_next_level_begins()
    {
        var g = Quiet(stage: 1);
        Assert.Equal(StageKind.Rivets, g.Stage.Kind);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        var first = g.Stage.Rivets[0];
        g.PutHero(first.X - 10, first.Plug.Y0);
        Steps(g, 30, Right);
        Assert.True(first.Pulled);
        Assert.Equal(100, g.Score);
        Assert.Contains(Sfx.RivetPop, heard);
        Assert.Equal(Phase.Playing, g.Phase);
        // The gap is behind the hero: walk back into it and down they go.
        Steps(g, 60, Left);
        Assert.Equal(Phase.Dying, g.Phase);
        Steps(g, (int)(60 * (GameEngine.DyingSeconds + GameEngine.RestartSeconds)) + 4);
        Assert.True(g.Stage.Rivets[0].Pulled, "pulled rivets stay pulled");
        g.Quiet();

        foreach (var r in g.Stage.Rivets.Skip(1))
        {
            g.PutHero(r.X - 10, r.Plug.Y0);
            Steps(g, 30, Right);
            Assert.True(r.Pulled);
        }
        Assert.Equal(Phase.LevelClear, g.Phase);
        Assert.Contains(Sfx.LevelClear, heard);
        Steps(g, (int)(60 * GameEngine.LevelClearSeconds) + 2);
        Assert.Equal(2, g.Level);
        Assert.Equal(StageKind.Girders, g.Stage.Kind);
    }

    [Fact]
    public void Belts_carry_the_hero_trays_ride_them_and_the_fire_burns()
    {
        var g = Quiet(level: 3, stage: 1);
        Assert.Equal(StageKind.Conveyors, g.Stage.Kind);
        var belt = g.Stage.Platforms.First(p => p.Belt != 0 && p.Y0 == 208);
        g.PutHero(100, 208);
        double x = g.Hero.X;
        Steps(g, 60);
        Assert.Equal(x + belt.Belt, g.Hero.X, 1);

        var tray = g.PutTray(belt.Belt > 0 ? belt.X1 - 10 : belt.X0 + 10, belt);
        g.PutHero(20, 248);
        Steps(g, 60);
        Assert.True(tray.Dead, "it rode off the end");

        g.PutHero(80, 168);
        Steps(g, 60 * 2, Right);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("burned", g.LastDeath);
    }

    [Fact]
    public void Lifts_ride_round_their_shafts_and_springs_drop_down_the_right()
    {
        var g = Quiet(level: 3, stage: 2);
        Assert.Equal(StageKind.Elevators, g.Stage.Kind);
        var up = g.Stage.Shafts[0].Lifts[0];
        double y = up.Y0;
        Steps(g, 30);
        Assert.True(up.Y0 < y || up.Y0 > y + 100, "rising (or come round again)");

        // Riding a lift all the way up is fatal.
        var lift = g.Stage.Shafts[0].Lifts.OrderBy(l => l.Y0).First(l => l.Y0 > 120);
        g.PutHero(72, lift.Y0);
        Assert.Equal(lift, g.Hero.On);
        for (int i = 0; i < 60 * 10 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("lift", g.LastDeath);

        g = new GameEngine(3, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(3, 2);
        g.Idle(60 * 6);
        Assert.Contains(g.Jacks, j => j.Dropping && j.X == 206);
    }

    [Fact]
    public void A_bonus_life_at_ten_thousand()
    {
        var g = Quiet();
        g.AddScore(9999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(20000);
        Assert.Equal(4, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_gets_past_the_girders()
    {
        int cleared = 0;
        var losses = new List<string>();
        for (int seed = 1; seed <= 4; seed++)
        {
            var g = new GameEngine(seed, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(seed);
            for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.StageIndex == 0; i++) g.Step(pilot.Decide(g));
            if (g.StageIndex > 0) cleared++;
            losses.AddRange(g.LossLog);
        }
        Assert.True(cleared >= 2, $"{cleared} of 4 cleared: {string.Join("; ", losses)}");
    }

    [Fact]
    public void A_game_ends_in_the_high_score_table()
    {
        var g = new GameEngine(2, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials)
        {
            g.Initials!.Finish();
            g.Idle(2);
            Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
            Assert.Equal(g.Level * 100 + g.Metres, HighScoreTable.Load(_dir).Entries[0].Stage);
        }
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("L1  25 m", GameCanvas.StageName(125));
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Level} {g.StageIndex} {g.Score} {g.Lives} {g.Hero.X:0.###} {g.Barrels.Count} {g.Fireballs.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 8)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.HowHigh, heard);
        Assert.Contains(Sfx.MusicGirders, heard);
        Assert.Contains(Sfx.Throw, heard);
        Assert.Contains(Sfx.Walk, heard);
        g.Idle((int)(60 * GameEngine.AttractSeconds));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.AmbienceStop, heard.Last(s => s.ToString().StartsWith("Music") || s == Sfx.AmbienceStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_stops_the_music()
    {
        var g = Quiet();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Steps(g, 2);
        g.Pause();
        Assert.Equal(Sfx.AmbienceStop, heard[^1]);
        double x = g.Hero.X;
        Steps(g, 30, Right);
        Assert.Equal(x, g.Hero.X);
        g.Resume();
        Steps(g, 2);
        Assert.Equal(Sfx.MusicGirders, heard.Last(s => s != Sfx.Walk));
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s != Sfx.AmbienceStop).Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.Equal((12, 16), (Sprites.HeroStand.Width, Sprites.HeroStand.Height));
        Assert.All(Sprites.HeroWalk.Concat(Sprites.HeroClimb).Append(Sprites.HeroJump), s => Assert.Equal((12, 16), (s.Width, s.Height)));
        Assert.Equal(Sprites.ApeIdle.Width, Sprites.ApeArmsUp.Width);
        Assert.Equal(4, Sprites.Barrel.Length);
        foreach (var s in new[] { Sprites.ApeIdle, Sprites.Drum, Sprites.Heart, Sprites.Tray })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
    }
}
