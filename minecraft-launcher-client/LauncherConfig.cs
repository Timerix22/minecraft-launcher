using DTLib.Dtsod;
using DTLib.Filesystem;

namespace launcher_client;

public class LauncherConfig
{
    public static IOPath ConfigFilePath = "minecraft-launcher.dtsod";
    
    public int GameMemory = 3000;
    public int GameWindowHeight = 500;
    public int GameWindowWidth = 900;
    public IOPath JavaPath = "jre/bin/java.exe";
    public string ServerAddress = "127.0.0.1";
    public int ServerPort = 25000;
    public string Username = "";
    
    private LauncherConfig(){}
    
    private LauncherConfig(DtsodV23 dtsod)
    {
        GameMemory = dtsod["gameMemory"];
        GameWindowHeight = dtsod["gameWindowHeight"];
        GameWindowWidth = dtsod["gameWindowWidth"];
        JavaPath = dtsod["javaPath"];
        ServerAddress = dtsod["serverAddress"];
        ServerPort = dtsod["serverPort"];
        Username = dtsod["username"];
    }

    public static LauncherConfig LoadFromFile() => new(new DtsodV23(File.ReadAllText(ConfigFilePath)));

    public DtsodV23 ToDtsod() =>
        new()
        {
            { "gameMemory", GameMemory },
            { "gameWindowHeight", GameWindowHeight },
            { "gameWindowWidth", GameWindowWidth },
            { "javaPath", JavaPath.Str },
            { "serverAddress", ServerAddress },
            { "serverPort", ServerPort },
            { "username", Username },
        };

    public void Save()
    {
        File.WriteAllText(ConfigFilePath, ToDtsod().ToString());
    }
    
    public static LauncherConfig CreateDefault()
    {
        var c = new LauncherConfig();
        c.Save();
        return c;
    }
}