using System;
using System.Collections.Generic;
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
                //await _zkCmdExecutor.RetryOperation(async () =>
                //{
                //    await keeper.deleteAsync(path, version);
                //    return null;
                //});
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


        public async Task<List<string>> getChildren(string path, Watcher watcher, bool retryOnConnLoss)
        {
            if (retryOnConnLoss)
            {
                return await _zkCmdExecutor.RetryOperation(async () =>
                {
                    Console.WriteLine("HELLO");
                    try
                    {
                        var children = (await keeper.getChildrenAsync(path, null)).Children;
                        Console.WriteLine("HELLO");
                        return children;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("HELLO");
                    }
                    Console.WriteLine("HELLO");
                    return null;
                });
            }
            else
            {
                return (await keeper.getChildrenAsync(path, watcher)).Children;
            }
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

        //public void upConfig(string confPath, string confName)
        //{
        //    ZkMaintenanceUtils.upConfig(this, confPath, confName);
        //}

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

        //public void uploadToZK(string rootPath, string zkPath, Regex filenameExclusions)
        //{
        //    ZkMaintenanceUtils.uploadToZK(this, rootPath, zkPath, filenameExclusions);
        //}

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
