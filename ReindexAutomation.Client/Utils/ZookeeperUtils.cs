using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace ReindexAutomation.Client.Utils
{
    public static class ZookeeperUtils
    {

        public static async Task<List<string>> GetTree(this ZooKeeper zkCnxn, string zooPath = "/")
        {
            var children = (await zkCnxn.getChildrenAsync(zooPath)).Children;
            var nodes = new List<string> { zooPath };
            if (!children.Any()) { return nodes; }
            if (zooPath.Last() != '/')
            {
                zooPath += "/";
            }
            foreach (var child in children)
            {
                nodes.AddRange(await GetTree(zkCnxn, zooPath + child));
            }
            return nodes;
        }

        public static async Task<List<string>> UpConfig(this ZooKeeper zkCnxn, string zooPath, string localDir)
        {
            //zkCnxn.getACLAsync(zooPath);
            return null;
        }
    }
}
