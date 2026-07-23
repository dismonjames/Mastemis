using Mastemis.Client.Core.Diagnostics;

namespace Mastemis.Client.Tests.Diagnostics;

public sealed class StartupDiagnosticWriterTests
{
    [Fact]
    public void Fatal_diagnostic_is_bounded_and_written_to_stderr_and_cache()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mastemis-startup-test-" + Guid.NewGuid().ToString("N"));
        var stderr = new StringWriter();
        try
        {
            new StartupDiagnosticWriter(stderr, directory).WriteFatal(new InvalidOperationException(new string('x', 900)));
            Assert.Contains("InvalidOperationException", stderr.ToString());
            var log = File.ReadAllText(Path.Combine(directory, "startup.log"));
            Assert.Contains("Mastemis desktop startup failed", log);
            Assert.True(log.Length < 700);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
