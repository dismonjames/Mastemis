using Mastemis.Application;
using Mastemis.Application.Problems.ReferenceOutputs;
using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class PostgresReferenceOutputJobScheduler(MastemisDbContext db, IReferenceOutputQueue queue) : IReferenceOutputJobScheduler
{
    public async Task<Guid> ScheduleAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await db.ProblemGenerationOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operationId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Generation operation not found.");
        var revision = await db.ReferenceSolutionRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProblemId == operation.ProblemId && x.IsCurrent && x.Enabled, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.InvalidInput, "An enabled reference solution is required.");
        var sources = await db.ReferenceSolutionSources.AsNoTracking().Where(x => x.RevisionId == revision.Id)
            .OrderBy(x => x.FileName).Select(x => new ReferenceSolutionSource(x.FileName, x.ObjectId, x.Sha256, x.Length))
            .ToArrayAsync(cancellationToken);
        var tests = await (from test in db.GeneratedTests.AsNoTracking()
                           join set in db.GeneratedTestSets.AsNoTracking() on test.TestSetId equals set.Id
                           where set.GenerationOperationId == operationId && !set.Published
                           orderby test.TestIndex
                           select new ReferenceOutputTestCase(test.TestIndex, test.InputObjectId, test.InputSha256, test.InputLength))
            .ToArrayAsync(cancellationToken);
        var problem = await db.ProblemDrafts.AsNoTracking().SingleAsync(x => x.Id == operation.ProblemId, cancellationToken);
        var cpuTime = TimeSpan.FromMilliseconds(problem.TimeLimitMilliseconds);
        var wallTime = TimeSpan.FromMilliseconds(Math.Min(600_000,
            Math.Max(problem.TimeLimitMilliseconds * 2, problem.TimeLimitMilliseconds + 250)));
        var limits = new ResourceLimits(cpuTime, wallTime,
            problem.MemoryLimitBytes, problem.OutputLimitBytes, Math.Max(problem.OutputLimitBytes, 67_108_864), 16,
            tests.Length, TimeSpan.FromSeconds(30), 4_194_304);
        var payload = new ReferenceOutputJobPayload(ReferenceOutputJobPayload.CurrentVersion, Guid.NewGuid(), operationId,
            new ProblemId(operation.ProblemId), operation.DraftVersion, revision.Id, revision.Language, sources, tests,
            limits, TimeSpan.FromTicks(Math.Min(TimeSpan.FromHours(1).Ticks,
                checked(limits.WallTime.Ticks * tests.Length + limits.CompilationTime.Ticks))));
        return await queue.EnqueueAsync(payload, 3, cancellationToken);
    }
}
