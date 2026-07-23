using Mastemis.Client.Core.Navigation;

namespace Mastemis.Client.Core.Diagnostics;

public sealed record VisualReviewScenario(string Name, ClientRoute Route, string DefaultRole, int? ProblemStudioSection = null);
