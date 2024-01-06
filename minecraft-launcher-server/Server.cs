using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DTLib.Dtsod;
using DTLib.Extensions;
using DTLib.Filesystem;
using DTLib.Logging;
using DTLib.Network;

namespace launcher_server;

static class Server
{
    private static ILogger logger = new CompositeLogger(
        new FileLogger("logs","launcher-server"),
        new ConsoleLogger());
    static readonly Socket mainSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    static DtsodV23 config = null!;
    public static readonly IOPath shared_dir = "public";


    static void Main(string[] args)
    {
        try
        {
            Console.Title = "minecraft_launcher_server";
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;
            
            config = new DtsodV23(File.ReadAllText("minecraft-launcher-server.dtsod"));
            
            logger.LogInfo("Main",  $"local address: {config["local_ip"]}");
            logger.LogInfo("Main", $"public address: {Functions.GetPublicIP()}");
                logger.LogInfo("Main", $"port: {config["local_port"]}");
            mainSocket.Bind(new IPEndPoint(IPAddress.Parse(config["local_ip"]), config["local_port"]));
            mainSocket.Listen(1000);
            Manifests.CreateAllManifests();
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
        logger.LogInfo("Main",  "");
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
                                var file = handlerSocket.GetPackage().BytesToString();
                                logger.LogInfo(nameof(HandleUser), $"updater requested file {file}");
                                // ReSharper disable once InconsistentlySynchronizedField
                                fsp.UploadFile(Path.Concat(shared_dir, file));
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