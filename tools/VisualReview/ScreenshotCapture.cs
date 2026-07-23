using System.Diagnostics;

namespace Mastemis.VisualReview;

internal static class ScreenshotCapture
{
    public static async Task CaptureAsync(X11WindowController.WindowInfo window, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await RunAsync("scrot", ["--overwrite", "--window", ((long)window.Id).ToString(System.Globalization.CultureInfo.InvariantCulture), destination], cancellationToken);
        if (!File.Exists(destination) || new FileInfo(destination).Length == 0) throw new IOException("Screenshot output is empty.");
        if (!await ContainsCaptureMarkerAsync(destination, cancellationToken))
            throw new InvalidOperationException("Captured pixels do not contain the Mastemis visual-review marker; the window was obscured or incorrect.");
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

    private static async Task<bool> ContainsCaptureMarkerAsync(string path, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("convert") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { path, "-crop", "20x20+0+0", "txt:-" }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start ImageMagick marker validation.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Capture marker validation failed: {error.Trim()}");
        return output.Contains("#FF00FF", StringComparison.OrdinalIgnoreCase);
    }
}
