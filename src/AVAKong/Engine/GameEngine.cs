using AVAKong.Models;

namespace AVAKong.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    /// <summary>"How high can you climb?": the stage's height, before it starts.</summary>
    Intro,
    Playing,
    /// <summary>Hit: the hero spins and falls still.</summary>
    Dying,
    /// <summary>The captive reached: a heart, then the ape carries her off to the next stage.</summary>
    StageClear,
    /// <summary>Every rivet pulled: the ape's floor gives way and he falls.</summary>
    LevelClear,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Walk, Jump, JumpOver, Smash, Item, RivetPop, Bounce, Pound, Throw, Death, StageClear, LevelClear,
    HowHigh, GameOver, ExtraLife, TitleTune, TimeLow,
    MusicGirders, MusicConveyors, MusicElevators, MusicRivets, MusicHammer, AmbienceStop,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 3;
    public const int ExtraLifeAt = 10000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private bool _extraLifeGiven;

    public GameEngine(int seed = 1981, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        Stage = Stage.Build(StageKind.Girders);
        PlaceHero();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Intro;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Level { get; private set; } = 1;
    /// <summary>Which stage of the level's sequence is being played.</summary>
    public int StageIndex { get; private set; }
    public Stage Stage { get; private set; }
    /// <summary>The height of this stage, as the intro screen counts it: 25 metres a stage.</summary>
    public int Metres => (StageIndex + 1) * 25;

    public List<Effect> Effects { get; } = [];

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Step(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                if (startPressed) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Level * 100 + Metres);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract);
    }

    private void Begin(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _extraLifeGiven = false;
        NewEntryIndex = -1;
        Effects.Clear();
        Level = 1;
        StartStage(0);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        Ambience(Sfx.AmbienceStop);
        Effects.Clear();
        Level = 1;
        StageIndex = 0;
        Stage = Stage.Build(StageKind.Girders);
        ClearEnemies();
        PlaceHero();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        Ambience(Sfx.AmbienceStop);
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Ambience(Sfx.AmbienceStop);
    }

    public void Resume()
    {
        if (State == GameState.Paused) State = GameState.Playing;
    }

    private void GameOver()
    {
        Ambience(Sfx.AmbienceStop);
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        if (!_extraLifeGiven && Score >= ExtraLifeAt)
        {
            _extraLifeGiven = true;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    private Sfx _ambience = Sfx.AmbienceStop;

    /// <summary>The background loop: the stage's music, or the hammer's.</summary>
    private void Ambience(Sfx loop)
    {
        if (loop == _ambience) return;
        _ambience = loop;
        Play(loop);
    }

    private void Popup(Vec at, int points, double life = 1) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = life, Text = points.ToString() });

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }
}
