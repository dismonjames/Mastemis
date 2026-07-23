using Mastemis.Client.Core.Diagnostics;
using Uno.UI.Hosting;

namespace Mastemis.Client;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var host = UnoPlatformHostBuilder.Create()
                .App(() => new App(args))
                .UseX11()
                .UseLinuxFrameBuffer()
                .UseMacOS()
                .UseWin32()
                .Build();
            host.Run();
            return 0;
        }
        catch (Exception error)
        {
            StartupDiagnosticWriter.CreateDefault().WriteFatal(error);
            return 1;
        }
    }
}
