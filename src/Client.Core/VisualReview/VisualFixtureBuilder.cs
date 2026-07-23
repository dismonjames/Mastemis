namespace Mastemis.Client.Core.VisualReview;

internal static class VisualFixtureBuilder
{
    internal static readonly DateTimeOffset Clock = new(2026, 7, 23, 8, 30, 0, TimeSpan.Zero);

    public static VisualFixture Create(string scenario, string role, string state, string viewModel,
        params VisualFixtureSection[] sections) => new(scenario, role, state, viewModel, Clock, sections);

    public static VisualFixtureSection Section(string heading, params VisualFixtureValue[] values) => new(heading, values);

    public static VisualFixtureValue Value(string label, object? value, string kind = "text") =>
        new(label, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "—", kind);
}
