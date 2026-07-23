using System.Diagnostics;

namespace Mastemis.VisualReview;

internal sealed class KWinReviewSession : IDisposable
{
    private readonly bool available;
    public bool IsAvailable => available;

    private KWinReviewSession(bool available) => this.available = available;

    public static KWinReviewSession Begin()
    {
        var available = Run("true");
        return new(available);
    }

    public void PrepareCapture()
    {
        if (available) Run("true");
    }

    public void Dispose()
    {
        if (available) Run("false");
    }

    private static bool Run(string value)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("qdbus6")
            {
                UseShellExecute = false,
                ArgumentList = { "org.kde.KWin", "/KWin", "org.kde.KWin.showDesktop", value }
            });
            if (process is null) return false;
            process.WaitForExit(3000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
