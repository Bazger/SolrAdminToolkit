using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using Wintellect.PowerCollections;

namespace ReindexAutomation.Client.Cloud
{
    //TODO: Work on Async
    //TODO: Work on InteruptException
    public static class ZkMaintenanceUtils
    {
        private static string ZKNODE_DATA_FILE = "zknode.data";

        //TODO: Check if works
        #region CHECK IF WORKS

        /// <summary>
        /// Lists a ZNode child and (optionally) the znodes of all the children. No data is dumped
        /// </summary>
        /// <param name="zkClient"></param>
        /// <param name="path">The node to remove on Zookeeper</param>
        /// <param name="recurse">Whether to remove children</param>
        /// <returns>An indented list of the znodes suitable for display</returns>
        /// <exception cref="KeeperException"> Could not perform the Zookeeper operation</exception>        
        public static async Task<string> listZnode(SolrZkClient zkClient, string path, bool recurse)
        {
            var root = path;

            if (path.ToLower(CultureInfo.InvariantCulture).StartsWith("zk:"))
            {
                root = path.Substring(3);
            }
            if (root.Equals("/") == false && root.EndsWith("/"))
            {
                root = root.Substring(0, root.Length - 1);
            }

            var sb = new StringBuilder();

            if (recurse == false)
            {
                foreach (var node in await zkClient.getChildren(root, null, true))
                {
                    if (node.Equals("zookeeper") == false)
                    {
                        sb.Append(node).Append(Environment.NewLine);
                    }
                }
                return sb.ToString();
            }

            traverseZkTree(zkClient, root, VISIT_ORDER.VISIT_PRE, znode =>
            {
                if (znode.StartsWith("/zookeeper")) return; // can't do anything with this node!
                int iPos = znode.LastIndexOf("/");
                if (iPos > 0)
                {
                    for (int idx = 0; idx < iPos; ++idx) sb.Append(" ");
                    sb.Append(znode.Substring(iPos + 1)).Append(Environment.NewLine);
                }
                else
                {
                    sb.Append(znode).Append(Environment.NewLine);
                }
            });

            return sb.ToString();
        }

        /// <summary>
        /// Copy between local file system and Zookeeper, or from one Zookeeper node to another, optionally copying recursively.
        /// </summary>
        /// <param name="zkClient"></param>
        /// <param name="src">Source to copy from. Both src and dst may be Znodes. However, both may NOT be local</param>
        /// <param name="srcIsZk"></param>
        /// <param name="dst">The place to copy the files too. Both src and dst may be Znodes. However both may NOT be local</param>
        /// <param name="dstIsZk"></param>
        /// <param name="recurse">If the source is a directory, reccursively copy the contents iff this is true.</param>
        /// <exception cref="ArgumentException">Explanatory exception due to bad params, failed operation, etc.</exception>
        public static async Task zkTransfer(SolrZkClient zkClient, string src, bool srcIsZk, string dst, bool dstIsZk, bool recurse)
        {
            if (srcIsZk == false && dstIsZk == false)
            {
                throw new Exception("One or both of source or destination must specify ZK nodes.");
            }

            // Make sure -recurse is specified if the source has children.
            if (recurse == false)
            {
                if (srcIsZk)
                {
                    if ((await zkClient.getChildren(src, null, true)).Any())
                    {
                        throw new ArgumentException("Zookeeper node " + src + " has children and recurse is false");
                    }
                }
                else if (IsDirectory(src))
                {
                    throw new ArgumentException("Local path " + src + " is a directory and recurse is false");
                }
            }

            if (dstIsZk && dst.Length == 0)
            {
                dst = "/"; // for consistency, one can copy from zk: and send to zk:/
            }
            dst = normalizeDest(src, dst, srcIsZk, dstIsZk);

            // ZK -> ZK copy.
            if (srcIsZk && dstIsZk)
            {
                traverseZkTree(zkClient, src, VISIT_ORDER.VISIT_PRE, async path =>
                {
                    var finalDestination = dst;
                    if (path.Equals(src) == false)
                    {
                        finalDestination += "/" + path.Substring(src.Length + 1);
                    }
                    await zkClient.makePath(finalDestination, false, true);
                    await zkClient.setData(finalDestination, await zkClient.getData(path, null, null, true), true);
                });
                return;
            }

            //local -> ZK copy
            if (dstIsZk)
            {
                uploadToZK(zkClient, src, dst, null);
                return;
            }

            // Copying individual files from ZK requires special handling since downloadFromZK assumes the node has children.
            // This is kind of a weak test for the notion of "directory" on Zookeeper.
            // ZK -> local copy where ZK is a parent node
            if ((await zkClient.getChildren(src, null, true)).Any())
            {
                await downloadFromZK(zkClient, src, dst);
                return;
            }

            // Single file ZK -> local copy where ZK is a leaf node
            if (IsDirectory(dst))
            {
                if (dst.EndsWith(Path.DirectorySeparatorChar.ToString()) == false) dst += Path.DirectorySeparatorChar;
                dst = normalizeDest(src, dst, srcIsZk, dstIsZk);
            }
            byte[] data = await zkClient.getData(src, null, null, true);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            //TODO: Log here
            //log.info("Writing file {}", filename);
            File.WriteAllBytes(dst, data);
        }

        // If the dest ends with a separator, it's a directory or non-leaf znode, so return the
        // last element of the src to appended to the dstName.
        private static string normalizeDest(string srcName, string dstName, bool srcIsZk, bool dstIsZk)
        {
            // Special handling for "."
            if (dstName.Equals("."))
            {
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "."));
            }

            var dstSeparator = (dstIsZk) ? '/' : Path.DirectorySeparatorChar;
            var srcSeparator = (srcIsZk) ? '/' : Path.DirectorySeparatorChar;

            if (dstName.EndsWith(dstSeparator.ToString()))
            { // Dest is a directory or non-leaf znode, append last element of the src path.
                var pos = srcName.LastIndexOf(srcSeparator);
                if (pos < 0)
                {
                    dstName += srcName;
                }
                else
                {
                    dstName += srcName.Substring(pos + 1);
                }
            }

            //TODO: Log here
            //log.info("copying from '{}' to '{}'", srcName, dstName);
            return dstName;
        }


        public static async Task moveZnode(SolrZkClient zkClient, string src, string dst)
        {
            String destName = normalizeDest(src, dst, true, true);

            // Special handling if the source has no children, i.e. copying just a single file.
            if (!(await zkClient.getChildren(src, null, true)).Any())
            {
                await zkClient.makePath(destName, false, true);
                await zkClient.setData(destName, await zkClient.getData(src, null, null, true), true);
            }
            else
            {
                traverseZkTree(zkClient, src, VISIT_ORDER.VISIT_PRE, async path =>
                {
                    var finalDestination = dst;
                    if (path.Equals(src) == false)
                    {
                        finalDestination += "/" + path.Substring(src.Length + 1);
                    }
                    await zkClient.makePath(finalDestination, false, true);
                    await zkClient.setData(finalDestination, await zkClient.getData(path, null, null, true), true);
                });
            }

            // Insure all source znodes are present in dest before deleting the source.
            // throws error if not all there so the source is left intact. Throws error if source and dest don't match.
            await checkAllZnodesThere(zkClient, src, destName);

            clean(zkClient, src);
        }


        // Insure that all the nodes in one path match the nodes in the other as a safety check before removing
        // the source in a 'mv' command.
        private static async Task checkAllZnodesThere(SolrZkClient zkClient, string src, string dst)
        {
            foreach (var node in await zkClient.getChildren(src, null, true))
            {
                if (await zkClient.exists(dst + "/" + node, true) == false)
                {
                    throw new Exception("mv command did not move node " + dst + "/" + node + " source left intact");
                }
                await checkAllZnodesThere(zkClient, src + "/" + node, dst + "/" + node);
            }
        }

        #endregion

        // This not just a copy operation since the config manager takes care of construction the znode path to configsets
        public static async Task downConfig(SolrZkClient zkClient, string confName, string confPath)
        {
            var manager = new ZkConfigManager(zkClient);

            // Try to download the configset
            await manager.downloadConfigDir(confName, confPath);
        }

        // This not just a copy operation since the config manager takes care of construction the znode path to configsets
        public static void upConfig(SolrZkClient zkClient, string confPath, string confName)
        {
            ZkConfigManager manager = new ZkConfigManager(zkClient);

            // Try to download the configset
            manager.uploadConfigDir(confPath, confName);
        }

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
        /// <summary>
        /// Delete a path and all of its sub nodes
        /// </summary>
        /// <param name="zkClient"></param>
        /// <param name="path"></param>
        /// <param name="filter">For node to be deleted</param>
        /// <returns></returns>
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
            });
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

        private static async Task<bool> isEphemeral(SolrZkClient zkClient, string zkPath)
        {
            var znodeStat = await zkClient.exists(zkPath, null, true);
            return znodeStat.getEphemeralOwner() != 0;
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

        public static async Task downloadFromZK(SolrZkClient zkClient, string zkPath, string filePath)
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
                        await downloadFromZK(zkClient, zkChild, Path.Combine(filePath, child));
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

        /// <summary>
        /// Recursively visit a zk tree rooted at path and apply the given visitor to each path. Exists as a separate method
        /// because some of the logic can get nuanced.
        /// </summary>
        /// <param name="zkClient"></param>
        /// <param name="path">The path to start from</param>
        /// <param name="visitOrder">Whether to call the visitor at the at the ending or beginning of the run.</param>
        /// <param name="visitor">The operation to perform on each path</param>
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

        public static async Task<IEnumerable<string>> GetTree(SolrZkClient zkCnxn, string zooPath = "/")
        {
            var children = (await zkCnxn.getChildren(zooPath, null, false));
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

        public static bool IsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
                return true;
            }
            return false;
        }
    }
}

