using System.Diagnostics;
using DTLib.Ben.Demystifier;

namespace launcher_server;

static class Updates
{
    public static void Check()
    {
        Server.Logger.LogDebug(nameof(Check), "checking for updates...");
        IOPath updatesDir = "updates";
        Directory.Create(updatesDir);
        var updatedFiles = Directory.GetAllFiles(updatesDir);
        if(updatedFiles.Count != 0) Server.Logger.LogInfo(nameof(Check), $"updated files found in '{updatesDir}'");

        foreach (var f in updatedFiles)
        {
            if(f.Str is "minecraft-launcher-server" or "minecraft-launcher-server.exe")
                SelfUpdate(updatesDir, f);
            return;
        }
        
        foreach (var updatedFilePath in updatedFiles)
        {
            try
            {
                var relativeFilePath = updatedFilePath.RemoveBase(updatesDir);
                if (relativeFilePath == Path.Concat(Server.shared_dir, "launcher_version.txt"))
                    UpdateLauncherVersion(updatedFilePath);
                else
                {
                    Server.Logger.LogInfo(nameof(Check), "updating file " + relativeFilePath);
                    File.Move(updatedFilePath, relativeFilePath, true);
                }
            }
            catch (Exception e)
            {
                Server.Logger.LogError(nameof(Check), $"failed update of file '{updatedFilePath}'\n"
                                                             + e.ToStringDemystified());
            }
        }
        
        if(updatedFiles.Count != 0)
        {
            Server.Logger.LogInfo(nameof(Check), "creating manifests...");
            Manifests.CreateAllManifests();
            Server.Logger.LogInfo(nameof(Check), "manifests created");
        }

        Server.Logger.LogDebug(nameof(Check), "update check completed");
    }

    private static void UpdateLauncherVersion(IOPath updatedFilePath)
    {
        Server.LatestLauncherVersion = File.ReadAllText(updatedFilePath);
        Server.Logger.LogInfo(nameof(Check), "new launcher version: " + Server.LatestLauncherVersion);
        File.Move(updatedFilePath, Server.LatestLauncherVersionFile, true);
    }

    public static void SelfUpdate(IOPath updatesDir, IOPath exeFile)
    {
        Server.Logger.LogWarn(nameof(SelfUpdate), "program update found, restarting...");
        IOPath exeFileNew = exeFile + "_new";
        File.Move(Path.Concat(updatesDir, exeFile), exeFileNew, true);
        if(Environment.OSVersion.Platform == PlatformID.Win32NT)
            Process.Start("cmd",$"/c move {exeFileNew} {exeFile} && {exeFile}");
        else 
            File.Move(exeFileNew, exeFile, true);
        Environment.Exit(0);
    }
}