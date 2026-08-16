using System.Diagnostics;
using Terminal.Gui.Drivers;

namespace Refedle.App.Views;

/// <summary>
/// Translates raw key input into <see cref="VimAction"/> values.
/// Handles stateful detection of the 'gg' two-key sequence with a 1000 ms timeout
/// that matches vim's default <c>timeoutlen</c>.
/// </summary>
internal sealed class VimKeyTranslator
{
    /// <summary>
    /// Default timeout in milliseconds for the 'gg' sequence, matching vim's timeoutlen default.
    /// </summary>
    internal const int DefaultTimeoutMs = 1000;

    private readonly Func<long> _timestampProvider;
    private readonly int _timeoutMs;

    private bool _pendingG;
    private long _pendingGTimestamp;

    /// <summary>
    /// Initializes a new instance of <see cref="VimKeyTranslator"/> using real wall-clock time.
    /// </summary>
    internal VimKeyTranslator()
        : this(Stopwatch.GetTimestamp, DefaultTimeoutMs) { }

    /// <summary>
    /// Initializes a new instance of <see cref="VimKeyTranslator"/> with injectable time source.
    /// Intended for unit testing only.
    /// </summary>
    internal VimKeyTranslator(Func<long> timestampProvider, int timeoutMs)
    {
        _timestampProvider = timestampProvider;
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Translates a key press into a <see cref="VimAction"/>.
    /// In Terminal.Gui v2, modifier flags are encoded directly in <paramref name="keyCode"/>
    /// (e.g. Shift+G arrives as <c>KeyCode.G | KeyCode.ShiftMask</c>).
    /// </summary>
    /// <param name="keyCode">The raw key code, including any modifier mask bits.</param>
    /// <returns>The resolved vim navigation action.</returns>
    internal VimAction Translate(KeyCode keyCode)
    {
        // Shift+G → GoToEnd; always clears any pending 'g' state
        if (keyCode == (KeyCode.G | KeyCode.ShiftMask))
        {
            _pendingG = false;
            return VimAction.GoToEnd;
        }

        // Second 'g' while pending — check timeout
        if (keyCode == KeyCode.G && _pendingG)
        {
            var elapsed = Stopwatch.GetElapsedTime(_pendingGTimestamp, _timestampProvider());
            _pendingG = false;

            if (elapsed.TotalMilliseconds <= _timeoutMs)
            {
                return VimAction.GoToFirst;
            }

            // Timeout exceeded — treat as a new first 'g'
            _pendingG = true;
            _pendingGTimestamp = _timestampProvider();
            return VimAction.PendingGSequence;
        }

        // First 'g' — enter pending state
        if (keyCode == KeyCode.G)
        {
            _pendingG = true;
            _pendingGTimestamp = _timestampProvider();
            return VimAction.PendingGSequence;
        }

        // Any other key resets pending state
        _pendingG = false;

        // Only unshifted h/j/k/l/d/u trigger vim moves
        if ((keyCode & KeyCode.ShiftMask) != KeyCode.Null)
        {
            return VimAction.None;
        }

        return TranslateBasicKey(keyCode);
    }

    private static VimAction TranslateBasicKey(KeyCode keyCode) =>
        keyCode switch
        {
            KeyCode.H => VimAction.MoveLeft,
            KeyCode.J => VimAction.MoveDown,
            KeyCode.K => VimAction.MoveUp,
            KeyCode.L => VimAction.MoveRight,
            KeyCode.D => VimAction.PageDown,
            KeyCode.U => VimAction.PageUp,
            _ => VimAction.None,
        };
}
