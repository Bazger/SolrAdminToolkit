using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace ReindexAutomation.Client.Utils
{
    public static class ZookeeperExtensions
    {

        public const string ConfigsZkNode = "/configs";

        public static async Task<IEnumerable<string>> GetTree(this ZooKeeper zkCnxn, string zooPath = "/")
        {
            var children = (await zkCnxn.getChildrenAsync(zooPath)).Children;
            var nodes = new List<string>();
            if (!children.Any())
            {
                return nodes;
            }
            if (zooPath.Last() != '/')
            {
                zooPath += "/";
            }
            foreach (var child in children)
            {
                nodes.Add(zooPath + child);
                nodes.AddRange(await GetTree(zkCnxn, zooPath + child));
            }
            return nodes;
        }

        public static async void UpConfig(this ZooKeeper zkCnxn, string configName, string localDir)
        {
            var currentPath = localDir;
            do
            {
                var files = Directory.GetFiles(currentPath);

            } while (Directory.GetDirectories(currentPath).Any());
        }

        public static async Task DownConfig(this ZooKeeper zkCnxn, string configName, string localDir)
        {
            var configPath = ConfigsZkNode + "/" + configName;
            var pathes = await GetTree(zkCnxn, configPath);
            foreach (var path in pathes)
            {
                
                var children = (await zkCnxn.getChildrenAsync(path)).Children;
                var childPath = path.Substring(ConfigsZkNode.Length + 1).Replace('/', '\\');
                var savingPath = Path.Combine(localDir, childPath);
                var savingDir = Path.GetDirectoryName(savingPath);
                if (!Directory.Exists(savingDir))
                {
                    Directory.CreateDirectory(savingDir);
                }
                if (!children.Any())
                {
                    var data = (await zkCnxn.getDataAsync(path)).Data;
                    File.WriteAllBytes(savingPath, data);
                }
            }
        }
    }
}
