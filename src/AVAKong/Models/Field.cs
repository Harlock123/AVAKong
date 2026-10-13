namespace AVAKong.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>An axis-aligned box.</summary>
public readonly record struct Box(double Left, double Top, double Right, double Bottom)
{
    /// <summary>A box standing on <paramref name="feet"/>, centred on <paramref name="x"/>.</summary>
    public static Box Standing(double x, double feet, double width, double height) =>
        new(x - width / 2, feet - height, x + width / 2, feet);

    public bool Overlaps(Box o) => Left < o.Right && o.Left < Right && Top < o.Bottom && o.Top < Bottom;
    public Box Shrink(double d) => new(Left + d, Top + d, Right - d, Bottom - d);
}

/// <summary>The screen in game pixels: 224 by 256, portrait like the arcade's.</summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const double Step = 1.0 / 60;
}
