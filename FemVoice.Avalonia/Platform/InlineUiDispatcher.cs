using FemVoiceStudio.Core.Platform;

namespace FemVoice.Avalonia.Platform;

/// <summary>
/// Runs all dispatched work inline on the calling thread. Used by the headless dashboard smoke and by
/// tests, where no Avalonia UI message loop is pumping. The real app uses <see cref="AvaloniaUiDispatcher"/>.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;
    public void Post(Action action) => action();
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
}
