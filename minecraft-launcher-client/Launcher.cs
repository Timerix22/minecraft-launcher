using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DTLib.Console;
using DTLib.Logging;
using DTLib.Filesystem;

namespace launcher_client;

internal static partial class Launcher
{
    public static bool Debug, Offline, Updated;
    
    private static void Main(string[] args)
    {
        // console arguments parsing
#if  DEBUG
        Debug = true;
#else
        if (args.Contains("debug"))
            Debug = true;
#endif
        if (args.Contains("offline"))
            Offline = true;
        if (args.Contains("updated"))
            Updated = true;
        
        // console initialization
        Console.Title = "anarx_2";
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        using ConsoleWrapper console = new ConsoleWrapper();
        console.StartUpdating();
        
        // logger initialization
        FileLogger fileLogger = new FileLogger("launcher-logs", "launcher-client");
        ConsoleWrapperLogger consoleWrapperLogger = new ConsoleWrapperLogger(console);
        ILogger Logger = new CompositeLogger(
            fileLogger, 
            consoleWrapperLogger
        );
        Logger.DebugLogEnabled = true; // always print debug log to file
        consoleWrapperLogger.DebugLogEnabled = Debug; 
        
        try
        {
            var config = LauncherConfig.LoadOrCreateDefault();
            NetworkManager networkManager = new NetworkManager(Logger, config.ServerAddress, config.ServerPort);
            
            Logger.LogInfo(nameof(Main), "launcher started");
            
            if(File.Exists("minecraft-launcher.exe_old"))
                File.Delete("minecraft-launcher.exe_old");
            
            // self-update
            if (!Updated && !Offline)
            {
                Logger.LogInfo(nameof(Main), "checking for launcher update");
                networkManager.ConnectToLauncherServer();
                bool updateDownloaded = networkManager.TryDownloadLauncherUpdate("minecraft-launcher.exe_new");
                if(updateDownloaded)
                {
                    System.IO.File.Move("minecraft-launcher.exe", "minecraft-launcher.exe_old");
                    Process.Start("cmd", "/c " +
                                         "move minecraft-launcher.exe_new minecraft-launcher.exe && " +
                                         "minecraft-launcher.exe updated");
                    return;
                }
                networkManager.DisconnectFromLauncherServer();
            }

            // если уже обновлён

            console.SetHeader($"username: {config.Username} | game memory: {config.GameMemory}M");
            console.SetFooter("[L] launch game | [N] change username | [H] help");
            console.DrawGui();

            while (true)
            {
                try
                {
                    var pressedKey = Console.ReadKey(true); // Считывание ввода
                    switch (pressedKey.Key)
                    {
                        case ConsoleKey.UpArrow:
                            console.ScrollUp();
                            break;
                        case ConsoleKey.DownArrow:
                            console.ScrollDown();
                            break;
                        case ConsoleKey.PageUp:
                            console.ScrollUp(console.TextAreaH);
                            break;
                        case ConsoleKey.PageDown:
                            console.ScrollDown(console.TextAreaH);
                            break;
                        case ConsoleKey.N:
                            // todo ReadLine wrapper
                            Console.SetCursorPosition(0, Console.WindowHeight - 1);
                            Console.CursorVisible = true;
                            string? _username = Console.ReadLine();
                            
                            if (_username == null || _username.Length < 2)
                                throw new Exception("too short username");
                            config.Username = _username;
                            config.Save();
                            
                            console.DrawGui();
                            break;
                        case ConsoleKey.L:
                            if (config.Username.Length < 2)
                                throw new Exception("username is too short");

                            // обновление клиента
                            if (!Offline)
                            {
                                networkManager.ConnectToLauncherServer();
                                networkManager.UpdateGame();
                            }

                            // запуск майнкрафта
                            Logger.LogInfo(nameof(Main), "launching minecraft");
                            string gameOptions = ConstructGameLaunchArgs(config.Username,
                                NameUUIDFromString("OfflinePlayer:" + config.Username),
                                config.GameMemory,
                                config.GameWindowWidth,
                                config.GameWindowHeight,
                                Directory.GetCurrent());
                            Logger.LogDebug("LaunchGame", gameOptions);
                            var gameProcess = Process.Start(config.JavaPath.Str, gameOptions);
                            gameProcess?.WaitForExit();
                            Logger.LogInfo(nameof(Main), "minecraft closed");
                            break;
                        case ConsoleKey.H:
                            console.WriteLine("help:");
                            console.WriteLine("  Q: How to use this launcher?");
                            console.WriteLine("  A: Set username if it isn't set and launch the game.");
                            console.WriteLine("  Q: How to change game memory and other settings?");
                            console.WriteLine("  A: Edit the `minecraft-launcher.dtsod` file.");
                            console.WriteLine("  Q: How to disable game files update on launch?");
                            console.WriteLine("  A: Restart the launcher with argument 'offline'.");
                            console.WriteLine("  Q: What to do if launcher doesn't work?");
                            console.WriteLine("  A: Send latest log file from `launcher-logs/` to Timerix.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(nameof(Main), ex);
                }
            }
        }
        catch (Exception ex)
        {
            console.StopUpdating();
            Logger.LogError(nameof(Main), ex);
            ColoredConsole.Write("gray", "press any key to close...");
            Console.ReadKey();
        }
        
        Console.ResetColor();
    }

    //minecraft player uuid explanation
    //https://gist.github.com/CatDany/0e71ca7cd9b42a254e49/
    //java uuid generation in c#
    //https://stackoverflow.com/questions/18021808/uuid-interop-with-c-sharp-code
    public static string NameUUIDFromString(string input)
        => NameUUIDFromBytes(Encoding.UTF8.GetBytes(input));
    
    public static string NameUUIDFromBytes(byte[] input)
    {
        var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(input);
        hash[6] &= 0x0f;
        hash[6] |= 0x30;
        hash[8] &= 0x3f;
        hash[8] |= 0x80;
        string hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        return hex.Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-");
    }
}