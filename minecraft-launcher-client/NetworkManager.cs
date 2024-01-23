using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using DTLib.Dtsod;
using DTLib.Extensions;
using DTLib.Filesystem;
using DTLib.Logging;
using DTLib.Network;
using DTLib.XXHash;

namespace launcher_client;

public class NetworkManager
{
    public Socket mainSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    public FSP Fsp;
    private ILogger Logger;
    private IPEndPoint ServerEndpoint;

    public NetworkManager(ILogger logger, IPEndPoint serverEndpoint)
    {
        Fsp = new FSP(mainSocket);
        Logger = logger;
        ServerEndpoint = serverEndpoint;
    }
    public NetworkManager(ILogger logger, string serverAddress, int serverPort) 
        : this(logger, new IPEndPoint(IPAddress.Parse(serverAddress), serverPort))
    {}

    // подключение серверу
    public void ConnectToLauncherServer()
    {
        if (mainSocket.Connected)
        {
            Logger.LogInfo(nameof(NetworkManager), "socket is connected already. disconnecting...");
            DisconnectFromLauncherServer();
        }

        while (true)
            try
            {
                Logger.LogInfo(nameof(NetworkManager), $"connecting to server {ServerEndpoint.Address}:{ServerEndpoint.Port}");
                mainSocket.Connect(ServerEndpoint);
                Logger.LogInfo(nameof(NetworkManager), "connection established");
                break;
            }
            catch (SocketException ex)
            {
                Logger.LogError(nameof(NetworkManager), ex);
                Thread.Sleep(2000);
            }

        mainSocket.ReceiveTimeout = 2500;
        mainSocket.SendTimeout = 2500;
        mainSocket.GetAnswer("requesting user name");
        mainSocket.SendPackage("minecraft-launcher");
        mainSocket.GetAnswer("minecraft-launcher OK");
    }

    public void DisconnectFromLauncherServer()
    {
        mainSocket.Shutdown(SocketShutdown.Both);
        mainSocket.Close();
        mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Fsp = new(mainSocket);
    }
    
    /// <summary>
    /// returns true if new update was downloaded
    /// </summary>
    public bool TryDownloadLauncherUpdate(IOPath localFilePath)
    {
        try
        {
            Logger.LogDebug(nameof(NetworkManager), "requesting latest launcher version");
            mainSocket.SendPackage("requesting latest launcher version");
            var answer = mainSocket.GetPackage().BytesToString();
            if (!answer.StartsWith("latest launcher version is ") ||
                !Version.TryParse(answer.Substring(28), out Version latestVersion))
                throw new Exception($"unexpected server answer: '{answer}'");

            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Logger.LogDebug(nameof(NetworkManager),
                $"current version: {currentVersion}; latest version: {latestVersion}");
            if (currentVersion < latestVersion)
            {
                Logger.LogInfo(nameof(NetworkManager), $"downloading launcher update to {localFilePath}");
                mainSocket.SendPackage("requesting launcher update");
                Fsp.DownloadFile(localFilePath);
                Logger.LogInfo(nameof(NetworkManager), "launcher update downloaded");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(NetworkManager), ex);
        }

        return false;
    }
    
    public void DownloadByManifest(IOPath dirOnServer, IOPath dirOnClient, bool overwrite, bool delete_excess)
    {
        var manifestPath = Path.Concat(dirOnServer, "manifest.dtsod");
        Logger.LogDebug(nameof(NetworkManager), manifestPath);
        string manifestContent = Fsp.DownloadFileToMemory(manifestPath).BytesToString();
        Logger.LogDebug(nameof(NetworkManager), manifestContent);
        var manifest = new DtsodV23(manifestContent);
        foreach (var fileOnServerData in manifest)
        {
            IOPath fileOnClient = Path.Concat(dirOnClient, fileOnServerData.Key);
            if (!File.Exists(fileOnClient))
            {
                Logger.LogDebug(nameof(NetworkManager), $"downloading {fileOnClient}");
                Fsp.DownloadFile(Path.Concat(dirOnServer, fileOnServerData.Key), fileOnClient);
            }
            else if (overwrite)
            {
                var fileStream = File.OpenRead(fileOnClient);
                ulong hash = xxHash64.ComputeHash(fileStream);
                fileStream.Close();
                string hashStr = BitConverter.GetBytes(hash).HashToString();
                if (hashStr != fileOnServerData.Value)
                {
                    Logger.LogDebug(nameof(NetworkManager), $"downloading {fileOnClient} (hash {hashStr} != {fileOnServerData.Value})");
                    Fsp.DownloadFile(Path.Concat(dirOnServer, fileOnServerData.Key), fileOnClient);
                }
            }
        }
        // удаление лишних файлов
        if (delete_excess)
        {
            foreach (var file in Directory.GetAllFiles(dirOnClient))
            {
                if (!manifest.ContainsKey(file.RemoveBase(dirOnClient).Str.Replace('\\','/')))
                {
                    Logger.LogDebug(nameof(NetworkManager), $"deleting {file}");
                    File.Delete(file);
                }
            }
        }
    }

    public void UpdateGame()
    {
        //обновление файлов клиента
        Logger.LogInfo(nameof(NetworkManager), "updating client...");
        DownloadByManifest("download_if_not_exist", Directory.GetCurrent(), false, false);
        DownloadByManifest("sync_always", Directory.GetCurrent(), true, false);
        var dirlistDtsod = new DtsodV23(Fsp
            .DownloadFileToMemory(Path.Concat("sync_and_remove","dirlist.dtsod"))
            .BytesToString());
        foreach (string dir in dirlistDtsod["dirs"])
            DownloadByManifest(Path.Concat("sync_and_remove", dir),
                Path.Concat(Directory.GetCurrent(), dir), true, true);
        Logger.LogInfo(nameof(NetworkManager), "client updated");
    }
}