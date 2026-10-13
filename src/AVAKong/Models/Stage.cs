namespace AVAKong.Models;

public enum StageKind
{
    /// <summary>Sloping girders; the ape rolls barrels down them.</summary>
    Girders,
    /// <summary>Conveyor belts carrying trays of cement, and a fire in the middle.</summary>
    Conveyors,
    /// <summary>Two shafts of lifts, one going up and one down, and bouncing springs.</summary>
    Elevators,
    /// <summary>Pull all eight rivets and the ape's floor gives way.</summary>
    Rivets,
}

public enum ItemKind { Purse, Hat, Umbrella }

/// <summary>
/// A girder, belt, floor or lift: a straight line the feet stand on, from (X0, Y0) to (X1, Y1).
/// Girders slope; things roll down them towards the larger Y.
/// </summary>
public sealed class Platform
{
    public double X0 { get; init; }
    public double X1 { get; init; }
    public double Y0 { get; set; }
    public double Y1 { get; set; }
    /// <summary>How fast a conveyor carries what stands on it (pixels a second, + is right).</summary>
    public double Belt { get; set; }
    /// <summary>A lift: it moves, and it's drawn as a small platform.</summary>
    public bool Lift { get; init; }
    /// <summary>A rivet's plug: removed when the rivet is pulled.</summary>
    public bool Plug { get; init; }
    public bool Removed { get; set; }

    public double YAt(double x) => X1 == X0 ? Y0 : Y0 + (Y1 - Y0) * (x - X0) / (X1 - X0);
    public bool Covers(double x) => x >= X0 && x <= X1;
    /// <summary>Which way is downhill: +1 right, −1 left, 0 flat.</summary>
    public int Downhill => Y1 > Y0 + 0.01 ? 1 : Y0 > Y1 + 0.01 ? -1 : 0;
}

/// <summary>A ladder from a lower platform up to an upper one. Broken ladders can't be climbed.</summary>
public sealed class Ladder
{
    public required double X { get; init; }
    public required Platform Upper { get; init; }
    public required Platform Lower { get; init; }
    public bool Broken { get; init; }
    public double Top => Upper.YAt(X);
    public double Bottom => Lower.YAt(X);
}

/// <summary>A lift shaft: platforms that ride up (or down) and come round again.</summary>
public sealed class Shaft
{
    public required double X { get; init; }
    public required double Top { get; init; }
    public required double Bottom { get; init; }
    /// <summary>Pixels a second; − rises.</summary>
    public required double Speed { get; init; }
    public List<Platform> Lifts { get; } = [];
    public const double LiftWidth = 24;
}

public sealed record Rivet(double X, Platform Plug)
{
    public bool Pulled => Plug.Removed;
}

/// <summary>Something to pick up: a hammer, or a dropped purse, hat or umbrella.</summary>
public sealed class Pickup
{
    public required Vec Pos { get; init; }
    public ItemKind? Item { get; init; }
    public bool IsHammer => Item is null;
    public bool Taken { get; set; }
}

/// <summary>
/// A stage's layout: this game's own, though the four kinds follow the arcade's. Everything is in
/// game pixels; a platform's Y is where feet stand.
/// </summary>
public sealed class Stage
{
    public required StageKind Kind { get; init; }
    public List<Platform> Platforms { get; } = [];
    public List<Ladder> Ladders { get; } = [];
    public List<Shaft> Shafts { get; } = [];
    public List<Rivet> Rivets { get; } = [];
    public List<Pickup> Pickups { get; } = [];
    public required Vec Start { get; init; }
    /// <summary>Where the ape stands.</summary>
    public required Vec Ape { get; init; }
    /// <summary>Where the captive waits; reaching her platform clears the stage (not on the rivets).</summary>
    public required Vec Captive { get; init; }
    public required Platform Goal { get; init; }
    /// <summary>The burning drum fireballs come out of, if there is one.</summary>
    public Vec? Drum { get; init; }

    private Platform Add(double x0, double y0, double x1, double y1, double belt = 0)
    {
        var p = new Platform { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Belt = belt };
        Platforms.Add(p);
        return p;
    }

    private void Ladder(double x, Platform upper, Platform lower, bool broken = false)
    {
        if (!upper.Covers(x) || !lower.Covers(x) || lower.YAt(x) <= upper.YAt(x))
            throw new InvalidOperationException($"Ladder at {x} doesn't join its platforms");
        Ladders.Add(new Ladder { X = x, Upper = upper, Lower = lower, Broken = broken });
    }

    private void Hammer(double x, Platform on) => Pickups.Add(new Pickup { Pos = new Vec(x, on.YAt(x) - 22) });
    private void Item(ItemKind kind, double x, Platform on) => Pickups.Add(new Pickup { Pos = new Vec(x, on.YAt(x) - 7), Item = kind });

    public static Stage Build(StageKind kind) => kind switch
    {
        StageKind.Girders => Girders(),
        StageKind.Conveyors => Conveyors(),
        StageKind.Elevators => Elevators(),
        _ => RivetStage(),
    };

    /// <summary>Six sloping girders zig-zagging up to the ape; the drum at the bottom left.</summary>
    private static Stage Girders()
    {
        var top = new Platform { X0 = 88, X1 = 136, Y0 = 52, Y1 = 52 };
        var s = new Stage { Kind = StageKind.Girders, Start = new Vec(56, 248), Ape = new Vec(40, 84), Captive = new Vec(100, 52), Goal = top, Drum = new Vec(14, 248) };
        var g0a = s.Add(0, 248, 112, 248);
        var g0b = s.Add(112, 248, 224, 241);
        var g1 = s.Add(0, 213, 208, 220);
        var g2 = s.Add(16, 187, 224, 180);
        var g3 = s.Add(0, 147, 208, 154);
        var g4 = s.Add(16, 121, 224, 114);
        var g5a = s.Add(0, 84, 120, 84);
        var g5b = s.Add(120, 84, 208, 88);
        s.Platforms.Add(top);
        s.Ladder(184, g1, g0b);
        s.Ladder(80, g1, g0a, broken: true);
        s.Ladder(32, g2, g1);
        s.Ladder(96, g2, g1);
        s.Ladder(168, g2, g1, broken: true);
        s.Ladder(64, g3, g2);
        s.Ladder(112, g3, g2, broken: true);
        s.Ladder(184, g3, g2);
        s.Ladder(32, g4, g3);
        s.Ladder(88, g4, g3, broken: true);
        s.Ladder(136, g4, g3);
        s.Ladder(72, g5a, g4, broken: true);
        s.Ladder(184, g5b, g4);
        s.Ladder(128, top, g5b);
        s.Hammer(160, g2);
        s.Hammer(48, g4);
        s.Item(ItemKind.Umbrella, 196, g5b);
        return s;
    }

    /// <summary>Belts on three floors carrying trays; a fire in the middle; the ape paces the top.</summary>
    private static Stage Conveyors()
    {
        var top = new Platform { X0 = 88, X1 = 136, Y0 = 56, Y1 = 56 };
        var s = new Stage { Kind = StageKind.Conveyors, Start = new Vec(36, 248), Ape = new Vec(112, 88), Captive = new Vec(100, 56), Goal = top, Drum = new Vec(112, 168) };
        var f0 = s.Add(0, 248, 224, 248);
        var f1 = s.Add(0, 208, 224, 208, belt: 28);
        var f2l = s.Add(0, 168, 96, 168, belt: 28);
        var f2m = s.Add(96, 168, 128, 168);
        var f2r = s.Add(128, 168, 224, 168, belt: -28);
        var f3 = s.Add(0, 128, 224, 128, belt: -28);
        var f4 = s.Add(32, 88, 192, 88);
        s.Platforms.Add(top);
        s.Ladder(24, f1, f0);
        s.Ladder(112, f1, f0);
        s.Ladder(200, f1, f0);
        s.Ladder(40, f2l, f1);
        s.Ladder(184, f2r, f1);
        s.Ladder(16, f3, f2l);
        s.Ladder(80, f3, f2l);
        s.Ladder(144, f3, f2r);
        s.Ladder(208, f3, f2r);
        s.Ladder(48, f4, f3);
        s.Ladder(176, f4, f3);
        s.Ladder(120, top, f4);
        s.Hammer(64, f1);
        s.Hammer(176, f3);
        s.Item(ItemKind.Hat, 200, f2r);
        s.Item(ItemKind.Purse, 32, f3);
        return s;
    }

    /// <summary>Islands, a lift going up and a lift going down, and springs bouncing off the top.</summary>
    private static Stage Elevators()
    {
        var top = new Platform { X0 = 88, X1 = 136, Y0 = 40, Y1 = 40 };
        var s = new Stage { Kind = StageKind.Elevators, Start = new Vec(20, 248), Ape = new Vec(40, 72), Captive = new Vec(100, 40), Goal = top };
        var l0 = s.Add(0, 248, 56, 248);
        var l1 = s.Add(0, 196, 48, 196);
        var m1 = s.Add(96, 176, 136, 176);
        var m2 = s.Add(96, 128, 136, 128);
        var r1 = s.Add(184, 152, 224, 152);
        var r2 = s.Add(184, 108, 224, 108);
        var t = s.Add(16, 72, 200, 72);
        s.Platforms.Add(top);
        s.Ladder(24, l1, l0);
        s.Ladder(104, m2, m1);
        s.Ladder(128, m2, m1);
        s.Ladder(208, r2, r1);
        s.Ladder(192, t, r2);
        s.Ladder(128, top, t);
        s.Shafts.Add(new Shaft { X = 72, Top = 92, Bottom = 268, Speed = -26 });
        s.Shafts.Add(new Shaft { X = 160, Top = 92, Bottom = 268, Speed = 26 });
        foreach (var shaft in s.Shafts)
            for (double y = shaft.Top + 4; y < shaft.Bottom; y += 44)
            {
                var lift = new Platform { X0 = shaft.X - Shaft.LiftWidth / 2, X1 = shaft.X + Shaft.LiftWidth / 2, Y0 = y, Y1 = y, Lift = true };
                shaft.Lifts.Add(lift);
                s.Platforms.Add(lift);
            }
        s.Item(ItemKind.Purse, 12, l1);
        s.Item(ItemKind.Hat, 116, m2);
        s.Item(ItemKind.Umbrella, 216, r2);
        return s;
    }

    /// <summary>Five floors; the middle four held by a rivet near each end.</summary>
    private static Stage RivetStage()
    {
        var top = new Platform { X0 = 88, X1 = 136, Y0 = 56, Y1 = 56 };
        var s = new Stage { Kind = StageKind.Rivets, Start = new Vec(40, 248), Ape = new Vec(112, 88), Captive = new Vec(112, 56), Goal = top };
        var f0 = s.Add(0, 248, 224, 248);
        s.Platforms.Add(top);
        // Each riveted floor: left end, left plug, middle, right plug, right end.
        Platform[] Floor(double x0, double x1, double y, double left, double right)
        {
            var a = s.Add(x0, y, left - 4, y);
            var pl = new Platform { X0 = left - 4, X1 = left + 4, Y0 = y, Y1 = y, Plug = true };
            var b = s.Add(left + 4, y, right - 4, y);
            var pr = new Platform { X0 = right - 4, X1 = right + 4, Y0 = y, Y1 = y, Plug = true };
            var c = s.Add(right + 4, y, x1, y);
            s.Platforms.Add(pl);
            s.Platforms.Add(pr);
            s.Rivets.Add(new Rivet(left, pl));
            s.Rivets.Add(new Rivet(right, pr));
            return [a, b, c];
        }
        var f1 = Floor(16, 208, 208, 40, 184);
        var f2 = Floor(24, 200, 168, 48, 176);
        var f3 = Floor(32, 192, 128, 56, 168);
        var f4 = Floor(40, 184, 88, 64, 160);
        s.Ladder(24, f1[0], f0);
        s.Ladder(112, f1[1], f0);
        s.Ladder(200, f1[2], f0);
        s.Ladder(32, f2[0], f1[0]);
        s.Ladder(112, f2[1], f1[1]);
        s.Ladder(192, f2[2], f1[2]);
        s.Ladder(40, f3[0], f2[0]);
        s.Ladder(120, f3[1], f2[1]);
        s.Ladder(184, f3[2], f2[2]);
        s.Ladder(48, f4[0], f3[0]);
        s.Ladder(88, f4[1], f3[1]);
        s.Ladder(176, f4[2], f3[2]);
        s.Hammer(104, f2[1]);
        s.Hammer(144, f3[1]);
        s.Item(ItemKind.Purse, 208, f0);
        s.Item(ItemKind.Hat, 30, f2[0]);
        s.Item(ItemKind.Umbrella, 128, f3[1]);
        return s;
    }

    /// <summary>The stages of a level: more of them as the levels go on, always ending on the rivets.</summary>
    public static StageKind[] Sequence(int level) => level switch
    {
        1 => [StageKind.Girders, StageKind.Rivets],
        2 => [StageKind.Girders, StageKind.Elevators, StageKind.Rivets],
        3 => [StageKind.Girders, StageKind.Conveyors, StageKind.Elevators, StageKind.Rivets],
        _ => [StageKind.Girders, StageKind.Conveyors, StageKind.Girders, StageKind.Elevators, StageKind.Rivets],
    };
}
