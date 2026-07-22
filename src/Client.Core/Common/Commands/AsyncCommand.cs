using System.Windows.Input;

namespace Mastemis.Client.Core.Common.Commands;

public sealed class AsyncCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null) : ICommand, IDisposable
{
    private CancellationTokenSource? cancellation;
    private bool running;

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        running = true;
        cancellation = new CancellationTokenSource();
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(cancellation.Token).ConfigureAwait(true); }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cancel() => cancellation?.Cancel();
    public void Dispose() => cancellation?.Dispose();
}
