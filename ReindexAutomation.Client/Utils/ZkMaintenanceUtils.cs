using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using Wintellect.PowerCollections;

namespace ReindexAutomation.Client.Utils
{
    public static class ZkMaintenanceUtils
    {
        private static string ZKNODE_DATA_FILE = "zknode.data";

        // yeah, it's recursive :(
        public static void clean(SolrZkClient zkClient, string path)
        {
            traverseZkTree(zkClient, path, VISIT_ORDER.VISIT_POST, async znode =>
            {
                try
                {
                    if (!znode.Equals("/"))
                    {
                        try
                        {
                            await zkClient.delete(znode, -1, true);
                        }
                        catch (KeeperException.NotEmptyException e)
                        {
                            clean(zkClient, znode);
                        }
                    }
                }
                catch (KeeperException.NoNodeException r)
                {
                }
            });
        }

        public static async Task clean(SolrZkClient zkClient, string path, Predicate<string> filter)
        {
            if (filter == null)
            {
                clean(zkClient, path);
                return;
            }

            var paths = new OrderedSet<string>();

            traverseZkTree(zkClient, path, VISIT_ORDER.VISIT_POST, znode =>
            {
                if (!znode.Equals("/") && filter.Invoke(znode)) paths.Add(znode);
            });

            foreach (string subpath in paths)
            {
                if (!subpath.Equals("/"))
                {
                    try
                    {
                        await zkClient.delete(subpath, -1, true);
                    }
                    catch (Exception ex)
                    {
                        if (ex is KeeperException.NotEmptyException || ex is KeeperException.NoNodeException)
                        {
                            //expected
                        }
                    }
                }
            }
        }

        private static async Task<int> copyDataDown(SolrZkClient zkClient, string zkPath, string filePath)
        {
            var data = await zkClient.getData(zkPath, null, null, true);
            if (data == null || data.Length <= 1)
            {
                return 0; // There are apparently basically empty ZNodes.
            }
            File.WriteAllBytes(filePath, data);
            return data.Length;
        }

        private static async Task<bool> isEphemeral(SolrZkClient zkClient, String zkPath)
        {
            Stat znodeStat = await zkClient.exists(zkPath, null, true);
            return znodeStat.getEphemeralOwner() != 0;
        }

        // This not just a copy operation since the config manager takes care of construction the znode path to configsets
        public static void downConfig(SolrZkClient zkClient, string confName, string confPath)
        {
            var manager = new ZkConfigManager(zkClient);

            // Try to download the configset
            manager.downloadConfigDir(confName, confPath);
        }



        public static async void downloadFromZK(SolrZkClient zkClient, string zkPath, string filePath)
        {
            try
            {
                var children = await zkClient.getChildren(zkPath, null, true);
                // If it has no children, it's a leaf node, write the associated data from the ZNode.
                // Otherwise, continue recursing, but write the associated data to a special file if any
                Console.WriteLine("Hello");
                if (!children.Any())
                {
                    // If we didn't copy data down, then we also didn't create the file. But we still need a marker on the local
                    // disk so create an empty file.
                    if (await copyDataDown(zkClient, zkPath, filePath) == 0)
                    {
                        File.Create(filePath);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // Make parent dir.
                                                                                // ZK nodes, whether leaf or not can have data. If it's a non-leaf node and
                                                                                // has associated data write it into the special file.
                    await copyDataDown(zkClient, zkPath, Path.Combine(filePath, ZKNODE_DATA_FILE));

                    foreach (var child in children)
                    {
                        var zkChild = zkPath;
                        if (zkChild.EndsWith("/") == false)
                        {
                            zkChild += "/";
                        }
                        zkChild += child;
                        if (await isEphemeral(zkClient, zkChild))
                        {
                            // Don't copy ephemeral nodes
                            continue;
                        }
                        // Go deeper into the tree now
                        downloadFromZK(zkClient, zkChild, Path.Combine(filePath, child));
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is KeeperException || ex is ThreadInterruptedException)
                {
                    throw new IOException("Error downloading files from zookeeper path " + zkPath + " to " + filePath,
                        SolrZkClient.checkInterrupted(ex));
                }
                throw;
            }
        }

        public static async void traverseZkTree(SolrZkClient zkClient, string path, VISIT_ORDER visitOrder, Action<string> visitor)
        {
            if (visitOrder == VISIT_ORDER.VISIT_PRE)
            {
                visitor.Invoke(path);
            }
            List<string> children;
            try
            {
                children = await zkClient.getChildren(path, null, true);
            }
            catch (KeeperException.NoNodeException ex)
            {
                return;
            }
            foreach (var child in children)
            {
                // we can't do anything to the built-in zookeeper node
                if (path.Equals("/") && child.Equals("zookeeper")) { continue; }
                if (path.StartsWith("/zookeeper")) { continue; }
                if (path.Equals("/"))
                {
                    traverseZkTree(zkClient, path + child, visitOrder, visitor);
                }
                else
                {
                    traverseZkTree(zkClient, path + "/" + child, visitOrder, visitor);
                }
            }
            if (visitOrder == VISIT_ORDER.VISIT_POST)
            {
                visitor.Invoke(path);
            }
        }
    }
}

