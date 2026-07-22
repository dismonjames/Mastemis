using Mastemis.Application;
using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Infrastructure.Storage.SourceRevisions;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Queue;

public sealed class WorkerJobPayloadService(MastemisDbContext db, IClock clock, WorkerJobPayloadOptions options)
{
    public async Task<WorkerJudgeContract> GetContractAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        CancellationToken cancellationToken)
    {
        var job = await OwnedJobAsync(workerId, jobId, leaseId, cancellationToken);
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == job.SubmissionId, cancellationToken);
        var profile = await db.ProblemJudgeProfiles.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProblemId == submission.ProblemId, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Judge profile not found.");
        var tests = await db.ProblemTestCases.AsNoTracking().Where(x => x.ProblemId == submission.ProblemId)
            .OrderBy(x => x.TestIndex).Select(x => new WorkerTestContract(x.TestIndex, x.CheckerId, x.InputBytes, x.ExpectedBytes))
            .ToListAsync(cancellationToken);
        var limits = new ResourceLimits(TimeSpan.FromMilliseconds(profile.CpuMilliseconds),
            TimeSpan.FromMilliseconds(profile.WallMilliseconds), profile.MemoryBytes, profile.OutputBytes,
            profile.FileBytes, profile.ProcessCount, profile.TestCount,
            TimeSpan.FromMilliseconds(profile.CompilationMilliseconds), profile.CompilationOutputBytes);
        limits.Validate();
        if (tests.Count == 0 || tests.Count > limits.TestCount)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Problem test data is invalid.");
        return new(jobId, new(job.SubmissionId), submission.Language, limits, tests);
    }

    public async Task<Stream> OpenSourceAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        CancellationToken cancellationToken)
    {
        var job = await OwnedJobAsync(workerId, jobId, leaseId, cancellationToken);
        var objectId = await (from submission in db.Submissions.AsNoTracking()
                              join revision in db.SourceRevisions.AsNoTracking() on submission.RevisionId equals revision.Id
                              where submission.Id == job.SubmissionId
                              select revision.ObjectId).SingleAsync(cancellationToken);
        if (!SourceObjectPath.IsGeneratedSourceObject(objectId)) throw Unsafe();
        return OpenRead(SourceObjectPath.Resolve(options.SourceRoot, objectId));
    }

    public async Task<Stream> OpenTestDataAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        int testIndex, bool expected, CancellationToken cancellationToken)
    {
        var job = await OwnedJobAsync(workerId, jobId, leaseId, cancellationToken);
        var problemId = await db.Submissions.AsNoTracking().Where(x => x.Id == job.SubmissionId)
            .Select(x => x.ProblemId).SingleAsync(cancellationToken);
        var test = await db.ProblemTestCases.AsNoTracking().SingleOrDefaultAsync(
            x => x.ProblemId == problemId && x.TestIndex == testIndex, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Test data not found.");
        var objectId = expected ? test.ExpectedObjectId : test.InputObjectId;
        return OpenRead(JudgeDataPath.Resolve(options.TestDataRoot, objectId));
    }

    private async Task<JudgeJobRow> OwnedJobAsync(JudgeWorkerId workerId, JudgeJobId jobId, Guid leaseId,
        CancellationToken cancellationToken)
    {
        var job = await db.JudgeJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Judge job not found.");
        if (job.WorkerId != workerId.Value || job.LeaseId != leaseId || job.LeaseExpiresAtUtc <= clock.UtcNow ||
            job.State is not ((int)JudgeJobState.Claimed) and not ((int)JudgeJobState.Running))
            throw new ApplicationFailure(ErrorCodes.LeaseRejected, "Worker does not own the active job lease.");
        return job;
    }

    private static FileStream OpenRead(string path)
    {
        try { return new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan); }
        catch (FileNotFoundException) { throw new ApplicationFailure(ErrorCodes.NotFound, "Judge data object not found."); }
    }

    private static ApplicationFailure Unsafe() => new(ErrorCodes.InvalidInput, "Unsafe judge data object identifier.");
}

public sealed record WorkerJobPayloadOptions(string SourceRoot, string TestDataRoot);

internal static class JudgeDataPath
{
    public static string Resolve(string rootPath, string objectId)
    {
        if (!objectId.StartsWith("judge/", StringComparison.Ordinal) || !objectId.EndsWith(".bin", StringComparison.Ordinal) ||
            objectId[6..^4].Length != 32 || !objectId[6..^4].All(Uri.IsHexDigit))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Unsafe judge data object identifier.");
        var root = Path.GetFullPath(rootPath);
        var path = Path.GetFullPath(Path.Combine(root, objectId.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Unsafe judge data object identifier.");
        return path;
    }
}
