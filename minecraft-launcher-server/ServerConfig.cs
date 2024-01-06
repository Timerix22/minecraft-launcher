namespace launcher_server;

public class ServerConfig
{
    public static IOPath ConfigFilePath = "minecraft-launcher.dtsod";
    
    public int LocalPort = 25000;
    public string LocalIp = "127.0.0.1";
    
    private ServerConfig(){}
    
    private ServerConfig(DtsodV23 dtsod)
    {
        LocalIp = dtsod["local_ip"];
        LocalPort = dtsod["local_port"];
    }

    private static ServerConfig LoadFromFile() => 
        new(new DtsodV23(File.ReadAllText(ConfigFilePath)));

    public DtsodV23 ToDtsod() =>
        new()
        {
            { "local_ip", LocalIp },
            { "local_port", LocalPort }
        };

    public void Save() => 
        File.WriteAllText(ConfigFilePath, ToDtsod().ToString());

    private static ServerConfig CreateDefault()
    {
        var c = new ServerConfig();
        c.Save();
        return c;
    }
    
    public static ServerConfig LoadOrCreateDefault() =>
        File.Exists(ConfigFilePath)
            ? LoadFromFile()
            : CreateDefault();
}