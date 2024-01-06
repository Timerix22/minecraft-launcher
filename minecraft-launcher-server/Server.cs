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
using System.Diagnostics;
using DTLib.Ben.Demystifier;
using Timer = DTLib.Timer;

namespace launcher_server;

static class Server
{
    private static ILogger logger = new CompositeLogger(
        new FileLogger("logs","launcher-server"),
        new ConsoleLogger());
    static readonly Socket mainSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    static ServerConfig Config = null!;
    public static readonly IOPath shared_dir = "public";


    static void Main(string[] args)
    {
        Timer? updateCheckTimer = null;
        try
        {
            Console.Title = "minecraft_launcher_server";
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Config = ServerConfig.LoadOrCreateDefault();
            
            CheckUpdates();
            // check for updates every minute
            updateCheckTimer = new Timer(true, 60 * 1000, CheckUpdates);
            updateCheckTimer.Start();
            
            
            logger.LogInfo("Main",  $"local address: {Config.LocalIp}");
            logger.LogInfo("Main", $"public address: {Functions.GetPublicIP()}");
                logger.LogInfo("Main", $"port: {Config.LocalPort}");
            mainSocket.Bind(new IPEndPoint(IPAddress.Parse(Config.LocalIp), Config.LocalPort));
            mainSocket.Listen(1000);
            
            logger.LogInfo("Main", "server started succesfully");
            // запуск отдельного потока для каждого юзера
            logger.LogInfo("Main", "waiting for users");
            while (true)
            {
                var userSocket = mainSocket.Accept();
                var userThread = new Thread(obj => HandleUser((Socket)obj!));
                userThread.Start(userSocket);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Main", ex);
            mainSocket.Close();
        }
        Console.ResetColor();
    }

    static void CheckUpdates()
    {
        logger.LogInfo(nameof(CheckUpdates), "checking for updates...");
        IOPath updatesDir = "updates";
        Directory.Create(updatesDir);
        var updatedFiles = Directory.GetAllFiles(updatesDir);
        foreach (var updatedFilePath in updatedFiles)
        {
            try
            {
                var relativeFilePath = updatedFilePath.RemoveBase(updatesDir);
                if (relativeFilePath.Str is "minecraft-launcher-server" or "minecraft-launcher-server.exe")
                {
                    logger.LogWarn(nameof(CheckUpdates), "program update found, restarting...");
                    string exeFile = relativeFilePath.Str;
                    string exeFileNew = exeFile + "_new";
                    File.Move(updatedFilePath, exeFileNew, true);
                    if(Environment.OSVersion.Platform == PlatformID.Win32NT)
                        Process.Start("cmd",$"/c move {exeFileNew} {exeFile} && {exeFile}");
                    else 
                        File.Move(exeFileNew, exeFile, true);
                    Environment.Exit(0);
                }
                
                logger.LogDebug(nameof(CheckUpdates), "updating file "+relativeFilePath);
                File.Move(updatedFilePath, relativeFilePath, true);
            }
            catch (Exception e)
            {
                logger.LogError(nameof(CheckUpdates), $"failed update of file '{updatedFilePath}'\n"
                    + e.ToStringDemystified());
            }
        }
        if(updatedFiles.Count != 0)
        {
            logger.LogInfo(nameof(CheckUpdates), "creating manifests...");
            Manifests.CreateAllManifests();
            logger.LogInfo(nameof(CheckUpdates), "manifests created");
        }
        logger.LogInfo(nameof(CheckUpdates), "update check completed");
    }
    
    // запускается для каждого юзера в отдельном потоке
    static void HandleUser(Socket handlerSocket)
    {
        logger.LogInfo(nameof(HandleUser), "user connecting...  ");
        try
        {
            // тут запрос пароля заменён запросом заглушки
            handlerSocket.SendPackage("requesting user name");
            string connectionString = handlerSocket.GetPackage().BytesToString();
            FSP fsp = new(handlerSocket);
            
            // запрос от апдейтера
            if (connectionString == "minecraft-launcher")
            {
                logger.LogInfo(nameof(HandleUser), "incoming connection from minecraft-launcher");
                handlerSocket.SendPackage("minecraft-launcher OK");
                // обработка запросов
                while (true)
                {
                    if (handlerSocket.Available >= 2)
                    {
                        string request = handlerSocket.GetPackage().BytesToString();
                        switch (request)
                        {
                            case "requesting launcher update":
                                logger.LogInfo(nameof(HandleUser), "updater requested launcher update");
                                // ReSharper disable once InconsistentlySynchronizedField
                                fsp.UploadFile(Path.Concat(shared_dir, "minecraft-launcher.exe"));
                                break;
                            case "requesting file download":
                                var filePath = handlerSocket.GetPackage().BytesToString();
                                logger.LogInfo(nameof(HandleUser), $"updater requested file {filePath}");
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

            logger.LogWarn(nameof(HandleUser),$"invalid connection string: '{connectionString}'");
            handlerSocket.SendPackage("invalid connection string");
        }
        catch (Exception ex)
        {
            logger.LogWarn(nameof(HandleUser), ex);
        }
        finally
        {
            if (handlerSocket.Connected) handlerSocket.Shutdown(SocketShutdown.Both);
            handlerSocket.Close();
            logger.LogInfo(nameof(HandleUser), "user disconnected");
        }
    }

    
}