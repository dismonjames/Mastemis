namespace Mastemis.VisualReview;

internal sealed record VisualCaptureOptions(
    string ClientPath,
    string OutputDirectory,
    IReadOnlyList<string> Scenarios,
    IReadOnlyList<(int Width, int Height)> Sizes,
    IReadOnlyList<string> Themes,
    string State,
    string? Role,
    double TextScale,
    bool ReducedMotion,
    bool KeyboardSmoke,
    TimeSpan Timeout)
{
    public static VisualCaptureOptions Parse(string[] args)
    {
        var root = Path.GetFullPath(Value(args, "--root") ?? Directory.GetCurrentDirectory());
        var client = Value(args, "--client") ?? Path.Combine(root, "src/Client/bin/Release/net10.0-desktop/Mastemis.Client");
        var output = Value(args, "--output") ?? Path.Combine(Path.GetTempPath(), "mastemis-visual-review");
        var matrix = args.Contains("--complete", StringComparer.OrdinalIgnoreCase);
        var lightMatrix = args.Contains("--light-matrix", StringComparer.OrdinalIgnoreCase);
        var pageMatrix = args.Contains("--page-matrix", StringComparer.OrdinalIgnoreCase);
        var scenario = Value(args, "--route") ?? "onboarding";
        IReadOnlyList<string> scenarios = matrix ? VisualMatrixCatalog.DarkRoutes : lightMatrix ? VisualMatrixCatalog.LightRoutes : [scenario];
        var theme = Value(args, "--theme") ?? "dark";
        IReadOnlyList<string> themes = matrix ? ["dark"] : lightMatrix ? ["light"] : [theme];
        IReadOnlyList<(int Width, int Height)> sizes = matrix || pageMatrix ? VisualMatrixCatalog.DesktopSizes
            : lightMatrix ? [(1366, 768), (1024, 768), (900, 700)]
            : [ParseSize(Value(args, "--size") ?? "1366x768")];
        return new(client, output, scenarios, sizes, themes, Value(args, "--state") ?? "auto",
            Value(args, "--role"), ParseScale(Value(args, "--text-scale")), args.Contains("--reduced-motion"),
            args.Contains("--keyboard-smoke"), TimeSpan.FromSeconds(ParseInt(Value(args, "--timeout"), 20)));
    }

    private static string? Value(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++) if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    private static (int, int) ParseSize(string value)
    {
        var parts = value.Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height))
            throw new ArgumentException("Size must use WIDTHxHEIGHT.");
        return (width, height);
    }

    private static int ParseInt(string? value, int fallback) => int.TryParse(value, out var result) ? result : fallback;
    private static double ParseScale(string? value) => double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 1;
}
