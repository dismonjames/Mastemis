using System.Text;
using System.Text.RegularExpressions;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Application.Problems.Assets;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed partial class PostgresProblemAssetStore(MastemisDbContext db, IProblemObjectStorage objects,
    IAdministrationActor actor, IClock clock) : IProblemAssetStore
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    { "image/png", "image/jpeg", "image/gif", "image/webp", "text/plain" };

    public async Task<ProblemAsset> UploadAsync(ProblemId problemId, string logicalName, string contentType, Stream content,
        CancellationToken cancellationToken)
    {
        var (name, normalized) = NormalizeName(logicalName);
        contentType = contentType.Trim().ToLowerInvariant(); if (!AllowedTypes.Contains(contentType)) throw Invalid("Asset content type is not allowed.");
        _ = await db.ProblemDrafts.SingleOrDefaultAsync(x => x.Id == problemId.Value, cancellationToken) ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Problem not found.");
        await using var buffer = new MemoryStream(); await CopyBoundedAsync(content, buffer, cancellationToken); ValidateContent(contentType, buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
        buffer.Position = 0; var staged = await objects.StageAsync(ProblemObjectKind.Asset, buffer, MaximumBytes, cancellationToken); var committed = false;
        try
        {
            var row = new ProblemAssetRow
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId.Value,
                LogicalName = name,
                NormalizedName = normalized,
                ContentType = contentType,
                ObjectId = staged.ObjectId,
                Sha256 = staged.Sha256,
                Length = staged.Length,
                CreatedByUserId = actor.UserId.Value,
                CreatedAtUtc = clock.UtcNow
            };
            db.ProblemAssets.Add(row); db.OutboxMessages.Add(ProblemOutbox.Create("ProblemAssetUpdated", problemId.Value, clock.UtcNow,
                new { problemId = problemId.Value, assetId = row.Id, action = "created" }));
            await db.SaveChangesAsync(cancellationToken); committed = true; await objects.MarkReferencedAsync(staged.ObjectId, cancellationToken); return Map(row);
        }
        catch (DbUpdateException) { if (!committed) await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "An asset with this name already exists."); }
        catch { if (!committed) await objects.DeleteStagedAsync(staged.ObjectId, CancellationToken.None); throw; }
    }

    public async Task<IReadOnlyList<ProblemAsset>> ListAsync(ProblemId problemId, CancellationToken cancellationToken) =>
        await db.ProblemAssets.AsNoTracking().Where(x => x.ProblemId == problemId.Value).OrderBy(x => x.LogicalName)
            .Select(x => Map(x)).ToArrayAsync(cancellationToken);

    public async Task<ProblemAssetContent?> OpenAsync(ProblemId problemId, Guid assetId, CancellationToken cancellationToken)
    {
        var row = await db.ProblemAssets.AsNoTracking().SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Id == assetId, cancellationToken);
        if (row is null) return null; return new(Map(row), await objects.OpenReadAsync(row.ObjectId, MaximumBytes, cancellationToken));
    }

    public async Task DeleteAsync(ProblemId problemId, Guid assetId, CancellationToken cancellationToken)
    {
        var row = await db.ProblemAssets.SingleOrDefaultAsync(x => x.ProblemId == problemId.Value && x.Id == assetId, cancellationToken);
        if (row is null) return; db.ProblemAssets.Remove(row); db.OutboxMessages.Add(ProblemOutbox.Create("ProblemAssetUpdated", problemId.Value, clock.UtcNow,
            new { problemId = problemId.Value, assetId, action = "deleted" })); await db.SaveChangesAsync(cancellationToken);
        await objects.DeleteReferencedAsync(row.ObjectId, cancellationToken);
    }

    private static (string Name, string Normalized) NormalizeName(string value)
    {
        value = value.Trim().Normalize(NormalizationForm.FormC);
        if (!NamePattern().IsMatch(value) || value.Contains("..", StringComparison.Ordinal)) throw Invalid("Asset logical name is invalid.");
        return (value, value.ToUpperInvariant());
    }
    private static async Task CopyBoundedAsync(Stream input, Stream output, CancellationToken ct)
    { var buffer = new byte[81920]; long total = 0; while (true) { var read = await input.ReadAsync(buffer, ct); if (read == 0) return; total += read; if (total > MaximumBytes) throw Invalid("Asset exceeds its size limit."); await output.WriteAsync(buffer.AsMemory(0, read), ct); } }
    private static void ValidateContent(string type, ReadOnlySpan<byte> content)
    {
        var valid = type switch
        {
            "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47 }),
            "image/jpeg" => content.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
            "image/gif" => content.StartsWith("GIF8"u8),
            "image/webp" => content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8),
            "text/plain" => !content.Contains((byte)0),
            _ => false
        };
        if (!valid) throw Invalid("Asset content does not match its declared type.");
    }
    private static ProblemAsset Map(ProblemAssetRow x) => new(x.Id, new(x.ProblemId), x.LogicalName, x.ContentType, x.Sha256, x.Length, new(x.CreatedByUserId), x.CreatedAtUtc);
    private static ApplicationFailure Invalid(string message) => new(ErrorCodes.InvalidInput, message);
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$")]
    private static partial Regex NamePattern();
}
