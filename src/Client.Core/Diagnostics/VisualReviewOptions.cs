using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Core.Diagnostics;

public sealed record VisualReviewOptions(
    ClientRoute Route,
    string Role,
    int Width,
    int Height,
    string Theme,
    string Scenario,
    string State,
    int? ProblemStudioSection,
    double TextScale,
    bool ReducedMotion)
{
    public static VisualReviewOptions? Parse(IReadOnlyList<string> arguments, bool enabled)
    {
        var marker = arguments.IndexOf("--visual-review");
        if (marker < 0) return null;
        if (!enabled) throw new InvalidOperationException("Visual review is disabled. Set MASTEMIS_ENABLE_VISUAL_REVIEW=1 for an isolated local review session.");
        if (marker + 1 >= arguments.Count || !VisualReviewScenarioCatalog.TryResolve(arguments[marker + 1], out var scenario))
            throw new ArgumentException("A supported visual-review route is required.");
        var theme = Value(arguments, "--theme") ?? "dark";
        if (!string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Visual-review theme must be dark or light.");
        var role = Value(arguments, "--role") ?? scenario.DefaultRole;
        var state = Value(arguments, "--state") ?? InferState(scenario.Name);
        if (!VisualReviewFixtureCatalog.Roles.Contains(role)) throw new ArgumentException("A supported visual-review role is required.");
        if (!VisualReviewFixtureCatalog.States.Contains(state)) throw new ArgumentException("A supported visual-review state is required.");
        return new(scenario.Route, role,
            Number(arguments, "--width", 1366, 800, 3840), Number(arguments, "--height", 768, 600, 2160),
            theme, scenario.Name, state, scenario.ProblemStudioSection,
            Decimal(arguments, "--text-scale", 1, 1, 2), Flag(arguments, "--reduced-motion"));
    }

    private static string? Value(IReadOnlyList<string> args, string name)
    {
        var index = args.IndexOf(name); return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }
    private static int Number(IReadOnlyList<string> args, string name, int fallback, int minimum, int maximum) =>
        int.TryParse(Value(args, name), out var value) && value >= minimum && value <= maximum ? value : fallback;
    private static double Decimal(IReadOnlyList<string> args, string name, double fallback, double minimum, double maximum) =>
        double.TryParse(Value(args, name), System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum ? value : fallback;
    private static bool Flag(IReadOnlyList<string> args, string name) => args.IndexOf(name) >= 0;
    private static string InferState(string scenario) => scenario.EndsWith("-error", StringComparison.Ordinal) ? "error"
        : scenario.EndsWith("-unavailable", StringComparison.Ordinal) ? "disconnected"
        : scenario.EndsWith("-success", StringComparison.Ordinal) ? "completed"
        : scenario.Contains("terminated", StringComparison.Ordinal) ? "terminated" : "populated";
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++) if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}
