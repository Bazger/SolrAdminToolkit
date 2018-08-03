using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            foreach (var subpath in paths)
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
            //TODO: Log here
            //log.info("Writing file {}", file.toString());
            File.WriteAllBytes(filePath, data);
            return data.Length;
        }

        private static async Task<bool> isEphemeral(SolrZkClient zkClient, string zkPath)
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
                    Directory.CreateDirectory(filePath); // Make parent dir.
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


        // This not just a copy operation since the config manager takes care of construction the znode path to configsets
        public static void upConfig(SolrZkClient zkClient, string confPath, string confName)
        {
            ZkConfigManager manager = new ZkConfigManager(zkClient);

            // Try to download the configset
            manager.uploadConfigDir(confPath, confName);
        }

        public static void uploadToZK(SolrZkClient zkClient, string fromPath, string zkPath, Regex filenameExclusions)
        {

            var path = fromPath;
            if (path.EndsWith("*"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            var rootPath = path;
            if (!Directory.Exists(rootPath))
            {
                throw new IOException("Path " + rootPath + " does not exist");
            }
            WalkFileTree(rootPath, async (file) =>
            {
                var fielName = Path.GetFileName(file);
                if (filenameExclusions != null && filenameExclusions.Match(fielName ?? throw new InvalidOperationException("File name is empty")).Success)
                {
                    //TODO: Log here
                    //log.info("uploadToZK skipping '{}' due to filenameExclusions '{}'", filename, filenameExclusions);
                    return;
                }
                var zkNode = createZkNodeName(zkPath, rootPath, file);
                try
                {
                    // if the path exists (and presumably we're uploading data to it) just set its data
                    if (Path.GetFileName(file).Equals(ZKNODE_DATA_FILE) && (await zkClient.exists(zkNode, true)))
                    {
                        await zkClient.setData(zkNode, file, true);
                    }
                    else
                    {
                        //Can't work async because it will try to create same path
                        zkClient.makePath(zkNode, file, false, true).Wait();
                    }
                }
                catch (KeeperException ex)
                {
                    throw new Exception("Error uploading file " + file + " to zookeeper path " + zkNode, SolrZkClient.checkInterrupted(ex));
                }
            }
        );
        }

        public static void WalkFileTree(string rootPath, Action<string> operationOnFile)
        {
            foreach (var file in Directory.GetFiles(rootPath))
            {
                operationOnFile(file);
            }

            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                WalkFileTree(dir, operationOnFile);
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

        // Take into account Windows file separators when making a Znode's name.
        public static string createZkNodeName(string zkRoot, string root, string file)
        {
            var relativePath = GetRelativePath(file, root);
            // Windows shenanigans
            if ('\\'.Equals(Path.DirectorySeparatorChar))
            {
                relativePath = relativePath.Replace("\\", "/");
            }
            // It's possible that the relative path and file are the same, in which case
            // adding the bare slash is A Bad Idea unless it's a non-leaf data node
            var isNonLeafData = file.Equals(ZKNODE_DATA_FILE);
            if (relativePath.Length == 0 && isNonLeafData == false) return zkRoot;

            // Important to have this check if the source is file:whatever/ and the destination is just zk:/
            if (zkRoot.EndsWith("/") == false) zkRoot += "/";

            var ret = zkRoot + relativePath;

            // Special handling for data associated with non-leaf node.
            if (isNonLeafData)
            {
                // special handling since what we need to do is add the data to the parent.
                ret = ret.Substring(0, ret.IndexOf(ZKNODE_DATA_FILE, StringComparison.Ordinal));
                if (ret.EndsWith("/"))
                {
                    ret = ret.Substring(0, ret.Length - 1);
                }
            }
            return ret;
        }

        private static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

