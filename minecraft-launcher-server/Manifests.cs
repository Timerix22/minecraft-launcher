using System.Linq;
using System.Text;
using DTLib;
using DTLib.Extensions;
using DTLib.Filesystem;
using static launcher_server.Server;

namespace launcher_server;

public static class Manifests
{
    static object manifestLocker = new();

    public static void CreateManifest(IOPath dir)
    {
        if(!Directory.Exists(dir))
        {
            Directory.Create(dir);
            return;
        }
        
        StringBuilder manifestBuilder = new();
        Hasher hasher = new();
        var manifestPath = Path.Concat(dir, "manifest.dtsod");
        if (Directory.GetFiles(dir).Contains(manifestPath))
            File.Delete(manifestPath);
        foreach (var fileInDir in Directory.GetAllFiles(dir))
        {
            var fileRelative = fileInDir.RemoveBase(dir);
            manifestBuilder.Append(fileRelative);
            manifestBuilder.Append(": \"");
            byte[] hash = hasher.HashFile(Path.Concat(fileInDir));
            manifestBuilder.Append(hash.HashToString());
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
                .Append(dirs[dirs.Length-1].RemoveBase(sync_and_remove_dir).Str.Replace('\\','/'))
                .Append("\"\n");

            dirlist_content_builder.Append("];");
            File.WriteAllText(Path.Concat(sync_and_remove_dir, "dirlist.dtsod"), dirlist_content_builder.ToString());
        }
    }
}