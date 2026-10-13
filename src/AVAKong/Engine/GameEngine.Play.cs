using AVAKong.Models;

namespace AVAKong.Engine;

public sealed partial class GameEngine
{
    public const double WalkSpeed = 46;
    public const double ClimbSpeed = 30;
    public const double JumpSpeed = 140;
    public const double Gravity = 520;
    /// <summary>Land more than this far below where a jump or fall began and the hero doesn't get up.</summary>
    public const double LethalDrop = 20;
    public const double HammerSeconds = 10;
    public const double IntroSeconds = 2.6;
    public const double RestartSeconds = 1.4;
    public const double DyingSeconds = 2.8;
    public const double StageClearSeconds = 4;
    public const double LevelClearSeconds = 6;
    /// <summary>The bonus drops by 100 this often.</summary>
    public const double BonusTick = 2;

    public Hero Hero { get; } = new();
    /// <summary>The bonus on offer for finishing the stage; at nothing, time's up.</summary>
    public int Bonus { get; private set; }
    public double HammerLeft { get; private set; }
    /// <summary>The intro is the "how high" screen (not a quick restart after losing a life).</summary>
    public bool ShowHowHigh { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _introFor = IntroSeconds;
    private double _bonusClock;
    private double _stepSound;
    private readonly HashSet<object> _jumpedOver = [];
    private Rivet? _onRivet;

    public static int BonusStart(int level) => Math.Min(8000, 4000 + 1000 * level);
    public static int ItemPoints(int level) => level switch { 1 => 300, 2 => 500, _ => 800 };

    // ----- stages -------------------------------------------------------------------------------

    private void StartStage(int index)
    {
        StageIndex = index;
        Stage = Stage.Build(Stage.Sequence(Level)[index]);
        ResetStage();
        ShowHowHigh = true;
        _introFor = IntroSeconds;
        SetPhase(Phase.Intro);
        Play(Sfx.HowHigh);
    }

    /// <summary>After a lost life: the same stage again, with what was pulled and picked up still gone.</summary>
    private void RestartStage()
    {
        var old = Stage;
        Stage = Stage.Build(old.Kind);
        for (int i = 0; i < old.Rivets.Count; i++) Stage.Rivets[i].Plug.Removed = old.Rivets[i].Pulled;
        for (int i = 0; i < old.Pickups.Count; i++) Stage.Pickups[i].Taken = old.Pickups[i].Taken && !old.Pickups[i].IsHammer;
        ResetStage();
        ShowHowHigh = false;
        _introFor = RestartSeconds;
        SetPhase(Phase.Intro);
    }

    private void ResetStage()
    {
        ClearEnemies();
        PlaceHero();
        Bonus = BonusStart(Level);
        _bonusClock = 0;
        HammerLeft = 0;
        LastDeath = null;
        _onRivet = null;
        ResetApe();
    }

    private void PlaceHero()
    {
        var h = Hero;
        h.X = Stage.Start.X;
        h.Feet = Stage.Start.Y;
        h.State = HeroState.Walking;
        h.Facing = 1;
        h.Vx = h.Vy = 0;
        h.Ladder = null;
        h.On = Support(h.X, h.Feet, 2);
        h.Stride = 0;
        h.Moving = false;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Intro:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime >= _introFor)
                {
                    SetPhase(Phase.Playing);
                    Ambience(StageMusic);
                }
                return;
            case Phase.Dying:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) GameOver();
                else RestartStage();
                return;
            case Phase.StageClear:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime >= StageClearSeconds) StartStage(Math.Min(StageIndex + 1, Stage.Sequence(Level).Length - 1));
                return;
            case Phase.LevelClear:
                Ambience(Sfx.AmbienceStop);
                if (PhaseTime < LevelClearSeconds) return;
                Level++;
                StartStage(0);
                return;
        }

        MoveWorld(dt);
        MoveHero(dt, c);
        if (Phase != Phase.Playing) return;
        MoveEnemies(dt);
        Pickups();
        Rivets();
        if (Phase != Phase.Playing) return;
        Smash();
        Collide();
        if (Phase != Phase.Playing) return;

        if (HammerLeft > 0)
        {
            HammerLeft = Math.Max(0, HammerLeft - dt);
        }
        Ambience(HammerLeft > 0 ? Sfx.MusicHammer : StageMusic);

        _bonusClock += dt;
        if (_bonusClock >= BonusTick)
        {
            _bonusClock -= BonusTick;
            Bonus = Math.Max(0, Bonus - 100);
            if (Bonus == 1000) Play(Sfx.TimeLow);
            if (Bonus == 0) { Die("ran out of time"); return; }
        }

        if (Stage.Kind != StageKind.Rivets && Hero.State == HeroState.Walking && Hero.On == Stage.Goal)
        {
            AddScore(Bonus);
            Popup(new Vec(Hero.X, Hero.Feet - 24), Bonus, 2.5);
            SetPhase(Phase.StageClear);
            Play(Sfx.StageClear);
        }
    }

    private Sfx StageMusic => Stage.Kind switch
    {
        StageKind.Girders => Sfx.MusicGirders,
        StageKind.Conveyors => Sfx.MusicConveyors,
        StageKind.Elevators => Sfx.MusicElevators,
        _ => Sfx.MusicRivets,
    };

    private void Die(string why)
    {
        LastDeath = why;
        LossLog.Add($"L{Level} {Stage.Kind}: {why} at ({Hero.X:0}, {Hero.Feet:0})");
        SetPhase(Phase.Dying);
        HammerLeft = 0;
        Play(Sfx.Death);
    }

    // ----- the hero -----------------------------------------------------------------------------

    /// <summary>The platform under a point, if there's one within <paramref name="tolerance"/> pixels.</summary>
    public Platform? Support(double x, double feet, double tolerance)
    {
        Platform? best = null;
        double bestGap = double.MaxValue;
        foreach (var p in Stage.Platforms)
        {
            if (p.Removed || !p.Covers(x)) continue;
            double gap = Math.Abs(p.YAt(x) - feet);
            if (gap <= tolerance && gap < bestGap) { best = p; bestGap = gap; }
        }
        return best;
    }

    private void MoveHero(double dt, Controls c)
    {
        var h = Hero;
        h.Moving = false;
        switch (h.State)
        {
            case HeroState.Climbing: Climb(dt, c); break;
            case HeroState.Walking: Walk(dt, c); break;
            default: Fly(dt); break;
        }
        if (h.Feet > Field.Height + 30) Die("fell");
    }

    private void Walk(double dt, Controls c)
    {
        var h = Hero;
        var p = h.On;
        if (p is null || p.Removed) { StartFall(); return; }
        h.Feet = p.YAt(h.X);

        // The lifts carry the hero into the machinery at the top of the shaft.
        if (p.Lift && Stage.Shafts.FirstOrDefault(s => s.Lifts.Contains(p)) is { } shaft && shaft.Speed < 0 && p.Y0 < shaft.Top + 10)
        {
            Die("carried into the lift's machinery");
            return;
        }

        if (HammerLeft <= 0)
        {
            if (c.Dy != 0 && LadderFor(c.Dy) is { } ladder)
            {
                h.State = HeroState.Climbing;
                h.Ladder = ladder;
                h.X = ladder.X;
                h.On = null;
                return;
            }
            if (c.Jump)
            {
                h.State = HeroState.Jumping;
                h.Vx = c.Dx * WalkSpeed;
                h.Vy = -JumpSpeed;
                h.FromY = h.Feet;
                if (c.Dx != 0) h.Facing = c.Dx;
                h.On = null;
                _jumpedOver.Clear();
                Play(Sfx.Jump);
                return;
            }
        }

        if (c.Dx != 0) h.Facing = c.Dx;
        double vx = c.Dx * WalkSpeed + p.Belt;
        h.X = Math.Clamp(h.X + vx * dt, Hero.Width / 2, Field.Width - Hero.Width / 2);
        if (c.Dx != 0)
        {
            h.Moving = true;
            h.Stride += WalkSpeed * dt;
            _stepSound += WalkSpeed * dt;
            if (_stepSound >= 9) { _stepSound = 0; Play(Sfx.Walk); }
        }
        if (!p.Covers(h.X) || p.Removed)
        {
            var next = Support(h.X, h.Feet, 3);
            if (next is null) { StartFall(); return; }
            h.On = next;
        }
        h.Feet = h.On!.YAt(h.X);
    }

    /// <summary>A ladder the hero can get on here: up from its foot, or down from its top.</summary>
    private Ladder? LadderFor(int dy)
    {
        var h = Hero;
        foreach (var l in Stage.Ladders)
        {
            if (l.Broken || Math.Abs(h.X - l.X) > 4) continue;
            if (dy < 0 && Math.Abs(h.Feet - l.Bottom) <= 3) return l;
            if (dy > 0 && Math.Abs(h.Feet - l.Top) <= 3) return l;
        }
        return null;
    }

    private void Climb(double dt, Controls c)
    {
        var h = Hero;
        var l = h.Ladder!;
        if (c.Dy == 0) return;
        h.Moving = true;
        h.Stride += ClimbSpeed * dt;
        h.Feet += c.Dy * ClimbSpeed * dt;
        if (h.Feet <= l.Top)
        {
            h.Feet = l.Top;
            h.On = l.Upper;
            h.State = HeroState.Walking;
            h.Ladder = null;
        }
        else if (h.Feet >= l.Bottom)
        {
            h.Feet = l.Bottom;
            h.On = l.Lower;
            h.State = HeroState.Walking;
            h.Ladder = null;
        }
    }

    private void StartFall()
    {
        var h = Hero;
        h.State = HeroState.Falling;
        h.Vx = 0;
        h.Vy = 0;
        h.FromY = h.Feet;
        h.On = null;
    }

    private void Fly(double dt)
    {
        var h = Hero;
        double before = h.Feet;
        h.Vy += Gravity * dt;
        h.X += h.Vx * dt;
        if (h.X < Hero.Width / 2 || h.X > Field.Width - Hero.Width / 2)
        {
            h.X = Math.Clamp(h.X, Hero.Width / 2, Field.Width - Hero.Width / 2);
            h.Vx = 0;
        }
        h.Feet += h.Vy * dt;

        if (h.State == HeroState.Jumping)
            foreach (var (thing, box) in Dangers())
                if (Math.Abs((box.Left + box.Right) / 2 - h.X) < 7 && box.Bottom >= h.Feet && box.Bottom - h.Feet < 30)
                    _jumpedOver.Add(thing);

        if (h.Vy <= 0) return;
        Platform? landing = null;
        double landY = double.MaxValue;
        foreach (var p in Stage.Platforms)
        {
            if (p.Removed || !p.Covers(h.X)) continue;
            double y = p.YAt(h.X);
            if (before <= y + 0.5 && h.Feet >= y && y < landY) { landing = p; landY = y; }
        }
        if (landing is null) return;
        h.Feet = landY;
        h.On = landing;
        bool jumped = h.State == HeroState.Jumping;
        h.State = HeroState.Walking;
        h.Vx = h.Vy = 0;
        if (landY - h.FromY > LethalDrop)
        {
            Die("fell too far");
            return;
        }
        if (jumped && _jumpedOver.Count > 0)
        {
            int points = _jumpedOver.Count switch { 1 => 100, 2 => 300, _ => 800 };
            AddScore(points);
            Popup(new Vec(h.X, h.Feet - 20), points);
            Play(Sfx.JumpOver);
        }
        _jumpedOver.Clear();
    }

    // ----- picking things up, pulling rivets, the hammer, getting hit ---------------------------

    private void Pickups()
    {
        var hb = Hero.Box;
        foreach (var p in Stage.Pickups)
        {
            if (p.Taken) continue;
            var box = p.IsHammer ? new Box(p.Pos.X - 4, p.Pos.Y - 5, p.Pos.X + 4, p.Pos.Y + 5) : new Box(p.Pos.X - 6, p.Pos.Y - 6, p.Pos.X + 6, p.Pos.Y + 6);
            if (!hb.Overlaps(box)) continue;
            if (p.IsHammer)
            {
                if (Hero.State == HeroState.Climbing) continue;
                p.Taken = true;
                HammerLeft = HammerSeconds;
            }
            else
            {
                p.Taken = true;
                int points = ItemPoints(Level);
                AddScore(points);
                Popup(p.Pos, points);
                Play(Sfx.Item);
            }
        }
    }

    /// <summary>A rivet comes out once the hero has gone right across it (so the gap opens behind them).</summary>
    private void Rivets()
    {
        if (Stage.Kind != StageKind.Rivets) return;
        var h = Hero;
        foreach (var r in Stage.Rivets)
        {
            if (r.Pulled) continue;
            double plugY = r.Plug.Y0;
            bool atFloor = h.Feet <= plugY + 1 && plugY - h.Feet < 26 && h.State != HeroState.Climbing;
            bool over = Math.Abs(h.X - r.X) <= 4;
            if (over && atFloor) { _onRivet = r; continue; }
            if (_onRivet == r && !over)
            {
                _onRivet = null;
                if (!atFloor) continue;
                r.Plug.Removed = true;
                AddScore(100);
                Popup(new Vec(r.X, plugY - 16), 100);
                Play(Sfx.RivetPop);
            }
        }
        if (Stage.Rivets.All(r => r.Pulled))
        {
            AddScore(Bonus);
            Popup(new Vec(112, 140), Bonus, 3);
            SetPhase(Phase.LevelClear);
            Play(Sfx.LevelClear);
        }
    }

    /// <summary>The hammer's reach: in front of the hero, from the boots to above the head.</summary>
    public Box HammerBox => Hero.Facing > 0
        ? new Box(Hero.X, Hero.Feet - 28, Hero.X + 17, Hero.Feet)
        : new Box(Hero.X - 17, Hero.Feet - 28, Hero.X, Hero.Feet);

    private void Smash()
    {
        if (HammerLeft <= 0) return;
        var reach = HammerBox;
        foreach (var b in Barrels)
            if (!b.Dead && b.Box.Overlaps(reach)) { b.Dead = true; Smashed(b.Box, _rng.Next(4) == 0 ? 800 : 300); }
        foreach (var f in Fireballs)
            if (!f.Dead && f.Box.Overlaps(reach)) { f.Dead = true; Smashed(f.Box, 500); }
        foreach (var t in Trays)
            if (!t.Dead && t.Box.Overlaps(reach)) { t.Dead = true; Smashed(t.Box, 300); }
    }

    private void Smashed(Box at, int points)
    {
        var centre = new Vec((at.Left + at.Right) / 2, (at.Top + at.Bottom) / 2);
        Effects.Add(new Effect { Kind = EffectKind.Smash, Pos = centre, Life = 0.4 });
        AddScore(points);
        Popup(centre, points);
        Play(Sfx.Smash);
    }

    /// <summary>Everything that kills on touch, with its box.</summary>
    private IEnumerable<(object Thing, Box Box)> Dangers()
    {
        foreach (var b in Barrels) if (!b.Dead) yield return (b, b.Box);
        foreach (var f in Fireballs) if (!f.Dead) yield return (f, f.Box);
        foreach (var t in Trays) if (!t.Dead) yield return (t, t.Box);
        foreach (var j in Jacks) if (!j.Dead) yield return (j, j.Box);
    }

    private void Collide()
    {
        var hb = Hero.Box.Shrink(2);
        foreach (var (thing, box) in Dangers())
            if (box.Shrink(1).Overlaps(hb))
            {
                Die($"hit by a {thing.GetType().Name.ToLowerInvariant()}");
                return;
            }
        if (ApeBox.Overlaps(hb)) { Die("touched the ape"); return; }
        if (Stage.Kind == StageKind.Conveyors && Stage.Drum is { } d && new Box(d.X - 7, d.Y - 18, d.X + 7, d.Y).Overlaps(hb))
            Die("burned");
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    /// <summary>Skips the intro.</summary>
    internal void Go()
    {
        if (Phase == Phase.Intro)
        {
            SetPhase(Phase.Playing);
            Ambience(StageMusic);
        }
    }

    internal void JumpTo(int level, int stageIndex)
    {
        Level = level;
        StartStage(stageIndex);
        Go();
    }

    internal void PutHero(double x, double feet)
    {
        PlaceHero();
        Hero.X = x;
        Hero.Feet = feet;
        Hero.On = Support(x, feet, 8);
        if (Hero.On is not null) Hero.Feet = Hero.On.YAt(x);
    }

    internal void SetBonus(int bonus) => Bonus = bonus;
}
