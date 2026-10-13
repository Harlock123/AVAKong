using AVAKong.Models;

namespace AVAKong.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It plans a route of platforms and ladders to
/// the captive (or, on the rivets, to the nearest rivet left), jumps whatever comes at it along the
/// way, hops the gaps where rivets were, and waits at a ladder while a barrel is coming down it.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);

    /// <summary>One way on: a ladder to climb, or a join to walk (or jump) across to the next platform.</summary>
    private sealed record Hop(Platform To, Ladder? Ladder, double X);

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        var h = g.Hero;

        if (h.State == HeroState.Climbing)
        {
            var l = h.Ladder!;
            // Keep going the way the plan says (up unless the plan was down).
            int dy = _climb != 0 ? _climb : -1;
            // Near the top, wait if something is rolling past.
            if (dy < 0 && h.Feet - l.Top < 20 && Threat(g, l.X, l.Top, 26)) return default;
            return new Controls(0, dy, false);
        }
        _climb = 0;
        if (h.State != HeroState.Walking || h.On is null) return default;

        // Something coming along the platform: jump it (towards a slow one, so it passes under quickly).
        if (Incoming(g) is { } jumpDx) return new Controls(jumpDx, 0, true);
        // A barrel coming down a ladder right above: get out from under it.
        foreach (var b in g.Barrels)
            if (b.State == BarrelState.Dropping && Math.Abs(b.X - h.X) < 12 && b.Y < h.Feet)
                return Toward(h.X, h.X + (h.X < b.X ? -20 : 20), gapCheck: g);

        var (targetPlatform, targetX) = Target(g);
        if (targetPlatform is null) return new Controls(_rng.Next(3) - 1, 0, false);
        var route = Route(g, h.On, targetPlatform);
        if (route is null) return default;
        if (route.Count == 0) return Toward(h.X, targetX, gapCheck: g);

        var next = route[0];
        if (next.Ladder is { } ladder)
        {
            if (Math.Abs(h.X - ladder.X) > 1.5)
            {
                // Only walk under it when it's safe to go straight up.
                bool up = ladder.Lower == h.On || Math.Abs(h.Feet - ladder.Bottom) < 3;
                if (up && Math.Abs(h.X - ladder.X) < 18 && !SafeToClimb(g, ladder)) return default;
                return Toward(h.X, ladder.X, gapCheck: g);
            }
            int dy = ladder.Lower == h.On || Math.Abs(h.Feet - ladder.Bottom) < 3 ? -1 : 1;
            // Don't start up a ladder a barrel is coming down, or might reach before we're at the top:
            // wait beside it, not under it.
            if (dy < 0 && !SafeToClimb(g, ladder))
            {
                double aside = ladder.X + (ladder.X > 112 ? -16 : 16);
                return Math.Abs(h.X - aside) > 2 && g.Support(aside, h.Feet, 3) == h.On ? Toward(h.X, aside, gapCheck: g) : default;
            }
            _climb = dy;
            return new Controls(0, dy, false);
        }
        return Toward(h.X, next.X, gapCheck: g);
    }

    private int _climb;

    /// <summary>Walk towards x; jump a gap in the floor if one opens just ahead.</summary>
    private static Controls Toward(double x, double target, GameEngine gapCheck)
    {
        int dx = target > x + 1 ? 1 : target < x - 1 ? -1 : 0;
        if (dx == 0) return default;
        var h = gapCheck.Hero;
        if (gapCheck.Support(h.X + dx * 5, h.Feet, 3) is null && gapCheck.Support(h.X + dx * 16, h.Feet, 3) is not null)
            return new Controls(dx, 0, true);
        return new Controls(dx, 0, false);
    }

    /// <summary>Where to head: the captive's platform, or on the rivets the far side of the nearest rivet.</summary>
    private static (Platform? Platform, double X) Target(GameEngine g)
    {
        var s = g.Stage;
        if (s.Kind != StageKind.Rivets) return (s.Goal, s.Goal.X0 + 10);
        var h = g.Hero;
        var rivet = s.Rivets.Where(r => !r.Pulled)
            .OrderBy(r => Math.Abs(r.Plug.Y0 - h.Feet) * 3 + Math.Abs(r.X - h.X)).FirstOrDefault();
        if (rivet is null) return (null, 0);
        bool onFloor = Math.Abs(rivet.Plug.Y0 - h.Feet) < 2;
        double past = onFloor ? (h.X < rivet.X ? rivet.X + 9 : rivet.X - 9) : rivet.X + 9;
        var floor = g.Support(past, rivet.Plug.Y0, 2) ?? rivet.Plug;
        return (floor, past);
    }

    /// <summary>
    /// Whether to jump now, and which way (0: straight up). The jump clears a barrel's height for
    /// about the middle 0.4 s of its flight, so it goes when the barrel will pass under in that time.
    /// </summary>
    private static int? Incoming(GameEngine g)
    {
        var h = g.Hero;
        // Starts the jump when something closing at this speed is within this gap.
        static bool Window(double gap, double closing) => gap > 7 + closing * 0.06 && gap < 7 + closing * 0.24;
        double barrelSpeed = 62 * (1 + 0.08 * Math.Min(g.Level - 1, 6));
        foreach (var b in g.Barrels)
        {
            if (b.State == BarrelState.Falling)
            {
                // About to land just behind us and roll our way.
                double above = h.Feet - b.Y;
                double gap = (h.X - b.X) * b.Dir;
                if (above > 0 && above < 16 && gap > 4 && gap < 22) return 0;
                continue;
            }
            if (b.State != BarrelState.Rolling || Math.Abs(b.Y - h.Feet) > 6) continue;
            if (Window((h.X - b.X) * b.Dir, barrelSpeed)) return 0;
        }
        foreach (var f in g.Fireballs)
        {
            if (f.Airborne || f.Ladder is not null || Math.Abs(f.Y - h.Feet) > 6) continue;
            double gap = Math.Abs(h.X - f.X);
            int towards = Math.Sign(f.X - h.X);
            // Only if the whole landing zone is solid floor.
            if (new[] { 18, 22, 26, 30 }.Any(d => g.Support(h.X + towards * d, h.Feet, 3) is null)) continue;
            if (Window(gap, GameEngine.WalkSpeed + 22)) return towards;
        }
        foreach (var t in g.Trays)
        {
            if (Math.Abs(t.On.YAt(t.X) - h.Feet) > 4) continue;
            double closing = (t.On.Belt - (h.On?.Belt ?? 0)) * Math.Sign(h.X - t.X);
            if (closing > 0 && Window(Math.Abs(h.X - t.X), closing + 2)) return 0;
        }
        return null;
    }

    /// <summary>
    /// No barrel is coming down this ladder, or rolling towards its top close enough to take it while
    /// we climb, and nothing is waiting at the top.
    /// </summary>
    private static bool SafeToClimb(GameEngine g, Ladder ladder)
    {
        double climbTime = (ladder.Bottom - ladder.Top) / GameEngine.ClimbSpeed + 0.5;
        foreach (var b in g.Barrels)
        {
            if (b.State == BarrelState.Dropping && b.Ladder == ladder) return false;
            if (WillPass(g, b, ladder, climbTime)) return false;
        }
        return !Threat(g, ladder.X, ladder.Top, 30);
    }

    /// <summary>
    /// Plays a barrel forward (rolling downhill and dropping off ends, never taking a ladder) to see
    /// whether it comes past the top of this ladder, or down into the climb, within the time.
    /// </summary>
    private static bool WillPass(GameEngine g, Barrel b, Ladder ladder, double seconds)
    {
        const double dt = 1.0 / 30;
        double x = b.X, y = b.Y, vy = b.Vy, speed = 62 * 1.3;
        int dir = b.Dir;
        var on = b.State == BarrelState.Rolling ? b.On : null;
        if (b.State == BarrelState.Dropping) { y = Math.Max(y, b.Ladder!.Bottom); on = b.Ladder.Lower; }
        for (double t = 0; t < seconds; t += dt)
        {
            if (on is not null)
            {
                if (on.Downhill != 0) dir = on.Downhill;
                x += dir * speed * dt;
                if (on == ladder.Upper && Math.Abs(x - ladder.X) < 12) return true;
                if (!on.Covers(x))
                {
                    var next = g.Support(x, y, 3);
                    if (next is null) { on = null; vy = 0; }
                    else on = next;
                }
                if (on is not null) y = on.YAt(x);
            }
            else
            {
                double before = y;
                vy = Math.Min(GameEngine.MaxFallingSpeed, vy + GameEngine.Gravity * dt);
                y += vy * dt;
                x += dir * 26 * dt;
                if (Math.Abs(x - ladder.X) < 12 && y > ladder.Top - 4 && y < ladder.Bottom) return true;
                foreach (var q in g.Stage.Platforms)
                    if (!q.Removed && q.Covers(x) && before <= q.YAt(x) + 0.5 && y >= q.YAt(x)) { on = q; y = q.YAt(x); break; }
                if (y > Field.Height) return false;
            }
        }
        return false;
    }

    /// <summary>Something rolling or walking close to a point.</summary>
    private static bool Threat(GameEngine g, double x, double y, double within)
    {
        foreach (var b in g.Barrels)
            if (Math.Abs(b.Y - y) < 8 && Math.Abs(b.X - x) < within) return true;
        foreach (var f in g.Fireballs)
            if (Math.Abs(f.Y - y) < 8 && Math.Abs(f.X - x) < within * 0.7) return true;
        return false;
    }

    /// <summary>The fewest hops from one platform to another (empty: already there; null: no way).</summary>
    private static List<Hop>? Route(GameEngine g, Platform from, Platform to)
    {
        if (from == to) return [];
        var prev = new Dictionary<Platform, (Platform From, Hop Hop)>();
        var queue = new Queue<Platform>();
        queue.Enqueue(from);
        var seen = new HashSet<Platform> { from };
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var hop in Hops(g, p))
            {
                if (!seen.Add(hop.To)) continue;
                prev[hop.To] = (p, hop);
                if (hop.To == to)
                {
                    var path = new List<Hop>();
                    for (var at = to; at != from; at = prev[at].From) path.Insert(0, prev[at].Hop);
                    return path;
                }
                queue.Enqueue(hop.To);
            }
        }
        return null;
    }

    private static IEnumerable<Hop> Hops(GameEngine g, Platform p)
    {
        foreach (var l in g.Stage.Ladders)
        {
            if (l.Broken) continue;
            if (l.Lower == p) yield return new Hop(l.Upper, l, l.X);
            else if (l.Upper == p) yield return new Hop(l.Lower, l, l.X);
        }
        // Platforms that meet end to end (or across a rivet's gap) at the same height.
        foreach (var q in g.Stage.Platforms)
        {
            if (q == p || q.Removed || q.Lift || p.Lift) continue;
            if (Math.Abs(q.X0 - p.X1) <= 10 && Math.Abs(q.YAt(q.X0) - p.YAt(p.X1)) <= 3) yield return new Hop(q, null, q.X0 + 3);
            if (Math.Abs(p.X0 - q.X1) <= 10 && Math.Abs(q.YAt(q.X1) - p.YAt(p.X0)) <= 3) yield return new Hop(q, null, q.X1 - 3);
        }
    }
}
