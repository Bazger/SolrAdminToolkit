using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using org.apache.zookeeper.data;

namespace SolrAdministrationToolKit.Client.Cloud
{
    /// <summary>
    /// All Solr ZooKeeper interactions should go through this class rather than
    /// ZooKeeper. This class handles synchronous connects and reconnections.
    /// </summary>
    public class SolrZkClient : IDisposable
    {
        private const int DefaultClientConnectTimeout = 30000;

        private ZkCmdExecutor _zkCmdExecutor;

        private ZooKeeper keeper;

        public SolrZkClient(string zkServerAddress, int zkClientTimeout = DefaultClientConnectTimeout)
        {
            //TODO: Improve ctor
            _zkCmdExecutor = new ZkCmdExecutor(zkClientTimeout);
            keeper = new ZooKeeper(zkServerAddress, zkClientTimeout, null);
            //TODO: Implement default connection strategy
            //this(zkServerAddress, zkClientTimeout, new DefaultConnectionStrategy(), null);
        }

        public async Task delete(string path, int version, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                await _zkCmdExecutor.RetryOperation(async () =>
                {
                    await keeper.deleteAsync(path, version);
                });
            }
            else
            {
                await keeper.deleteAsync(path, version);
            }
        }

        //TODO: Func here
        //public Watcher wrapWatcher(final Watcher watcher) {

        /// <summary>
        /// Return the stat of the node of the given path. Return null if no such a
        /// node exists.
        /// If the watch is non-null and the call is successful (no exception is thrown),
        /// a watch will be left on the node with the given path. The watch will be
        /// triggered by a successful operation that creates/delete the node or sets
        /// the data on the node.
        /// </summary>
        /// <param name="path">The node path</param>
        /// <param name="watcher">Explicit watcher</param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns>The stat of the node of the given path; return null if no such a node exists.</returns>
        /// <exception cref="KeeperException">If the server signals an error</exception>.
        public async Task<Stat> exists(string path, Watcher watcher, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.existsAsync(path, watcher));
            }
            else
            {
                return await keeper.existsAsync(path, watcher);
            }
        }

        /// <summary>
        /// Returns true if path exists
        /// </summary>
        /// <param name="path">The node path</param>
        /// <param name="retryOnConnLoss">Retry check</param>
        /// <returns>The stat of the node of the given path; return null if no such a node exists.</returns>
        public async Task<bool> exists(string path, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.existsAsync(path, null) != null);
            }
            else
            {
                return (await keeper.existsAsync(path, null)) != null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="watcher"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns>Returns children of the node at the path</returns>
        public async Task<List<string>> getChildren(string path, Watcher watcher, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => (await keeper.getChildrenAsync(path, null)).Children);
            }
            else
            {
                return (await keeper.getChildrenAsync(path, watcher)).Children;
            }
        }

        /**
         * Returns node's data
         */
        public async Task<byte[]> getData(string path, Watcher watcher, Stat stat, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => (await keeper.getDataAsync(path, watcher)).Data);
            }
            else
            {
                return (await keeper.getDataAsync(path, watcher)).Data;
            }
        }

        /**
         * Returns node's state
         */
        public async Task<Stat> setData(string path, byte[] data, int version, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.setDataAsync(path, data, version));
            }
            else
            {
                return await keeper.setDataAsync(path, data, version);
            }
        }

        //TODO: Func here
        //public void atomicUpdate(String path, Function<byte[], byte[]> editor) throws KeeperException, InterruptedException {

        /// <summary>
        ///  Returns path of created node
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="createMode"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task<string> create(string path, byte[] data, CreateMode createMode, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.createAsync(path, data, ZooDefs.Ids.OPEN_ACL_UNSAFE, createMode));
            }
            else
            {
                return await keeper.createAsync(path, data, ZooDefs.Ids.OPEN_ACL_UNSAFE, createMode);
            }
        }


        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// 
        /// e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
        /// group, node exist, each will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task makePath(string path, bool retryOnConnLoss)
        {
            await makePath(path, null, CreateMode.PERSISTENT, retryOnConnLoss);
        }

        public async Task makePath(string path, bool failOnExists, bool retryOnConnLoss)
        {
            await makePath(path, null, CreateMode.PERSISTENT, null, failOnExists, retryOnConnLoss, 0);
        }

        public async Task makePath(string path, string file, bool failOnExists, bool retryOnConnLoss)
        {
            await makePath(path, File.ReadAllBytes(file),
                CreateMode.PERSISTENT, null, failOnExists, retryOnConnLoss, 0);
        }

        public async Task makePath(string path, string file, bool retryOnConnLoss)
        {
            await makePath(path, File.ReadAllBytes(file), retryOnConnLoss);
        }

        public async Task makePath(string path, CreateMode createMode, bool retryOnConnLoss)
        {
            await makePath(path, null, createMode, retryOnConnLoss);
        }


        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data">To set on the last zkNode</param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task makePath(string path, byte[] data, bool retryOnConnLoss)
        {
            await makePath(path, data, CreateMode.PERSISTENT, retryOnConnLoss);
        }


        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// 
        /// e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
        /// group, node exist, each will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data">To set on the last zkNode</param>
        /// <param name="createMode"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task makePath(string path, byte[] data, CreateMode createMode, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, null, retryOnConnLoss);
        }

        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// 
        /// e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
        /// group, node exist, each will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data">To set on the last zkNode</param>
        /// <param name="createMode"></param>
        /// <param name="watcher"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task makePath(string path, byte[] data, CreateMode createMode,
            Watcher watcher, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, watcher, true, retryOnConnLoss, 0);
        }

        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// 
        /// e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
        /// group, node exist, each will be created.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data">To set on the last zkNode</param>
        /// <param name="createMode"></param>
        /// <param name="watcher"></param>
        /// <param name="failOnExists"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task makePath(string path, byte[] data, CreateMode createMode,
            Watcher watcher, bool failOnExists, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, watcher, failOnExists, retryOnConnLoss, 0);
        }

        /// <summary>
        /// Creates the path in ZooKeeper, creating each node as necessary.
        /// 
        /// e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
        /// group, node exist, each will be created.
        /// 
        /// skipPathParts will force the call to fail if the first skipPathParts do not exist already.
        /// 
        /// Note: retryOnConnLoss is only respected for the final node - nodes
        /// before that are always retried on connection loss.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="createMode"></param>
        /// <param name="watcher"></param>
        /// <param name="failOnExists"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <param name="skipPathParts"></param>
        /// <returns></returns>
        public async Task makePath(string path, byte[] data, CreateMode createMode,
            Watcher watcher, bool failOnExists, bool retryOnConnLoss, int skipPathParts)
        {
            //TODO: Log here
            //log.debug("makePath: {}", path);
            var retry = true;

            if (path.StartsWith("/"))
            {
                path = path.Substring(1, path.Length - 1);
            }
            var paths = path.Split('/');
            var sbPath = new StringBuilder();
            for (var i = 0; i < paths.Length; i++)
            {
                var pathPiece = paths[i];
                sbPath.Append("/" + pathPiece);
                if (i < skipPathParts)
                {
                    continue;
                }
                byte[] bytes = null;
                var currentPath = sbPath.ToString();
                Stat isExists = await exists(currentPath, watcher, retryOnConnLoss);
                if (isExists == null || ((i == paths.Length - 1) && failOnExists))
                {
                    var mode = CreateMode.PERSISTENT;
                    if (i == paths.Length - 1)
                    {
                        mode = createMode;
                        bytes = data;
                        if (!retryOnConnLoss) { retry = false; }
                    }
                    try
                    {
                        if (retry)
                        {
                            var finalMode = mode;
                            var finalBytes = bytes;
                            Console.WriteLine("Create: " + currentPath);
                            await _zkCmdExecutor.RetryOperation(async () =>
                            {
                                await keeper.createAsync(currentPath, finalBytes, ZooDefs.Ids.OPEN_ACL_UNSAFE, finalMode);
                            });
                            Console.WriteLine("End: " + currentPath);
                        }
                        else
                        {
                            await keeper.createAsync(currentPath, bytes, ZooDefs.Ids.OPEN_ACL_UNSAFE, mode);
                        }
                    }
                    catch (KeeperException.NodeExistsException e)
                    {
                        Console.WriteLine("EXCEPTION");
                        if (!failOnExists)
                        {
                            // TODO: version ? for now, don't worry about race
                            await setData(currentPath, data, -1, retryOnConnLoss);
                            // set new watch
                            await exists(currentPath, watcher, retryOnConnLoss);
                            return;
                        }

                        // ignore unless it's the last node in the path
                        if (i == paths.Length - 1)
                        {
                            throw e;
                        }
                    }
                    if (i == paths.Length - 1)
                    {
                        // set new watch
                        await exists(currentPath, watcher, retryOnConnLoss);
                    }
                }
                else if (i == paths.Length - 1)
                {
                    Console.WriteLine("Set: " + currentPath);
                    // TODO: version ? for now, don't worry about race
                    await setData(currentPath, data, -1, retryOnConnLoss);
                    // set new watch
                    await exists(currentPath, watcher, retryOnConnLoss);
                }
            }
        }

        public async Task makePath(string zkPath, CreateMode createMode, Watcher watcher, bool retryOnConnLoss)
        {
            await makePath(zkPath, null, createMode, watcher, retryOnConnLoss);
        }


        /// <summary>
        /// Write data to ZooKeeper.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task<Stat> setData(String path, byte[] data, bool retryOnConnLoss)
        {
            return await setData(path, data, -1, retryOnConnLoss);
        }

        /// <summary>
        /// Write file to ZooKeeper - default system encoding used.
        /// </summary>
        /// <param name="path">Path to upload file to e.g. /solr/conf/solrconfig.xml</param>
        /// <param name="file">Path to file to be uploaded</param>
        /// <param name="retryOnConnLoss"></param>
        /// <returns></returns>
        public async Task<Stat> setData(string path, string file, bool retryOnConnLoss)
        {
            //TODO: Log here
            //log.debug("Write to ZooKeeper: {} to {}", file.getAbsolutePath(), path);
            var data = File.ReadAllBytes(file);
            return await setData(path, data, retryOnConnLoss);
        }


        public async Task<List<OpResult>> multi(List<Op> ops, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.multiAsync(ops));
            }
            else
            {
                return await keeper.multiAsync(ops);
            }
        }

        /*
         * Funcs that not implemented:
         * 
         * public void printLayout(String path, int indent, StringBuilder string)
         * public void printLayoutToStdOut()
         * public void printLayoutToStream(PrintStream out)
         * public static String prettyPrint(String input, int indent)
         * private static String prettyPrint(String input)
         * 
         */

        /// <summary>
        ///  Allows package private classes to update volatile ZooKeeper
        /// </summary>
        /// <param name="keeper"></param>
        void updateKeeper(ZooKeeper keeper)
        {
            var oldKeeper = this.keeper;
            this.keeper = keeper;
            oldKeeper?.closeAsync().Wait();
        }

        public async Task closeKeeper(ZooKeeper keeper)
        {
            if (keeper != null)
            {
                await keeper.closeAsync();
            }
        }

        /// <summary>
        /// Validates if zkHost contains a chroot. See http://zookeeper.apache.org/doc/r3.2.2/zookeeperProgrammers.html#ch_zkSessions
        /// </summary>
        /// <param name="zkHost"></param>
        /// <returns></returns>
        public static bool containsChroot(String zkHost)
        {
            return zkHost.Contains("/");
        }

        public static Exception checkInterrupted(Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                Thread.CurrentThread.Interrupt();
            }
            return ex;
        }


        /// <summary>
        /// Set the ACL on a single node in ZooKeeper. This will replace all existing ACL on that node.
        /// </summary>
        /// <param name="path">Path to set ACL on e.g. /solr/conf/solrconfig.xml</param>
        /// <param name="acls">A list of ACLs to be applied</param>
        /// <param name="retryOnConnLoss">True if the command should be retried on connection loss</param>
        /// <returns></returns>
        public async Task<Stat> setACL(string path, List<ACL> acls, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () => await keeper.setACLAsync(path, acls));
            }
            else
            {
                return await keeper.setACLAsync(path, acls);
            }
        }

        /// <summary>
        /// Update all ACLs for a zk tree based on our configured ZkACLProvider
        /// </summary>
        /// <param name="root">The root node to recursively update</param>
        public async Task updateACLs(string root)
        {
            await ZkMaintenanceUtils.traverseZkTree(this, root, VISIT_ORDER.VISIT_POST, async path =>
            {
                try
                {
                    await setACL(path, (await keeper.getACLAsync(path)).Acls, true);
                }
                catch (KeeperException.NoNodeException)
                {
                    // If a node was deleted, don't bother trying to set ACLs on it.
                }
            });
        }


        // Some pass-throughs to allow less code disruption to other classes that use SolrZkClient.
        public async Task clean(string path)
        {
            await ZkMaintenanceUtils.clean(this, path);
        }

        public async Task clean(string path, Predicate<string> nodeFilter)
        {
            await ZkMaintenanceUtils.clean(this, path, nodeFilter);
        }

        public async Task upConfig(string confPath, string confName)
        {
            await ZkMaintenanceUtils.upConfig(this, confPath, confName);
        }

        public async Task<string> listZnode(string path, bool recurse)
        {
            return await ZkMaintenanceUtils.listZnode(this, path, recurse);
        }

        public async Task downConfig(string confName, string confPath)
        {
            await ZkMaintenanceUtils.downConfig(this, confName, confPath);
        }

        public async Task zkTransfer(string src, bool srcIsZk, string dst, bool dstIsZk, bool recurse)
        {
            await ZkMaintenanceUtils.zkTransfer(this, src, srcIsZk, dst, dstIsZk, recurse);
        }

        public async Task moveZnode(string src, string dst)
        {
            await ZkMaintenanceUtils.moveZnode(this, src, dst);
        }

        public async Task uploadToZK(string rootPath, string zkPath, Regex filenameExclusions)
        {
            await ZkMaintenanceUtils.uploadToZK(this, rootPath, zkPath, filenameExclusions);
        }

        public async Task downloadFromZK(string zkPath, string dir)
        {
            await ZkMaintenanceUtils.downloadFromZK(this, zkPath, dir);
        }

        public void Dispose()
        {
            keeper?.closeAsync().Wait();
        }
    }
}
