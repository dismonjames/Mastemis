using System.Diagnostics;
using System.Text.Json;

namespace Mastemis.VisualReview;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = VisualCaptureOptions.Parse(args);
        Directory.CreateDirectory(options.OutputDirectory);
        var results = new List<CaptureMetadata>();
        using var controller = new X11WindowController();
        using var desktop = KWinReviewSession.Begin();
        Console.WriteLine($"KWin desktop isolation: {(desktop.IsAvailable ? "available" : "unavailable")}");
        foreach (var scenario in options.Scenarios)
            foreach (var theme in options.Themes)
                foreach (var (width, height) in options.Sizes)
                {
                    desktop.PrepareCapture();
                    CaptureMetadata result;
                    var attempt = 0;
                    do
                    {
                        attempt++;
                        result = await CaptureAsync(controller, desktop, options, scenario, theme, width, height);
                        if (!result.CaptureSuccess && attempt < 3) await Task.Delay(500);
                    }
                    while (!result.CaptureSuccess && attempt < 3);
                    results.Add(result);
                    Console.WriteLine($"[{results.Count}] {scenario} {theme} {width}x{height}: {(result.CaptureSuccess ? "captured" : "failed")} (attempt {attempt})");
                }
        var summaryPath = Path.Combine(options.OutputDirectory, "matrix.json");
        await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Visual matrix: {results.Count(x => x.CaptureSuccess)}/{results.Count} valid captures. Metadata: {summaryPath}");
        foreach (var failure in results.Where(x => !x.CaptureSuccess)) Console.Error.WriteLine($"{failure.Route} {failure.Theme} {failure.RequestedWidth}x{failure.RequestedHeight}: {failure.Error}");
        return results.All(x => x.CaptureSuccess) ? 0 : 1;
    }

    private static async Task<CaptureMetadata> CaptureAsync(X11WindowController controller, KWinReviewSession desktop, VisualCaptureOptions options,
        string scenario, string theme, int width, int height)
    {
        var state = options.State == "auto" ? VisualMatrixCatalog.StateFor(scenario) : options.State;
        Process? process = null; X11WindowController.WindowInfo? window = null; string? error = null; var activated = false; var success = false;
        try
        {
            var existingWindows = controller.MastemisWindowIds();
            var start = new ProcessStartInfo(options.ClientPath) { UseShellExecute = false, RedirectStandardError = true };
            start.Environment["MASTEMIS_ENABLE_VISUAL_REVIEW"] = "1";
            foreach (var value in Arguments(scenario, state, options, theme, width, height)) start.ArgumentList.Add(value);
            process = Process.Start(start) ?? throw new InvalidOperationException("Desktop client did not start.");
            window = controller.WaitForNewWindow(existingWindows, options.Timeout) ?? throw new TimeoutException("No new Mastemis.Client X11 window appeared for the launched process.");
            desktop.PrepareCapture();
            await Task.Delay(3000);
            activated = controller.Activate(window);
            if (!activated) { Thread.Sleep(300); activated = controller.Activate(window); }
            if (!activated) throw new InvalidOperationException("Mastemis window could not be made the active foreground window.");
            window = controller.Refresh(window);
            var workspaceConstrained = width == controller.DisplayWidth && height == controller.DisplayHeight &&
                Math.Abs(window.Width - width) <= 4 && window.Height >= height - 96;
            if (!workspaceConstrained && (Math.Abs(window.Width - width) > 24 || Math.Abs(window.Height - height) > 48))
                throw new InvalidOperationException($"Window is {window.Width}x{window.Height}, requested {width}x{height}.");
            if (options.KeyboardSmoke && !controller.SendKeyboardSmoke(window)) throw new InvalidOperationException("Keyboard smoke input could not be delivered.");
            var name = $"{scenario}--{theme}--{width}x{height}--{state}.png";
            if (!controller.Activate(window)) throw new InvalidOperationException("Mastemis window lost foreground activation before capture.");
            await ScreenshotCapture.CaptureAsync(window, Path.Combine(options.OutputDirectory, name), CancellationToken.None);
            success = true;
        }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            if (process is { HasExited: false }) { process.Kill(true); await process.WaitForExitAsync(); }
        }
        return new(scenario, state, options.Role, theme, width, height, window?.Width ?? 0, window?.Height ?? 0,
            options.TextScale, options.ReducedMotion, process?.Id ?? 0, window is null ? string.Empty : $"0x{window.Id:x}",
            activated, success, process is { HasExited: true } ? process.ExitCode : null, DateTimeOffset.UtcNow, error);
    }

    private static IEnumerable<string> Arguments(string scenario, string state, VisualCaptureOptions options, string theme, int width, int height)
    {
        yield return "--visual-review"; yield return scenario; yield return "--state"; yield return state;
        yield return "--theme"; yield return theme; yield return "--width"; yield return width.ToString();
        yield return "--height"; yield return height.ToString(); yield return "--text-scale";
        yield return options.TextScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (options.Role is not null) { yield return "--role"; yield return options.Role; }
        if (options.ReducedMotion) yield return "--reduced-motion";
    }
}
