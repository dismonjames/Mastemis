namespace Mastemis.Judge.Configuration;

public sealed record JudgeOrchestratorOptions(string Image, TimeSpan TotalTimeout, long MaximumTestDataBytes,
    string JudgeVersion)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Image) || Image.Length > 200 || TotalTimeout <= TimeSpan.Zero ||
            TotalTimeout > TimeSpan.FromHours(2) || MaximumTestDataBytes is < 1 or > 268_435_456 ||
            string.IsNullOrWhiteSpace(JudgeVersion) || JudgeVersion.Length > 100)
            throw new ArgumentException("Judge orchestration configuration is invalid.");
    }
}
