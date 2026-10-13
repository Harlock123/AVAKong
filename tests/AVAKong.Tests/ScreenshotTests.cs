using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAKong.Engine;
using AVAKong.Models;
using AVAKong.Views;
using Controls = AVAKong.Engine.Controls;

namespace AVAKong.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set KONG_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kong-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("KONG_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAKong-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Play(GameEngine g, AttractPilot pilot, int steps)
    {
        for (int i = 0; i < steps && g.Phase == Phase.Playing && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
    }

    [AvaloniaFact]
    public void The_title_how_high_and_the_girders()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Idle(60);
        Shot(w, c, "how-high");
        g.Go();
        var pilot = new AttractPilot(2);
        Play(g, pilot, 60 * 12);
        Shot(w, c, "girders");
        w.Close();
    }

    [AvaloniaFact]
    public void The_belts_the_lifts_and_the_rivets()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpTo(3, 1);
        var pilot = new AttractPilot(3);
        g.Idle(60 * 6);
        g.PutHero(40, 208);
        g.Idle(20);
        Shot(w, c, "conveyors");

        g.JumpTo(3, 2);
        g.Idle(60 * 5);
        Shot(w, c, "elevators");

        g.JumpTo(1, 1);
        Play(g, pilot, 60 * 30);
        Shot(w, c, "rivets");
        w.Close();
    }

    [AvaloniaFact]
    public void The_hammer_and_reaching_the_captive()
    {
        var (w, c, g) = Open(seed: 7);
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(7);
        Play(g, pilot, 60 * 8);
        // Take the hammer on the third girder and swing it at the barrels coming down.
        var hammer = g.Stage.Pickups.First(p => p.IsHammer);
        g.PutHero(hammer.Pos.X - 2, hammer.Pos.Y + 22);
        g.Step(new Controls(0, 0, true));
        for (int i = 0; i < 40; i++) g.Step(default);
        g.Hero.Facing = 1;
        g.PutBarrel(g.Hero.X + 30, g.Hero.Feet, -1);
        for (int i = 0; i < 18; i++) g.Step(default);
        Assert.True(g.HammerLeft > 0);
        Shot(w, c, "hammer");

        var ladder = g.Stage.Ladders.Single(l => l.Upper == g.Stage.Goal);
        g.Quiet();
        g.Idle(60 * 10); // the hammer runs out (no climbing with it)
        g.PutHero(ladder.X, ladder.Bottom);
        for (int i = 0; i < 60 * 3 && g.Phase == Phase.Playing; i++) g.Step(new Controls(0, -1, false));
        Assert.Equal(Phase.StageClear, g.Phase);
        g.Idle(40);
        Shot(w, c, "rescued");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 5 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials)
        {
            g.Initials!.Type('A');
            g.Initials.Type('V');
            Shot(w, c, "initials");
            g.Initials.Type('A');
            g.Idle(5);
        }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
