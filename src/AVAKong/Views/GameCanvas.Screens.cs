using Avalonia;
using Avalonia.Media;
using AVAKong.Engine;
using AVAKong.Models;

namespace AVAKong.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: the scores and spare lives on the left; the level, the height and the keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "1UP", new Point(lx, 60), 30, Red, center: true);
        Text(dc, (playing ? g.Score : 0).ToString("D6"), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HIGH SCORE", new Point(lx, 160), 30, Red, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D6"), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            int spare = g.Lives - (g.Phase is Phase.Dying ? 0 : 1);
            for (int i = 0; i < Math.Min(spare, 5); i++)
                DrawPanelSprite(dc, Sprites.HeroStand, lx - 54 + i * 30, 690);
            Text(dc, "LEVEL", new Point(rx, 60), 30, Red, center: true);
            Text(dc, g.Level.ToString(), new Point(rx, 92), 34, White, center: true);
            Text(dc, "HEIGHT", new Point(rx, 160), 30, Red, center: true);
            Text(dc, $"{g.Metres} m", new Point(rx, 192), 34, Yellow, center: true);
        }
        Text(dc, "AVAKONG", new Point(rx, 540), 24, Dim, center: true);
        Text(dc, "Arrows or WASD move", new Point(rx, 570), 20, Dim, center: true);
        Text(dc, "Up and down climb", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Space jumps", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 2;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Text(dc, "AVA KONG", P(cx, 20), 96, Red, center: true);
        Text(dc, "AFTER THE 1981 ARCADE CLASSIC", P(cx, 36), 26, Dim, center: true);

        // The ape, pounding his chest, with the captive beside him.
        bool up = _clock % 2 < 1 && (int)(_clock * 6) % 2 == 0;
        DrawSprite(dc, up ? Sprites.ApeArmsUp : Sprites.ApeIdle, cx - 30, 50);
        DrawSprite(dc, Sprites.Captive[(int)(_clock * 3) % 2], cx + 18, 60);
        DrawGirder(dc, new Platform { X0 = cx - 50, X1 = cx + 50, Y0 = 82, Y1 = 82 }, 0xFFFF2E63);

        Text(dc, "CLIMB TO THE TOP AND RESCUE THE CAPTIVE", P(cx, 98), 26, White, center: true);
        Row(Sprites.Barrel[0], 112, "JUMP A BARREL", "100");
        Row(Sprites.Barrel[1], 124, "TWO AT ONCE / THREE OR MORE", "300/800");
        Row(Sprites.Hammer, 136, "HAMMER: SMASH BARRELS AND FIRE", "300-800");
        Row(Sprites.Purse, 148, "HER PURSE, HAT OR UMBRELLA", "300-800");
        Row(Sprites.Rivet, 160, "PULL A RIVET", "100");
        Text(dc, "FINISH A STAGE TO KEEP WHAT'S LEFT OF THE BONUS", P(cx, 176), 24, Yellow, center: true);
        Text(dc, "PULL ALL EIGHT RIVETS TO BRING THE APE DOWN", P(cx, 186), 24, Yellow, center: true);

        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 214), 32, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 238), 22, Dim, center: true);

        void Row(Sprites.Sprite s, double y, string name, string points)
        {
            DrawSprite(dc, s, 22 - s.Width / 2.0, y - s.Height / 2.0);
            Text(dc, name, P(36, y), 26, White);
            Text(dc, points, P(212, y), 26, White, right: true);
        }
    }

    /// <summary>A score's place on the table: the level and how high.</summary>
    public static string StageName(int stage) => $"L{stage / 100}  {stage % 100} m";

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Text(dc, "TOP CLIMBERS", P(cx, 30), 60, Yellow, center: true);
        Text(dc, "RANK    SCORE   NAME   REACHED", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Red : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {StageName(e.Stage),-9}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
