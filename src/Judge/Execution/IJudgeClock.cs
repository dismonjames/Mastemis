namespace Mastemis.Judge.Execution;

public interface IJudgeClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemJudgeClock : IJudgeClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
