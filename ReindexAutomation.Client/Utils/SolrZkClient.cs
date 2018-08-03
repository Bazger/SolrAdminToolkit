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

namespace ReindexAutomation.Client.Utils
{
    public class SolrZkClient : IDisposable
    {
        const int DefaultClientConnectTimeout = 30000;

        private ZkCmdExecutor _zkCmdExecutor;

        private ZooKeeper keeper;

        public SolrZkClient(string zkServerAddress, int zkClientTimeout = DefaultClientConnectTimeout)
        {
            //TODO: Improve ctor
            _zkCmdExecutor = new ZkCmdExecutor(zkClientTimeout);
            keeper = new ZooKeeper(zkServerAddress, zkClientTimeout, null);
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


        /// <summary>
        /// Returns true if path exists
        /// </summary>
        /// <param name="path">the node path</param>
        /// <param name="retryOnConnLoss">Retry check</param>
        /// <returns>Existing status</returns>
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



        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
         * group, node exist, each will be created.
         */
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

        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * @param data to set on the last zkNode
         */
        public async Task makePath(string path, byte[] data, bool retryOnConnLoss)
        {
            await makePath(path, data, CreateMode.PERSISTENT, retryOnConnLoss);
        }

        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
         * group, node exist, each will be created.
         *
         * @param data to set on the last zkNode
         */
        public async Task makePath(string path, byte[] data, CreateMode createMode, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, null, retryOnConnLoss);
        }

        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
         * group, node exist, each will be created.
         *
         * @param data to set on the last zkNode
         */
        public async Task makePath(string path, byte[] data, CreateMode createMode,
            Watcher watcher, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, watcher, true, retryOnConnLoss, 0);
        }

        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
         * group, node exist, each will be created.
         *
         * @param data to set on the last zkNode
         */
        public async Task makePath(string path, byte[] data, CreateMode createMode,
            Watcher watcher, bool failOnExists, bool retryOnConnLoss)
        {
            await makePath(path, data, createMode, watcher, failOnExists, retryOnConnLoss, 0);
        }

        /**
         * Creates the path in ZooKeeper, creating each node as necessary.
         *
         * e.g. If <code>path=/solr/group/node</code> and none of the nodes, solr,
         * group, node exist, each will be created.
         * 
         * skipPathParts will force the call to fail if the first skipPathParts do not exist already.
         *
         * Note: retryOnConnLoss is only respected for the final node - nodes
         * before that are always retried on connection loss.
         */
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

        /**
         * Write data to ZooKeeper.
         */
        public async Task<Stat> setData(String path, byte[] data, bool retryOnConnLoss)
        {
            return await setData(path, data, -1, retryOnConnLoss);
        }

        /**
         * Write file to ZooKeeper - default system encoding used.
         *
         * @param path path to upload file to e.g. /solr/conf/solrconfig.xml
         * @param file path to file to be uploaded
         */
        public async Task<Stat> setData(string path, string file, bool retryOnConnLoss)
        {
            //TODO: Log here
            //log.debug("Write to ZooKeeper: {} to {}", file.getAbsolutePath(), path);
            var data = File.ReadAllBytes(file);
            return await setData(path, data, retryOnConnLoss);
        }


        public static Exception checkInterrupted(Exception ex)
        {
            if (ex is ThreadInterruptedException)
            {
                Thread.CurrentThread.Interrupt();
            }
            return ex;
        }

        // Some pass-throughs to allow less code disruption to other classes that use SolrZkClient.
        public void clean(string path)
        {
            ZkMaintenanceUtils.clean(this, path);
        }

        public void clean(string path, Predicate<string> nodeFilter)
        {
            ZkMaintenanceUtils.clean(this, path, nodeFilter);
        }

        public void upConfig(string confPath, string confName)
        {
            var a = DateTime.Now;
            ZkMaintenanceUtils.upConfig(this, confPath, confName);
            Console.WriteLine(DateTime.Now.Ticks - a.Ticks);
        }

        //public string listZnode(string path, string recurse)
        //{
        //    return ZkMaintenanceUtils.listZnode(this, path, recurse);
        //}

        public void downConfig(string confName, string confPath)
        {
            ZkMaintenanceUtils.downConfig(this, confName, confPath);
        }

        //public void zkTransfer(String src, Boolean srcIsZk,
        //    String dst, Boolean dstIsZk,
        //    Boolean recurse)
        //{
        //    ZkMaintenanceUtils.zkTransfer(this, src, srcIsZk, dst, dstIsZk, recurse);
        //}

        //public void moveZnode(string src, string dst)
        //{
        //    ZkMaintenanceUtils.moveZnode(this, src, dst);
        //}

        public void uploadToZK(string rootPath, string zkPath, Regex filenameExclusions)
        {
            ZkMaintenanceUtils.uploadToZK(this, rootPath, zkPath, filenameExclusions);
        }

        public void downloadFromZK(string zkPath, string dir)
        {
            ZkMaintenanceUtils.downloadFromZK(this, zkPath, dir);
        }

        public void Dispose()
        {
            keeper?.closeAsync().Wait();
        }
    }
}
