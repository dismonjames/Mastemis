namespace Mastemis.Client.Core.VisualReview;

public sealed record VisualFixture(
    string Scenario,
    string Role,
    string State,
    string ExpectedViewModel,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<VisualFixtureSection> Sections)
{
    public const int MaximumValueLength = 512;

    public IEnumerable<VisualFixtureValue> Values => Sections.SelectMany(section => section.Values);
}

public sealed record VisualFixtureSection(string Heading, IReadOnlyList<VisualFixtureValue> Values);

public sealed record VisualFixtureValue(string Label, string Value, string Kind = "text");
