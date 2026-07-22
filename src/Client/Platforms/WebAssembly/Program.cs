using System.Runtime.InteropServices.JavaScript;

namespace Mastemis.Client;

public static partial class Program
{
    private static App? app;

    [JSExport]
    public static int Main(string[] args)
    {
        app = new App();
        return 0;
    }
}
