using System;
using System.IO;
using System.Runtime.CompilerServices;
using Cosmic.Core;

namespace Cosmic.Desktop;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var platform = new DesktopCosmicPlatform();
#if !DEBUG
        try
        {
#endif
        if (File.Exists("log.txt"))
        {
            File.Delete("log.txt");
        }

        EngineStuff.Engine = new(platform);
        EngineStuff.Engine.Init();
        platform.Run();
#if !DEBUG
        }
        catch (Exception e)
        {
            platform.PrintLog($"FATAL ERROR: report the last lines in log.txt to Amy");
            // platform.PrintLog($"{e.GetType().Name}: {e.Message}");
            platform.PrintLog($"{e.Message}");
            if (e.StackTrace != null)
            {
                platform.PrintLog(e.StackTrace);
            }
        }
#endif
    }
}