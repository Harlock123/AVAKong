using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAKong.Engine;
using AVAKong.Models;

namespace AVAKong.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAKong/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Pink = Brush(0xFFFF7AC8);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);
    private const uint LadderColour = 0xFF4FD7FF;

    /// <summary>Blue barrels: the same drawing in blue.</summary>
    private static readonly Dictionary<char, char> BlueBarrel = new() { ['n'] = 'u', ['d'] = 'U' };

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    if (g.Phase == Phase.Intro && g.ShowHowHigh) DrawHowHigh(dc);
                    else DrawPlay(dc);
                    break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the site -----------------------------------------------------------------------------

    private uint GirderColour => Engine.Stage.Kind switch
    {
        StageKind.Girders => 0xFFFF2E63,
        StageKind.Conveyors => 0xFFFF6A2E,
        StageKind.Elevators => 0xFFE8306E,
        _ => 0xFF3A7CFF,
    };

    private void DrawSite(DrawingContext dc)
    {
        var g = Engine;
        var s = g.Stage;
        bool collapsing = g.Phase == Phase.LevelClear;

        foreach (var shaft in s.Shafts)
        {
            // Cables down each side of the shaft, and the machinery at top and bottom.
            Fill(dc, 0xFF5A5A6A, shaft.X - 10, shaft.Top - 4, 1, Field.Height);
            Fill(dc, 0xFF5A5A6A, shaft.X + 9, shaft.Top - 4, 1, Field.Height);
            Fill(dc, 0xFFB8B8C0, shaft.X - 12, shaft.Top - 12, 24, 8);
            Fill(dc, 0xFF5A5A6A, shaft.X - 4, shaft.Top - 10, 8, 4);
        }

        foreach (var l in s.Ladders)
        {
            if (collapsing && l.Top > 60 && l.Bottom < 240) continue;
            DrawLadder(dc, l);
        }

        foreach (var p in s.Platforms)
        {
            if (p.Removed) continue;
            if (collapsing && p.Y0 > 60 && p.Y0 < 240) continue;
            if (p.Plug)
            {
                DrawSprite(dc, Sprites.Rivet, (p.X0 + p.X1) / 2 - 4, p.Y0);
                continue;
            }
            if (p.Belt != 0) DrawBelt(dc, p);
            else DrawGirder(dc, p, p.Lift ? 0xFFFF9A3A : GirderColour);
        }

        foreach (var pk in s.Pickups)
        {
            if (pk.Taken) continue;
            if (pk.IsHammer) DrawSprite(dc, Sprites.Hammer, pk.Pos.X - 4.5, pk.Pos.Y - 6);
            else DrawSprite(dc, Sprites.ItemArt(pk.Item!.Value), pk.Pos.X - 6, pk.Pos.Y - 6);
        }

        if (s.Drum is { } d)
        {
            DrawSprite(dc, Sprites.Drum, d.X - 7, d.Y - 16);
            if (g.DrumLit)
            {
                int f = (int)(_clock * 8) % 2;
                DrawSprite(dc, Sprites.Fireball[f], d.X - 7, d.Y - 26);
                DrawSprite(dc, Sprites.Fireball[1 - f], d.X - 2, d.Y - 27);
            }
        }
    }

    private static void DrawLadder(DrawingContext dc, Ladder l)
    {
        double top = l.Top, bottom = l.Bottom;
        void Part(double y0, double y1)
        {
            Fill(dc, LadderColour, l.X - 4, y0, 1, y1 - y0);
            Fill(dc, LadderColour, l.X + 3, y0, 1, y1 - y0);
            for (double y = y0 + 2; y < y1; y += 4) Fill(dc, LadderColour, l.X - 4, y, 8, 1);
        }
        if (!l.Broken) Part(top, bottom);
        else
        {
            // A stub hanging under the girder above, and one standing on the girder below.
            Part(top + 6, top + 12);
            Part(bottom - 7, bottom);
        }
    }

    /// <summary>A girder: 8-pixel lengths following the slope, each with a hole in its web.</summary>
    private static void DrawGirder(DrawingContext dc, Platform p, uint colour)
    {
        for (double x = p.X0; x < p.X1 - 0.01; x += 8)
        {
            double w = Math.Min(8, p.X1 - x);
            double y = Math.Round(p.YAt(x + w / 2));
            Fill(dc, colour, x, y, w, 7);
            if (w >= 6) Fill(dc, 0xFF000000, x + 2, y + 2, w - 4, 3);
        }
    }

    private void DrawBelt(DrawingContext dc, Platform p)
    {
        double y = Math.Round(p.Y0);
        Fill(dc, 0xFFFFB040, p.X0, y, p.X1 - p.X0, 6);
        // The belt's treads move the way it carries.
        double shift = (_clock * p.Belt) % 8;
        for (double x = p.X0 - 8 + ((shift % 8) + 8) % 8; x < p.X1; x += 8)
            if (x >= p.X0 && x + 3 <= p.X1) Fill(dc, 0xFF8A4A10, x, y + 2, 3, 2);
        foreach (double rx in new[] { p.X0 + 4, p.X1 - 4 })
            dc.DrawEllipse(Brush(0xFF8A8A9A), null, P(rx, y + 3), 4 * Scale, 4 * Scale);
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        DrawSite(dc);
        DrawApeAndCaptive(dc);

        foreach (var t in g.Trays) DrawSprite(dc, Sprites.Tray, t.X - 7, t.On.YAt(t.X) - 7);
        foreach (var j in g.Jacks) DrawSprite(dc, Sprites.Jack[j.Hop is > 0.15 and < 0.85 ? 0 : 1], j.X - 7, j.Y - 10);
        foreach (var b in g.Barrels)
        {
            var remap = b.Blue ? BlueBarrel : null;
            if (b.State == BarrelState.Dropping) DrawSprite(dc, Sprites.BarrelDown, b.X - 7, b.Y - 10, remap: remap);
            else DrawSprite(dc, Sprites.Barrel[(int)(b.Spin / 4) % 4], b.X - 6, b.Y - 10, flip: b.Dir < 0, remap: remap);
        }
        foreach (var f in g.Fireballs) DrawSprite(dc, Sprites.Fireball[(int)(_clock * 8 + f.X) % 2], f.X - 5, f.Y - 12, flip: f.Dir < 0);

        DrawHero(dc);

        foreach (var fx in g.Effects)
        {
            if (fx.Kind == EffectKind.Score) Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - fx.Age * 8), 26, Cyan, center: true);
            else
            {
                double r = (3 + fx.Age / fx.Life * 9) * Scale;
                dc.DrawEllipse(null, new Pen(Brush(0xFFFFE14A), 3), P(fx.Pos), r, r);
                dc.DrawEllipse(null, new Pen(Brush(0xFFF2F2F2), 2), P(fx.Pos), r * 0.55, r * 0.55);
            }
        }

        // The bonus, top right, as on the arcade's screen.
        Fill(dc, 0xFF1A2A6A, 162, 6, 56, 22);
        dc.DrawRectangle(null, new Pen(Brush(0xFF4F7AFF), 2), new Rect(P(162, 6), P(218, 28)));
        Text(dc, "BONUS", P(190, 12), 24, Cyan, center: true);
        Text(dc, g.Bonus.ToString("D4"), P(190, 22), 30, g.Bonus <= 1000 && _clock % 0.5 < 0.3 ? Red : Yellow, center: true);

        var notice = P(Field.Width / 2.0, 34);
        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", P(Field.Width / 2.0, 120), 60, White, center: true);
                Text(dc, "ESC TO QUIT, ANY OTHER KEY TO RESUME", P(Field.Width / 2.0, 134), 24, Dim, center: true);
                break;
            case GameState.GameOver:
                Fill(dc, 0xE0000000, 50, 110, 124, 30);
                Text(dc, "GAME  OVER", P(Field.Width / 2.0, 125), 56, Red, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            case GameState.Attract:
                Text(dc, "DEMONSTRATION", notice, 32, Red, center: true);
                break;
        }
    }

    private void DrawApeAndCaptive(DrawingContext dc)
    {
        var g = Engine;
        var s = g.Stage;
        var ape = g.Ape;
        double t = g.PhaseTime;

        // The captive, waving and calling.
        bool carried = g.Phase == Phase.StageClear && t > 1.8;
        if (!carried)
        {
            var cap = s.Captive;
            DrawSprite(dc, Sprites.Captive[(int)(_clock * 3) % 2], cap.X - 6, cap.Y - 22);
            if (g.Phase == Phase.Playing && _clock % 3 < 1.2) Text(dc, "HELP!", P(cap.X + 16, cap.Y - 20), 26, White, center: true);
        }

        if (g.Phase == Phase.LevelClear)
        {
            // The floor gone, the ape falls on his head.
            double y = Math.Min(244, ape.Y + t * t * 40);
            DrawSprite(dc, Sprites.ApeIdle, ape.X - 18, y - 32, flipY: true);
            if (t > 2.4)
            {
                DrawSprite(dc, Sprites.Heart, s.Captive.X + 10 - 7.5, s.Captive.Y - 34);
                DrawHeroSprite(dc, Sprites.HeroStand, s.Captive.X + 22, s.Captive.Y, -1);
            }
            return;
        }

        if (carried)
        {
            // He grabs her and climbs away.
            double y = ape.Y - (t - 1.8) * 50;
            DrawSprite(dc, Sprites.ApeArmsUp, ape.X - 18, y - 32);
            DrawSprite(dc, Sprites.Captive[0], ape.X + 6, y - 26);
            return;
        }

        var art = Sprites.ApeIdle;
        if (s.Kind == StageKind.Girders && g.SinceThrow < 0.45)
        {
            art = Sprites.ApeArmsUp;
            DrawSprite(dc, Sprites.BarrelDown, ape.X - 7, ape.Y - 44);
        }
        else if (_clock % 3 < 1.1 && (int)(_clock * 6) % 2 == 0) art = Sprites.ApeArmsUp;
        DrawSprite(dc, art, ape.X - 18, ape.Y - 32);
        if (s.Kind == StageKind.Girders)
        {
            // His stack of barrels.
            foreach (var (bx, by) in new[] { (0.0, 10.0), (12.0, 10.0), (6.0, 20.0) }) DrawSprite(dc, Sprites.BarrelDown, bx, ape.Y - by);
        }
        if (g.Phase == Phase.StageClear)
            DrawSprite(dc, Sprites.Heart, (s.Captive.X + g.Hero.X) / 2 - 7.5, s.Captive.Y - 36);
    }

    private void DrawHero(DrawingContext dc)
    {
        var g = Engine;
        var h = g.Hero;
        if (g.Phase == Phase.LevelClear) return;
        if (g.Phase == Phase.Intro && !g.ShowHowHigh && (int)(_clock * 8) % 2 == 0) return;
        if (g.Phase == Phase.Dying)
        {
            double t = g.PhaseTime;
            double angle = t < 1.2 ? t * 720 : 90;
            DrawHeroSprite(dc, Sprites.HeroStand, h.X, h.Feet, h.Facing, angle);
            if (t >= 1.2) dc.DrawEllipse(null, new Pen(Brush(0xFFFFE14A), 2), P(h.X - 4, h.Feet - 12), 5 * Scale, 2 * Scale);
            return;
        }

        var art = h.State switch
        {
            HeroState.Climbing => Sprites.HeroClimb[(int)(h.Stride / 4) % 2],
            HeroState.Jumping or HeroState.Falling => Sprites.HeroJump,
            _ => h.Moving ? Sprites.HeroWalk[(int)(h.Stride / 3) % 4] : Sprites.HeroStand,
        };
        DrawHeroSprite(dc, art, h.X, h.Feet, h.Facing);

        if (g.HammerLeft > 0 && !(g.HammerLeft < 2 && (int)(_clock * 10) % 2 == 0))
        {
            // Swinging: up over the head, then down in front.
            bool up = (int)(_clock * 7) % 2 == 0;
            if (up) DrawSprite(dc, Sprites.Hammer, h.X - 4.5 + h.Facing * 2, h.Feet - 26);
            else DrawSprite(dc, Sprites.Hammer, h.X + h.Facing * 9 - 4.5, h.Feet - 14, angle: h.Facing * 90);
        }
    }

    private static void DrawHeroSprite(DrawingContext dc, Sprites.Sprite art, double x, double feet, int facing, double angle = 0) =>
        DrawSprite(dc, art, x - 6, feet - 16, flip: facing < 0, angle: angle);

    /// <summary>The "how high" screen: one ape for each stage climbed so far.</summary>
    private void DrawHowHigh(DrawingContext dc)
    {
        var g = Engine;
        int n = g.StageIndex + 1;
        for (int i = 0; i < n; i++)
        {
            double y = 236 - i * 40;
            DrawSprite(dc, Sprites.ApeIdle, 60, y - 32);
            Text(dc, $"{(i + 1) * 25} m", P(130, y - 14), 40, i == n - 1 ? Yellow : White);
        }
        Text(dc, "HOW HIGH CAN YOU CLIMB?", P(Field.Width / 2.0, 252 - n * 40 - 14), 36, Cyan, center: true);
        Text(dc, $"LEVEL {g.Level}", P(Field.Width / 2.0, 20), 34, White, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored, flipped, recoloured or turned.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y,
        bool flip = false, bool flipY = false, IReadOnlyDictionary<char, char>? remap = null, double angle = 0)
    {
        var centre = P(x + s.Width / 2.0, y + s.Height / 2.0);
        using var turn = angle != 0
            ? (IDisposable?)dc.PushTransform(Matrix.CreateTranslation(-centre.X, -centre.Y)
                * Matrix.CreateRotation(angle * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y))
            : null;
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[flipY ? s.Height - 1 - r : r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                if (remap is not null && remap.TryGetValue(ch, out var to)) ch = to;
                var a = P(x + c, y + r);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale + 0.3, Scale + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
