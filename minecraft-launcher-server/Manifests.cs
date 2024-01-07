using System.Linq;
using DTLib.XXHash;
using static launcher_server.Server;

namespace launcher_server;

public static class Manifests
{
    public static object manifestLocker = new();

    public static void CreateManifest(IOPath dir)
    {
        if(!Directory.Exists(dir))
        {
            Directory.Create(dir);
            return;
        }
        
        StringBuilder manifestBuilder = new();
        var manifestPath = Path.Concat(dir, "manifest.dtsod");
        if (Directory.GetFiles(dir).Contains(manifestPath))
            File.Delete(manifestPath);
        foreach (var fileInDir in Directory.GetAllFiles(dir))
        {
            var fileRelative = fileInDir.RemoveBase(dir);
            manifestBuilder.Append(fileRelative);
            manifestBuilder.Append(": \"");
            var fileStream = File.OpenRead(fileInDir);
            ulong hash = xxHash64.ComputeHash(fileStream);
            fileStream.Close();
            string hashStr = BitConverter.GetBytes(hash).HashToString();
            manifestBuilder.Append(hashStr);
            manifestBuilder.Append("\";\n");
        }
        File.WriteAllText(manifestPath, manifestBuilder.ToString().Replace('\\','/'));
    }
    
    public static void CreateAllManifests()
    {
        lock (manifestLocker)
        {
            var sync_and_remove_dir = Path.Concat(shared_dir, "sync_and_remove");
            CreateManifest(Path.Concat(shared_dir, "download_if_not_exist"));
            CreateManifest(Path.Concat(shared_dir, "sync_always"));
            if (!Directory.Exists(sync_and_remove_dir))
                Directory.Create(sync_and_remove_dir);
            else foreach (var dir in Directory.GetDirectories(sync_and_remove_dir))
                CreateManifest(dir);
            StringBuilder dirlist_content_builder = new("dirs: [\n");

            var dirs = Directory.GetDirectories(sync_and_remove_dir);
            for (var i = 0; i < dirs.Length-1; i++)
            {
                dirlist_content_builder
                    .Append("\t\"")
                    .Append(dirs[i].RemoveBase(sync_and_remove_dir).Str.Replace('\\','/'))
                    .Append("\",\n");
            }
            dirlist_content_builder
                .Append("\t\"")
                .Append(dirs[^1].RemoveBase(sync_and_remove_dir).Str.Replace('\\','/'))
                .Append("\"\n");

            dirlist_content_builder.Append("];");
            File.WriteAllText(Path.Concat(sync_and_remove_dir, "dirlist.dtsod"), dirlist_content_builder.ToString());
        }
    }
}