using Avalonia.Input;

namespace AVAKong.Engine;

/// <summary>Keyboard and gamepad boiled down to the controls: left or right, up or down, and jump.</summary>
public sealed class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressed = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key) => _pressed.Add(key);
    public void OnKeyUp(Key key) => _pressed.Remove(key);
    public bool Has(Key key) => _pressed.Contains(key);

    public void Update() => _pad.Update();

    public Controls Controls
    {
        get
        {
            int dx = (Has(Key.Right) || Has(Key.D) || _pad.DPadRight || _pad.LeftStickX > 0.4f ? 1 : 0)
                   - (Has(Key.Left) || Has(Key.A) || _pad.DPadLeft || _pad.LeftStickX < -0.4f ? 1 : 0);
            int dy = (Has(Key.Down) || Has(Key.S) || _pad.DPadDown || _pad.LeftStickY > 0.5f ? 1 : 0)
                   - (Has(Key.Up) || Has(Key.W) || _pad.DPadUp || _pad.LeftStickY < -0.5f ? 1 : 0);
            bool jump = Has(Key.Space) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || Has(Key.Z) || Has(Key.X)
                        || _pad.ButtonA || _pad.ButtonB || _pad.ButtonX;
            return new Controls(dx, dy, jump);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
