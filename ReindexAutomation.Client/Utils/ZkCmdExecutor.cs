using System;
using System.Threading;
using org.apache.zookeeper;

namespace ReindexAutomation.Client.Utils
{
    public class ZkCmdExecutor
    {
        private const int RetryDelayMillis = 1500;
        private double _timeoutSeconds;
        private readonly double _retryCount;

        public ZkCmdExecutor(int timeoutMillis)
        {
            _timeoutSeconds = timeoutMillis / 1000.0;
            _retryCount = Math.Round(0.5f * ((float)Math.Sqrt(8.0f * _timeoutSeconds + 1.0f) - 1.0f)) + 1;
        }

        /// <summary>
        /// Perform the given operation, retrying if the connection fails
        /// </summary>
        /// <param name="operation">Operation for executing</param>
        /// <returns></returns>
        public T RetryOperation<T>(Func<T> operation)
        {
            KeeperException exception = null;
            for (var i = 0; i < _retryCount; i++)
            {
                try
                {
                    return operation();
                }
                catch (KeeperException.ConnectionLossException e)
                {
                    if (exception == null)
                    {
                        exception = e;
                    }
                    if (!Thread.CurrentThread.IsAlive)
                    {
                        Thread.CurrentThread.Interrupt();
                        throw new ThreadInterruptedException();
                    }
                    if (i != (int)_retryCount - 1)
                    {
                        RetryDelay(i);
                    }
                }
            }
            throw exception;
        }


        public void EnsureExists(string path, SolrZkClient zkClient)
        {
            EnsureExists(path, null, CreateMode.PERSISTENT, zkClient, 0);
        }


        public void EnsureExists(string path, byte[] data, SolrZkClient zkClient)
        {
            EnsureExists(path, data, CreateMode.PERSISTENT, zkClient, 0);
        }

        public void EnsureExists(string path, byte[] data, CreateMode createMode, SolrZkClient zkClient)
        {
            EnsureExists(path, data, createMode, zkClient, 0);
        }

        public async void EnsureExists(string path, byte[] data, CreateMode createMode, SolrZkClient zkClient, int skipPathParts)
        {
            if (await zkClient.exists(path, true))
            {
                return;
            }
            try
            {
                await zkClient.makePath(path, data, createMode, null, true, true, skipPathParts);
            }
            catch (KeeperException.NodeExistsException ex)
            {
                // it's okay if another beats us creating the node
            }

        }

        /// <summary>
        ///  Performs a retry delay if this is not the first attempt
        /// </summary>
        /// <param name="attemptCount">The number of the attempts performed so far</param>
        protected void RetryDelay(int attemptCount)
        {
            Thread.Sleep((attemptCount + 1) * RetryDelayMillis);
        }
    }
}
