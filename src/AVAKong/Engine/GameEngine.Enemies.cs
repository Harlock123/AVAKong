using AVAKong.Models;

namespace AVAKong.Engine;

public sealed partial class GameEngine
{
    public const double MaxFallingSpeed = 220;

    public List<Barrel> Barrels { get; } = [];
    public List<Fireball> Fireballs { get; } = [];
    public List<Tray> Trays { get; } = [];
    public List<Jack> Jacks { get; } = [];

    /// <summary>Where the ape is (he paces the top of the belts stage).</summary>
    public Vec Ape { get; private set; }
    /// <summary>Seconds since the ape last threw a barrel (for drawing the throw).</summary>
    public double SinceThrow { get; private set; } = 99;
    public bool DrumLit { get; private set; }

    private double _throwTimer, _spawnTimer, _trayTimer, _jackTimer, _beltTimer, _poundTimer;
    private int _barrelsThrown;
    private int _apeDir = 1;
    private bool _trayLeft;

    public Box ApeBox => new(Ape.X - 18, Ape.Y - 30, Ape.X + 18, Ape.Y);

    private void ClearEnemies()
    {
        Barrels.Clear();
        Fireballs.Clear();
        Trays.Clear();
        Jacks.Clear();
        DrumLit = false;
    }

    private void ResetApe()
    {
        Ape = Stage.Ape;
        _throwTimer = 1.0;
        _spawnTimer = Stage.Kind == StageKind.Rivets ? 2 : 0.5;
        _trayTimer = 0.5;
        _jackTimer = 1.0;
        _beltTimer = 0;
        _poundTimer = 3;
        _barrelsThrown = 0;
        SinceThrow = 99;
        if (Stage.Kind == StageKind.Elevators)
        {
            // Two fireballs already out on the islands.
            Fireballs.Add(new Fireball { X = 124, Y = 176, On = Support(124, 176, 2), Dir = -1 });
            Fireballs.Add(new Fireball { X = 200, Y = 108, On = Support(200, 108, 2), Dir = 1 });
        }
        if (Stage.Kind == StageKind.Conveyors) DrumLit = true;
    }

    /// <summary>The level's pace: barrels and fireballs quicken as the levels go on.</summary>
    private double Pace => 1 + 0.08 * Math.Min(Level - 1, 6);

    private void MoveWorld(double dt)
    {
        // Lifts ride round their shafts.
        foreach (var shaft in Stage.Shafts)
            foreach (var lift in shaft.Lifts)
            {
                double y = lift.Y0 + shaft.Speed * dt;
                if (shaft.Speed < 0 && y < shaft.Top) y += shaft.Bottom - shaft.Top;
                if (shaft.Speed > 0 && y > shaft.Bottom) y -= shaft.Bottom - shaft.Top;
                lift.Y0 = lift.Y1 = y;
            }
        // The full-width belts change direction now and then.
        if (Stage.Kind == StageKind.Conveyors)
        {
            _beltTimer += dt;
            if (_beltTimer >= 6.5)
            {
                _beltTimer = 0;
                foreach (var p in Stage.Platforms)
                    if (p.Belt != 0 && p.X0 == 0 && p.X1 == Field.Width) p.Belt = -p.Belt;
            }
        }
    }

    private void MoveEnemies(double dt)
    {
        SinceThrow += dt;
        _poundTimer -= dt;
        if (_poundTimer <= 0)
        {
            _poundTimer = 4 + _rng.NextDouble() * 3;
            if (Stage.Kind != StageKind.Girders) Play(Sfx.Pound);
        }
        switch (Stage.Kind)
        {
            case StageKind.Girders: ThrowBarrels(dt); break;
            case StageKind.Conveyors: PaceApe(dt); FeedTrays(dt); SpawnFireballs(dt, fromDrum: true); break;
            case StageKind.Elevators: ThrowJacks(dt); break;
            case StageKind.Rivets: SpawnFireballs(dt, fromDrum: false); break;
        }
        foreach (var b in Barrels) MoveBarrel(b, dt);
        foreach (var f in Fireballs) MoveFireball(f, dt);
        foreach (var t in Trays)
        {
            t.X += t.On.Belt * dt;
            if (!t.On.Covers(t.X)) t.Dead = true;
        }
        foreach (var j in Jacks) MoveJack(j, dt);
        Barrels.RemoveAll(b => b.Dead);
        Fireballs.RemoveAll(f => f.Dead);
        Trays.RemoveAll(t => t.Dead);
        Jacks.RemoveAll(j => j.Dead);
    }

    // ----- barrels ------------------------------------------------------------------------------

    private void ThrowBarrels(double dt)
    {
        _throwTimer -= dt;
        if (_throwTimer > 0 || Barrels.Count >= 7) return;
        _throwTimer = Math.Max(1.2, 2.7 - 0.25 * (Level - 1)) * (0.8 + _rng.NextDouble() * 0.5);
        bool first = _barrelsThrown == 0;
        _barrelsThrown++;
        var on = Support(Ape.X + 24, Ape.Y, 2);
        Barrels.Add(new Barrel
        {
            X = Ape.X + 24, Y = Ape.Y, On = on, Dir = 1,
            Straight = first, Blue = first || _rng.Next(7) == 0,
        });
        SinceThrow = 0;
        Play(Sfx.Throw);
    }

    private void MoveBarrel(Barrel b, double dt)
    {
        double speed = 62 * Pace;
        switch (b.State)
        {
            case BarrelState.Rolling:
                var p = b.On;
                if (p is null || p.Removed) { b.State = BarrelState.Falling; b.Vy = 0; break; }
                if (p.Downhill != 0) b.Dir = p.Downhill;
                b.X += b.Dir * speed * dt;
                b.Spin += speed * dt;
                foreach (var l in Stage.Ladders)
                {
                    if (l.Upper != p || Math.Abs(b.X - l.X) > 2 || b.Considered == l) continue;
                    b.Considered = l;
                    var h = Hero;
                    double chance = b.Straight ? 1 : 0.22 + 0.04 * Math.Min(Level, 5);
                    if (h.Feet > l.Top + 4 && Math.Abs(h.X - l.X) < 60) chance += 0.2;
                    if (_rng.NextDouble() < chance)
                    {
                        b.State = BarrelState.Dropping;
                        b.Ladder = l;
                        b.X = l.X;
                        return;
                    }
                }
                if (!p.Covers(b.X))
                {
                    var next = Support(b.X, b.Y, 3);
                    if (next is null) { b.State = BarrelState.Falling; b.Vy = 0; b.On = null; break; }
                    b.On = next;
                }
                b.Y = b.On!.YAt(b.X);
                if (Stage.Drum is { } drum && Math.Abs(b.X - drum.X) < 6 && Math.Abs(b.Y - drum.Y) < 4)
                {
                    b.Dead = true;
                    if (b.Blue || b.Straight) LightFireball(drum);
                }
                break;
            case BarrelState.Dropping:
                var l2 = b.Ladder!;
                b.Y += 52 * Pace * dt;
                if (b.Y >= l2.Bottom)
                {
                    b.Y = l2.Bottom;
                    b.On = l2.Lower;
                    b.Considered = l2;
                    b.State = BarrelState.Rolling;
                    if (b.On.Downhill == 0) b.Dir = -b.Dir;
                }
                break;
            case BarrelState.Falling:
                double before = b.Y;
                b.Vy = Math.Min(MaxFallingSpeed, b.Vy + Gravity * dt);
                b.Y += b.Vy * dt;
                b.X += b.Dir * 26 * dt;
                foreach (var q in Stage.Platforms)
                {
                    if (q.Removed || !q.Covers(b.X)) continue;
                    double y = q.YAt(b.X);
                    if (before <= y + 0.5 && b.Y >= y)
                    {
                        b.Y = y;
                        b.On = q;
                        b.State = BarrelState.Rolling;
                        break;
                    }
                }
                if (b.Y > Field.Height + 20) b.Dead = true;
                break;
        }
    }

    // ----- fireballs ----------------------------------------------------------------------------

    private int MaxFireballs => Stage.Kind switch
    {
        StageKind.Girders => Math.Min(5, 1 + Level),
        StageKind.Rivets => Math.Min(5, 1 + Level),
        _ => Math.Min(5, 2 + Level),
    };

    private void LightFireball(Vec drum)
    {
        DrumLit = true;
        if (Fireballs.Count >= MaxFireballs) return;
        Fireballs.Add(new Fireball { X = drum.X, Y = drum.Y - 16, Airborne = true, Vy = -70, Dir = 1 });
    }

    private void SpawnFireballs(double dt, bool fromDrum)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0 || Fireballs.Count >= MaxFireballs) return;
        _spawnTimer = fromDrum ? 9 : 5;
        if (fromDrum && Stage.Drum is { } d)
        {
            Fireballs.Add(new Fireball { X = d.X, Y = d.Y - 16, Airborne = true, Vy = -80, Dir = _rng.Next(2) == 0 ? -1 : 1 });
            return;
        }
        // On the rivets they appear at the end of a floor, away from the hero.
        var ends = Stage.Platforms.Where(p => !p.Plug && !p.Removed && p.Y0 < 240 && p.Y0 > 100 && p != Stage.Goal)
            .SelectMany(p => new[] { (X: p.X0 + 4, Y: p.Y0, Dir: 1), (X: p.X1 - 4, Y: p.Y0, Dir: -1) })
            .Where(e => e.X < 30 || e.X > Field.Width - 30)
            .Where(e => Math.Abs(e.X - Hero.X) + Math.Abs(e.Y - Hero.Feet) > 70)
            .ToList();
        if (ends.Count == 0) return;
        var (x, y, dir) = ends[_rng.Next(ends.Count)];
        Fireballs.Add(new Fireball { X = x, Y = y - 20, Airborne = true, Vy = -40, Dir = dir });
    }

    private void MoveFireball(Fireball f, double dt)
    {
        double speed = 22 * Pace;
        if (f.Airborne)
        {
            double before = f.Y;
            f.Vy += Gravity * 0.6 * dt;
            f.Y += f.Vy * dt;
            f.X = Math.Clamp(f.X + f.Dir * 24 * dt, 4, Field.Width - 4);
            if (f.Vy > 0)
                foreach (var q in Stage.Platforms)
                {
                    if (q.Removed || q.Lift || !q.Covers(f.X)) continue;
                    double y = q.YAt(f.X);
                    if (before <= y + 0.5 && f.Y >= y) { f.Y = y; f.On = q; f.Airborne = false; break; }
                }
            if (f.Y > Field.Height + 20) f.Dead = true;
            return;
        }
        if (f.Ladder is { } l)
        {
            f.Y += f.ClimbDir * 18 * Pace * dt;
            if (f.Y <= l.Top) { f.Y = l.Top; f.On = l.Upper; f.Ladder = null; f.Considered = l; }
            else if (f.Y >= l.Bottom) { f.Y = l.Bottom; f.On = l.Lower; f.Ladder = null; f.Considered = l; }
            return;
        }
        var on = f.On;
        if (on is null || on.Removed) { f.Airborne = true; f.Vy = 0; return; }

        f.Think -= dt;
        if (f.Think <= 0)
        {
            f.Think = 0.7 + _rng.NextDouble();
            bool sameLevel = Math.Abs(Hero.Feet - f.Y) < 12;
            f.Dir = sameLevel && _rng.NextDouble() < 0.7 ? Math.Sign(Hero.X - f.X) is var s && s != 0 ? s : f.Dir
                : _rng.Next(2) == 0 ? -1 : 1;
        }
        double nx = f.X + f.Dir * speed * dt;
        // Walk across plugs and joins, but turn back at a gap or a drop.
        var ahead = Support(nx + f.Dir * 4, f.Y, 3);
        if (ahead is null || ahead.Lift) { f.Dir = -f.Dir; return; }
        f.X = nx;
        var under = Support(f.X, f.Y, 3);
        if (under is not null) { f.On = under; f.Y = under.YAt(f.X); }

        foreach (var lad in Stage.Ladders)
        {
            if (lad.Broken || Math.Abs(f.X - lad.X) > 1 || f.Considered == lad) continue;
            bool up = lad.Lower == f.On || Math.Abs(lad.Bottom - f.Y) < 2;
            bool down = lad.Upper == f.On || Math.Abs(lad.Top - f.Y) < 2;
            if (!up && !down) continue;
            f.Considered = lad;
            // More likely to climb towards the hero.
            bool towards = up ? Hero.Feet < f.Y - 8 : Hero.Feet > f.Y + 8;
            if (_rng.NextDouble() < (towards ? 0.55 : 0.2))
            {
                f.Ladder = lad;
                f.X = lad.X;
                f.ClimbDir = up ? -1 : 1;
                return;
            }
        }
        if (f.Considered is { } c && Math.Abs(f.X - c.X) > 6) f.Considered = null;
    }

    // ----- the belts stage ----------------------------------------------------------------------

    private void PaceApe(double dt)
    {
        double x = Ape.X + _apeDir * 18 * dt;
        if (x < 64 || x > 160 || _rng.NextDouble() < 0.004) _apeDir = -_apeDir;
        Ape = Ape with { X = Math.Clamp(x, 64, 160) };
    }

    private void FeedTrays(double dt)
    {
        _trayTimer -= dt;
        if (_trayTimer > 0 || Trays.Count >= 8) return;
        _trayTimer = Math.Max(1.2, 2.4 - 0.15 * Level);
        var belts = Stage.Platforms.Where(p => p.Belt != 0).ToList();
        _trayLeft = !_trayLeft;
        var belt = belts[_rng.Next(belts.Count)];
        double x = belt.Belt > 0 ? belt.X0 + 7 : belt.X1 - 7;
        // Never right on top of the hero.
        if (Math.Abs(x - Hero.X) < 24 && Math.Abs(belt.YAt(x) - Hero.Feet) < 4) return;
        Trays.Add(new Tray { X = x, On = belt });
    }

    // ----- the lifts stage ----------------------------------------------------------------------

    private void ThrowJacks(double dt)
    {
        _jackTimer -= dt;
        if (_jackTimer > 0) return;
        _jackTimer = Math.Max(1.2, 2.0 - 0.1 * Level) * (0.8 + _rng.NextDouble() * 0.5);
        Jacks.Add(new Jack { X = Ape.X + 24, Y = Ape.Y });
    }

    private void MoveJack(Jack j, double dt)
    {
        if (j.Dropping)
        {
            j.Y += 130 * dt;
            if (j.Y > Field.Height + 20) j.Dead = true;
            return;
        }
        double before = j.Hop;
        j.X += 62 * dt;
        j.Hop = (j.X - Ape.X - 24) % 36 / 36;
        if (j.Hop < before) Play(Sfx.Bounce);
        j.Y = Ape.Y - 16 * Math.Sin(Math.PI * j.Hop);
        if (j.X >= 206)
        {
            j.X = 206;
            j.Dropping = true;
        }
    }

    // ----- for the tests ------------------------------------------------------------------------

    internal Barrel PutBarrel(double x, double y, int dir)
    {
        var b = new Barrel { X = x, Y = y, Dir = dir, On = Support(x, y, 8) };
        if (b.On is not null) b.Y = b.On.YAt(x);
        Barrels.Add(b);
        return b;
    }

    internal Fireball PutFireball(double x, double y, int dir)
    {
        var f = new Fireball { X = x, Y = y, Dir = dir, On = Support(x, y, 8), Think = 99 };
        if (f.On is not null) f.Y = f.On.YAt(x);
        Fireballs.Add(f);
        return f;
    }

    internal Tray PutTray(double x, Platform on)
    {
        var t = new Tray { X = x, On = on };
        Trays.Add(t);
        return t;
    }

    /// <summary>Stops the ape throwing and everything else arriving.</summary>
    internal void Quiet()
    {
        _throwTimer = _spawnTimer = _trayTimer = _jackTimer = double.MaxValue;
        ClearEnemies();
    }
}
