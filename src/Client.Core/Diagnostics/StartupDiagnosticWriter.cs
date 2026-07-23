using System.Text;

namespace Mastemis.Client.Core.Diagnostics;

public interface IStartupDiagnosticWriter
{
    void WriteFatal(Exception exception);
}

public sealed class StartupDiagnosticWriter(TextWriter standardError, string cacheDirectory) : IStartupDiagnosticWriter
{
    public void WriteFatal(Exception exception)
    {
        var message = $"{DateTimeOffset.UtcNow:O} Mastemis desktop startup failed: {exception.GetType().Name}: {Safe(exception.Message)}";
        standardError.WriteLine(message);
        try
        {
            Directory.CreateDirectory(cacheDirectory);
            File.AppendAllText(Path.Combine(cacheDirectory, "startup.log"), message + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { standardError.WriteLine("Mastemis could not write its startup diagnostic log."); }
    }

    public static StartupDiagnosticWriter CreateDefault() => new(Console.Error, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mastemis", "diagnostics"));

    private static string Safe(string value) => value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 512)];
}
