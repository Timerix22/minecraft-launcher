global using System;
global using System.Collections.Generic;
global using System.Net;
global using System.Net.Sockets;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using DTLib.Dtsod;
global using DTLib.Extensions;
global using DTLib.Filesystem;
global using DTLib.Logging;
global using DTLib.Network;
using Timer = DTLib.Timer;

namespace launcher_server;

static class Server
{
    public static ILogger Logger = new CompositeLogger(
        new FileLogger("logs","launcher-server"),
        new ConsoleLogger());
    static ServerConfig Config = null!;
    public static readonly IOPath shared_dir = "public";

    public static readonly IOPath LatestLauncherVersionFile = "launcher_version.txt";
    public static string LatestLauncherVersion = "0.0.1";
    
    static void Main(string[] args)
    {
        var mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            Console.Title = "minecraft_launcher_server";
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Config = ServerConfig.LoadOrCreateDefault();
            LatestLauncherVersion = File.ReadAllText(LatestLauncherVersionFile);
            
            Logger.LogInfo(nameof(Main), "creating manifests...");
            Manifests.CreateAllManifests();
            Logger.LogInfo(nameof(Main), "manifests created");
            Updates.Check();
            // check for updates every minute
            var updateCheckTimer = new Timer(true, 60 * 1000, Updates.Check);
            updateCheckTimer.Start();
            
            
            Logger.LogInfo("Main",  $"local address: {Config.LocalIp}");
            Logger.LogInfo("Main", $"public address: {Functions.GetPublicIP()}");
                Logger.LogInfo("Main", $"port: {Config.LocalPort}");
            mainSocket.Bind(new IPEndPoint(IPAddress.Parse(Config.LocalIp), Config.LocalPort));
            mainSocket.Listen(1000);
            
            Logger.LogInfo("Main", "server started succesfully");
            // запуск отдельного потока для каждого юзера
            Logger.LogInfo("Main", "waiting for users");
            while (true)
            {
                var userSocket = mainSocket.Accept();
                var userThread = new Thread(obj => HandleUser((Socket)obj!));
                userThread.Start(userSocket);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Main", ex);
            mainSocket.Close();
        }
        Console.ResetColor();
    }

    // запускается для каждого юзера в отдельном потоке
    static void HandleUser(Socket handlerSocket)
    {
        Logger.LogInfo(nameof(HandleUser), "user connecting...  ");
        try
        {
            // тут запрос пароля заменён запросом заглушки
            handlerSocket.SendPackage("requesting user name");
            string connectionString = handlerSocket.GetPackage().BytesToString();
            FSP fsp = new(handlerSocket);
            
            // запрос от апдейтера
            if (connectionString == "minecraft-launcher")
            {
                Logger.LogInfo(nameof(HandleUser), "incoming connection from minecraft-launcher");
                handlerSocket.SendPackage("minecraft-launcher OK");
                // обработка запросов
                while (true)
                {
                    if (handlerSocket.Available >= 2)
                    {
                        string request = handlerSocket.GetPackage().BytesToString();
                        switch (request)
                        {
                            case "requesting latest launcher version":
                                handlerSocket.SendPackage("latest launcher version is "+LatestLauncherVersion);
                                break;
                            case "requesting launcher update":
                                Logger.LogInfo(nameof(HandleUser), "updater requested launcher update");
                                // ReSharper disable once InconsistentlySynchronizedField
                                fsp.UploadFile(Path.Concat(shared_dir, "minecraft-launcher.exe"));
                                break;
                            case "requesting file download":
                                var filePath = handlerSocket.GetPackage().BytesToString();
                                Logger.LogInfo(nameof(HandleUser), $"updater requested file {filePath}");
                                if(filePath.EndsWith("manifest.dtsod"))
                                    lock (Manifests.manifestLocker)
                                    {
                                        fsp.UploadFile(Path.Concat(shared_dir, filePath));
                                    }
                                // ReSharper disable once InconsistentlySynchronizedField
                                else fsp.UploadFile(Path.Concat(shared_dir, filePath));
                                break;
                            default:
                                throw new Exception("unknown request: " + request);
                        }
                    }
                    else Thread.Sleep(50);
                }
            }
            // неизвестный юзер

            Logger.LogWarn(nameof(HandleUser),$"invalid connection string: '{connectionString}'");
            handlerSocket.SendPackage("invalid connection string");
        }
        catch (Exception ex)
        {
            Logger.LogWarn(nameof(HandleUser), ex);
        }
        finally
        {
            if (handlerSocket.Connected) handlerSocket.Shutdown(SocketShutdown.Both);
            handlerSocket.Close();
            Logger.LogInfo(nameof(HandleUser), "user disconnected");
        }
    }

    
}