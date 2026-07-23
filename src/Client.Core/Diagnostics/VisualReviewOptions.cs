using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Core.Diagnostics;

public sealed record VisualReviewOptions(ClientRoute Route, string Role, int Width, int Height, string Theme)
{
    public static VisualReviewOptions? Parse(IReadOnlyList<string> arguments, bool enabled)
    {
        var marker = arguments.IndexOf("--visual-review");
        if (marker < 0) return null;
        if (!enabled) throw new InvalidOperationException("Visual review is disabled. Set MASTEMIS_ENABLE_VISUAL_REVIEW=1 for an isolated local review session.");
        if (marker + 1 >= arguments.Count || !TryRoute(arguments[marker + 1], out var route))
            throw new ArgumentException("A supported visual-review route is required.");
        return new(route, Value(arguments, "--role") ?? "Administrator",
            Number(arguments, "--width", 1366, 800, 3840), Number(arguments, "--height", 768, 600, 2160),
            Value(arguments, "--theme") ?? "dark");
    }

    private static string? Value(IReadOnlyList<string> args, string name)
    {
        var index = args.IndexOf(name); return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }
    private static int Number(IReadOnlyList<string> args, string name, int fallback, int minimum, int maximum) =>
        int.TryParse(Value(args, name), out var value) && value >= minimum && value <= maximum ? value : fallback;
    private static bool TryRoute(string value, out ClientRoute route)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, true, out route);
    }
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++) if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}
