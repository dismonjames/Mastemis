using System.Reflection;

namespace Mastemis.Client.Core.Features.About;

public sealed class AboutViewModel
{
    public string Product => "Mastemis";
    public string Author => "Lê Hùng Quang Minh";
    public string Copyright => "Copyright © 2026 Lê Hùng Quang Minh";
    public string License => "Mozilla Public License 2.0 (MPL-2.0)";
    public string Version { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "development";
    public string VersionLabel => $"Version {Version}";
    public string LicenseSummary => "Mastemis source files are distributed under MPL-2.0. Changes to covered files must remain available in Source Code Form under the same license. Third-party components keep their own licenses.";
}
