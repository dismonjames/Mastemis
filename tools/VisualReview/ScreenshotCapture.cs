using System.Diagnostics;

namespace Mastemis.VisualReview;

internal static class ScreenshotCapture
{
    public static async Task CaptureAsync(X11WindowController.WindowInfo window, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await RunAsync("scrot", ["--overwrite", "--window", $"0x{window.Id:x}", destination], cancellationToken);
        if (!File.Exists(destination) || new FileInfo(destination).Length == 0) throw new IOException("Screenshot output is empty.");
    }

    private static async Task RunAsync(string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {command}.");
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{command} failed: {error.Trim()}");
    }
}
