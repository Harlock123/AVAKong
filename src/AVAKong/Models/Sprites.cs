namespace AVAKong.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The hero is drawn row by row; the bigger and
/// rounder things are painted from ellipses and boxes into a pixel grid, which keeps them easy to read
/// and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['h'] = 0xFFFFD23A, // hard hat
        ['s'] = 0xFFFFC08A, // skin
        ['g'] = 0xFF3CC860, // green shirt
        ['b'] = 0xFF8A5A2A, // brown overalls
        ['k'] = 0xFF2A1A12, // dark
        ['w'] = 0xFFF4F4F4, // white
        ['f'] = 0xFF8A4A1E, // ape fur
        ['F'] = 0xFF5E3010, // dark fur
        ['l'] = 0xFFE8A878, // light face and chest
        ['r'] = 0xFFE83A3A, // red
        ['o'] = 0xFFFF8A1E, // orange
        ['y'] = 0xFFFFE14A, // yellow
        ['v'] = 0xFFB88AFF, // lavender
        ['p'] = 0xFFFF7AC8, // pink
        ['n'] = 0xFFB86A2A, // barrel wood
        ['d'] = 0xFF6A3A12, // barrel bands
        ['u'] = 0xFF2A6AFF, // blue
        ['U'] = 0xFF8AB8FF, // light blue
        ['c'] = 0xFFB8B8C0, // cement
        ['m'] = 0xFF7A7A88, // steel
    };

    // ----- the hero (facing right; mirrored for left) -------------------------------------------

    private static readonly string[] HeroTop =
    [
        "....hhhh....",
        "...hhhhhhh..",
        "...sssss....",
        "...ssssks...",
        "...sssssss..",
        "....ssss....",
        "...gggggg...",
        "..ggbggbgg..",
        "..sgbbbbgs..",
        "...bbbbbb...",
        "...bbbbbb...",
    ];

    public static readonly Sprite HeroStand = S([.. HeroTop, "...bb..bb...", "...bb..bb...", "...bb..bb...", "..kkk..kkk..", "............"]);

    public static readonly Sprite[] HeroWalk =
    [
        S([.. HeroTop, "..bb....bb..", "..bb....bb..", ".kkk....bb..", "........kkk.", "............"]),
        HeroStand,
        S([.. HeroTop, "....bbbb....", "....bb.bb...", "...kkk.bb...", "......kkk...", "............"]),
        HeroStand,
    ];

    public static readonly Sprite HeroJump = S(
        "....hhhh....",
        "...hhhhhhh..",
        "...sssss....",
        "...ssssks...",
        "...sssssss..",
        "s...ssss...s",
        "sg.gggggg.gs",
        ".ggbggbggg..",
        "...bbbbbb...",
        "...bbbbbb...",
        "..bbb..bbb..",
        ".bb......bb.",
        "kkk......kkk",
        "............",
        "............",
        "............");

    /// <summary>Climbing, seen from behind: two frames.</summary>
    public static readonly Sprite[] HeroClimb =
    [
        S("....hhhh....", "...hhhhhh...", "...hhhhhh...", "...kkkkkk...", "....kkkk....", "s..gggggg...",
          "sggggggggg..", "...gggggggs.", "...bbbbbb.s.", "...bbbbbb...", "...bbbbbb...", "...bb..bb...",
          "...bb..bb...", "...bb..kkk..", "..kkk.......", "............"),
        S("....hhhh....", "...hhhhhh...", "...hhhhhh...", "...kkkkkk...", "....kkkk....", "...gggggg..s",
          "..ggggggggs.", ".sggggggg...", ".s.bbbbbb...", "...bbbbbb...", "...bbbbbb...", "...bb..bb...",
          "...bb..bb...", "..kkk..bb...", ".......kkk..", "............"),
    ];

    // ----- the ape --------------------------------------------------------------------------------

    private static Canvas ApeBody(bool armsUp)
    {
        var c = new Canvas(36, 32)
            .Ellipse(10, 28, 6, 4, 'f').Ellipse(26, 28, 6, 4, 'f')
            .Ellipse(18, 20, 12.5, 9, 'f')
            .Ellipse(18, 21, 7, 6, 'l')
            .Ellipse(18, 9, 8, 7, 'f');
        if (armsUp)
            c.Ellipse(5, 10, 4, 8, 'f').Ellipse(31, 10, 4, 8, 'f').Ellipse(5, 3, 3.5, 3, 'l').Ellipse(31, 3, 3.5, 3, 'l');
        else
            c.Ellipse(4, 20, 4, 8, 'f').Ellipse(32, 20, 4, 8, 'f').Ellipse(4, 27, 3.5, 3, 'l').Ellipse(32, 27, 3.5, 3, 'l');
        return c
            .Ellipse(18, 11, 5.5, 4, 'l')
            .Box(13, 6, 22, 6, 'F')
            .Dot(14, 8, 'w').Dot(15, 8, 'k').Dot(21, 8, 'k').Dot(22, 8, 'w')
            .Dot(17, 10, 'F').Dot(19, 10, 'F')
            .Box(15, 13, 21, 13, 'r');
    }

    public static readonly Sprite ApeIdle = ApeBody(false).Done();
    public static readonly Sprite ApeArmsUp = ApeBody(true).Done();

    // ----- the captive ----------------------------------------------------------------------------

    public static readonly Sprite[] Captive =
    [
        new Canvas(12, 22).Ellipse(6, 4, 4.5, 4, 'o').Ellipse(6.5, 5, 2.8, 2.8, 's').Dot(7, 4, 'k')
            .Box(4, 8, 8, 10, 'v').Box(3, 11, 9, 14, 'v').Box(2, 15, 10, 19, 'v').Box(1, 18, 11, 19, 'v')
            .Box(2, 9, 2, 12, 's').Box(9, 7, 9, 9, 's').Dot(10, 6, 's')
            .Box(4, 20, 5, 21, 's').Box(7, 20, 8, 21, 's').Done(),
        new Canvas(12, 22).Ellipse(6, 4, 4.5, 4, 'o').Ellipse(6.5, 5, 2.8, 2.8, 's').Dot(7, 4, 'k')
            .Box(4, 8, 8, 10, 'v').Box(3, 11, 9, 14, 'v').Box(2, 15, 10, 19, 'v').Box(1, 18, 11, 19, 'v')
            .Box(2, 9, 2, 12, 's').Box(9, 9, 10, 9, 's').Dot(11, 8, 's')
            .Box(4, 20, 5, 21, 's').Box(7, 20, 8, 21, 's').Done(),
    ];

    // ----- barrels, fire, trays, springs ----------------------------------------------------------

    /// <summary>A rolling barrel, four turns of its bands.</summary>
    public static readonly Sprite[] Barrel = Enumerable.Range(0, 4).Select(f =>
    {
        var c = new Canvas(12, 10).Ellipse(6, 5, 6, 5, 'n');
        foreach (int x in new[] { 1 + f, 6 + f })
            if (x is > 0 and < 11) c.Box(x, 1, x, 8, 'd');
        return c.Ellipse(6, 5, 6, 5, 'n').Box(1 + f % 2 * 5, 1, 1 + f % 2 * 5, 8, 'd').Box(2, 0, 9, 0, 'd').Box(2, 9, 9, 9, 'd').Done();
    }).ToArray();

    /// <summary>A barrel coming down a ladder, seen end on.</summary>
    public static readonly Sprite BarrelDown = new Canvas(14, 10).Box(1, 0, 12, 9, 'n').Box(0, 2, 13, 7, 'n')
        .Box(1, 2, 12, 2, 'd').Box(1, 7, 12, 7, 'd').Box(5, 4, 8, 5, 'd').Done();

    public static readonly Sprite[] Fireball =
    [
        new Canvas(10, 12).Ellipse(5, 8, 4.8, 4, 'o').Ellipse(5, 5, 3, 5, 'o').Ellipse(5, 8, 3, 2.5, 'y').Dot(3, 7, 'k').Dot(6, 7, 'k').Done(),
        new Canvas(10, 12).Ellipse(5, 8, 4.8, 4, 'o').Ellipse(6, 4, 2.6, 4.5, 'o').Ellipse(5, 8, 3, 2.5, 'y').Dot(3, 7, 'k').Dot(6, 7, 'k').Done(),
    ];

    public static readonly Sprite Tray = new Canvas(14, 7).Ellipse(7, 3, 6, 3, 'c').Box(1, 4, 12, 4, 'c').Box(0, 5, 13, 6, 'm').Done();

    public static readonly Sprite[] Jack =
    [
        new Canvas(14, 10).Box(0, 0, 13, 1, 'm').Box(0, 8, 13, 9, 'm').Box(2, 2, 11, 2, 'r').Box(2, 4, 11, 4, 'r').Box(2, 6, 11, 6, 'r')
            .Box(2, 2, 2, 4, 'r').Box(11, 4, 11, 6, 'r').Done(),
        new Canvas(14, 10).Box(0, 2, 13, 3, 'm').Box(0, 8, 13, 9, 'm').Box(2, 5, 11, 5, 'r').Box(2, 7, 11, 7, 'r')
            .Box(2, 5, 2, 7, 'r').Done(),
    ];

    public static readonly Sprite Drum = new Canvas(14, 16).Box(1, 0, 12, 15, 'u').Box(0, 1, 13, 2, 'U').Box(0, 13, 13, 14, 'U')
        .Box(3, 6, 4, 9, 'w').Box(6, 6, 7, 9, 'w').Box(9, 6, 9, 9, 'w').Box(10, 9, 11, 9, 'w').Done();

    public static readonly Sprite Hammer = new Canvas(9, 12).Box(0, 0, 8, 3, 'm').Box(1, 0, 7, 0, 'w').Box(3, 4, 5, 11, 'n').Done();

    public static readonly Sprite Rivet = new Canvas(8, 6).Box(0, 0, 7, 5, 'y').Box(1, 1, 6, 1, 'w').Box(3, 2, 4, 4, 'n').Done();

    public static readonly Sprite Heart = new Canvas(15, 13).Ellipse(4, 4, 4, 4, 'p').Ellipse(11, 4, 4, 4, 'p')
        .Ellipse(7.5, 6, 6, 4, 'p').Ellipse(7.5, 9, 3.5, 4, 'p').Dot(3, 2, 'w').Dot(2, 3, 'w').Done();

    public static readonly Sprite Purse = new Canvas(12, 12).Ellipse(6, 3, 3.5, 3, 'p').Ellipse(6, 3, 2, 1.8, '.')
        .Box(1, 5, 10, 11, 'p').Box(2, 6, 9, 6, 'w').Dot(6, 8, 'y').Done();

    public static readonly Sprite Hat = new Canvas(12, 12).Ellipse(6, 6, 4, 4, 'v').Box(0, 7, 11, 9, 'v').Box(2, 6, 9, 6, 'p').Done();

    public static readonly Sprite Umbrella = new Canvas(12, 12).Ellipse(6, 5, 6, 5, 'p').Box(0, 5, 11, 11, '.')
        .Dot(1, 5, 'p').Dot(5, 5, 'p').Dot(10, 5, 'p').Box(6, 1, 6, 10, 'k').Box(4, 10, 6, 10, 'k').Box(4, 9, 4, 9, 'k').Done();

    public static Sprite ItemArt(ItemKind k) => k switch { ItemKind.Purse => Purse, ItemKind.Hat => Hat, _ => Umbrella };
}
