using AVAKong.Models;

namespace AVAKong.Engine;

public enum HeroState { Walking, Jumping, Falling, Climbing }

/// <summary>The hero: X is the middle, Feet the bottom of the boots.</summary>
public sealed class Hero
{
    public const double Width = 10, Height = 16;
    public double X { get; set; }
    public double Feet { get; set; }
    public HeroState State { get; set; }
    /// <summary>+1 facing right, −1 left.</summary>
    public int Facing { get; set; } = 1;
    public double Vx { get; set; }
    public double Vy { get; set; }
    /// <summary>Where a jump or fall began, to judge whether the landing is survivable.</summary>
    public double FromY { get; set; }
    public Platform? On { get; set; }
    public Ladder? Ladder { get; set; }
    /// <summary>Distance walked or climbed, for the stride.</summary>
    public double Stride { get; set; }
    public bool Moving { get; set; }
    public Box Box => Box.Standing(X, Feet, Width, Height);
}

public enum BarrelState { Rolling, Dropping, Falling }

/// <summary>A barrel: rolls downhill, drops off the ends, and sometimes takes a ladder.</summary>
public sealed class Barrel
{
    public double X { get; set; }
    public double Y { get; set; }
    public int Dir { get; set; } = 1;
    public double Vy { get; set; }
    public BarrelState State { get; set; }
    public Platform? On { get; set; }
    public Ladder? Ladder { get; set; }
    /// <summary>Blue: lights a fireball when it reaches the drum.</summary>
    public bool Blue { get; init; }
    /// <summary>The ape's first barrel of a stage: takes every ladder it meets, straight to the drum.</summary>
    public bool Straight { get; init; }
    public double Spin { get; set; }
    public bool Dead { get; set; }
    /// <summary>The last ladder it decided about, so it decides once per ladder.</summary>
    public Ladder? Considered { get; set; }
    public Box Box => new(X - 5, Y - 10, X + 5, Y);
}

/// <summary>A fireball: wanders the platforms and ladders, and tends to drift towards the hero.</summary>
public sealed class Fireball
{
    public double X { get; set; }
    public double Y { get; set; }
    public int Dir { get; set; } = 1;
    public Platform? On { get; set; }
    public Ladder? Ladder { get; set; }
    public int ClimbDir { get; set; }
    /// <summary>Hopping out of the drum, before it settles on a platform.</summary>
    public double Vy { get; set; }
    public bool Airborne { get; set; }
    public double Think { get; set; }
    public bool Dead { get; set; }
    public Ladder? Considered { get; set; }
    public Box Box => new(X - 5, Y - 12, X + 5, Y);
}

/// <summary>A tray of cement riding a conveyor.</summary>
public sealed class Tray
{
    public double X { get; set; }
    public required Platform On { get; init; }
    public bool Dead { get; set; }
    public Box Box => new(X - 7, On.YAt(X) - 7, X + 7, On.YAt(X));
}

/// <summary>A spring: bounds along the top girder, then drops down the right-hand side.</summary>
public sealed class Jack
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Hop { get; set; }
    public bool Dropping { get; set; }
    public bool Dead { get; set; }
    public Box Box => new(X - 7, Y - 10, X + 7, Y);
}

public enum EffectKind { Score, Smash }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: left or right (−1, 0, +1), up or down, and jump.</summary>
public record struct Controls(int Dx, int Dy, bool Jump);
