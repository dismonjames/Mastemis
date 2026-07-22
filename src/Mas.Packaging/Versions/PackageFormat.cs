namespace Mastemis.Mas.Packaging.Versions;

public static class PackageFormat
{
    public const string CurrentVersion = "1.0";
    public static bool IsSupported(string value) => string.Equals(value, CurrentVersion, StringComparison.Ordinal);
}
