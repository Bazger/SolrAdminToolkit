﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace ReindexAutomation.Client.Utils
{
    public class ZkConfigManager
    {
        private const string ConfigsZKnode = "/configs";

        public static readonly Regex UploadFilenameExcludeRegex = new Regex("^\\..*$");

        private readonly SolrZkClient zkClient;

        /// <summary>
        /// Creates a new ZkConfigManager
        /// </summary>
        /// <param name="zkClient">zkClient the SolrZkClient to use</param>
        public ZkConfigManager(SolrZkClient zkClient)
        {
            this.zkClient = zkClient;
        }

        /// <summary>
        /// Upload files from a given path to a config in Zookeeper
        /// </summary>
        /// <param name="dir">Path to the files</param>
        /// <param name="configName">The name to give the config</param>
        /// <exception cref="IOException">If an I/O error occurs or the path does not exist</exception>
        public void uploadConfigDir(string dir, string configName)
        {
            zkClient.uploadToZK(dir, ConfigsZKnode + "/" + configName, UploadFilenameExcludeRegex);
        }

        /// <summary>
        /// Upload matching files from a given path to a config in Zookeeper
        /// </summary>
        /// <param name="dir">Path to the files</param>
        /// <param name="configName">The name to give the config</param>
        /// <param name="filenameExclusions">Files matching this pattern will not be uploaded</param>
        /// <exception cref="IOException">If an I/O error occurs or the path does not exist</exception>
        public void uploadConfigDir(string dir, string configName, Regex filenameExclusions)
        {
            zkClient.uploadToZK(dir, ConfigsZKnode + "/" + configName, filenameExclusions);
        }

        /// <summary>
        /// Download a config from Zookeeper and write it to the filesystem
        /// </summary>
        /// <param name="configName">The config to download</param>
        /// <param name="dir">The path to write files under</param>
        /// <exception cref="IOException">If an I/O error occurs or the config does not exist</exception>
        public void downloadConfigDir(string configName, string dir)
        {
            zkClient.downloadFromZK(ConfigsZKnode + "/" + configName, dir);
        }

        public async Task<List<string>> listConfigs()
        {
            try
            {
                return await zkClient.getChildren(ConfigsZKnode, null, true);
            }
            catch (KeeperException.NoNodeException ex)
            {
                return new List<string>();
            }
            catch (Exception ex)
            {
                if (ex is KeeperException || ex is ThreadInterruptedException)
                {
                    throw new IOException("Error listing configs", SolrZkClient.checkInterrupted(ex));
                }
                throw;
            }
        }

        /// <summary>
        /// Check whether a config exists in Zookeeper
        /// </summary>
        /// <param name="configName">The config to check existance on</param>
        /// <returns>Whether the config exists or not</returns>
        /// <exception cref="IOException">If an I/O error occurs</exception>
        public async Task<bool> configExists(String configName)
        {
            try
            {
                return await zkClient.exists(ConfigsZKnode + "/" + configName, true);
            }
            catch (Exception ex)
            {
                if (ex is KeeperException || ex is ThreadInterruptedException)
                {
                    throw new IOException("Error checking whether config exists", SolrZkClient.checkInterrupted(ex));
                }
                throw;
            }
        }

        /// <summary>
        /// Delete a config in ZooKeeper
        /// </summary>
        /// <param name="configName">The config to delete</param>
        /// <exception cref="IOException">If an I/O error occurs</exception> 
        public void deleteConfigDir(string configName)
        {
            try
            {
                zkClient.clean(ConfigsZKnode + "/" + configName);
            }
            catch (Exception ex)
            {
                if (ex is KeeperException || ex is ThreadInterruptedException)
                {
                    throw new IOException("Error checking whether config exists", SolrZkClient.checkInterrupted(ex));
                }
                throw;
            }
        }


        private async Task copyConfigDirFromZk(string fromZkPath, string toZkPath, ISet<string> copiedToZkPaths = null)
        {
            try
            {
                var files = await zkClient.getChildren(fromZkPath, null, true);
                foreach (var file in files)
                {
                    var children = await zkClient.getChildren(fromZkPath + "/" + file, null, true);
                    if (!children.Any())
                    {
                        var toZkFilePath = toZkPath + "/" + file;
                        //TODO: Log here
                        //logger.info("Copying zk node {} to {}",fromZkPath + "/" + file, toZkFilePath);
                        var data = await zkClient.getData(fromZkPath + "/" + file, null, null, true);
                        //Take care it fails on Exists
                        await zkClient.makePath(toZkFilePath, data, true);
                        copiedToZkPaths?.Add(toZkFilePath);
                    }
                    else
                    {
                        await copyConfigDirFromZk(fromZkPath + "/" + file, toZkPath + "/" + file, copiedToZkPaths);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is KeeperException || ex is ThreadInterruptedException)
                {
                    throw new IOException("Error copying nodes from zookeeper path " + fromZkPath + " to " + toZkPath, SolrZkClient.checkInterrupted(ex));
                }
                throw;
            }
        }

        /// <summary>
        /// Copy a config in ZooKeeper
        /// </summary>
        /// <param name="fromConfig">The config to copy from</param>
        /// <param name="toConfig">The config to copy to</param>
        /// <returns></returns>
        /// <exception cref="IOException">If an I/O error occurs</exception> 
        public async Task copyConfigDir(string fromConfig, string toConfig)
        {
            await copyConfigDirAsync(fromConfig, toConfig, null);
        }

        /// <summary>
        /// Copy a config in ZooKeeper
        /// </summary>
        /// <param name="fromConfig">The config to copy from</param>
        /// <param name="toConfig">The config to copy to</param>
        /// <param name="copiedToZkPaths">Should be an empty Set, will be filled in by function
        /// with the paths that were actually copied to</param>
        /// <returns></returns>
        /// <exception cref="IOException">If an I/O error occurs</exception> 
        public async Task copyConfigDirAsync(string fromConfig, string toConfig, HashSet<string> copiedToZkPaths)
        {
            await copyConfigDirFromZk(ConfigsZKnode + "/" + fromConfig, ConfigsZKnode + "/" + toConfig, copiedToZkPaths);
        }


        // This method is used by configSetUploadTool and CreateTool to resolve the configset directory.
        // Check several possibilities:
        // 1> confDir/solrconfig.xml exists
        // 2> confDir/conf/solrconfig.xml exists
        // 3> configSetDir/confDir/conf/solrconfig.xml exists (canned configs)

        // Order is important here since "confDir" may be
        // 1> a full path to the parent of a solrconfig.xml or parent of /conf/solrconfig.xml
        // 2> one of the canned config sets only, e.g. _default
        // and trying to assemble a path for configsetDir/confDir is A Bad Idea. if confDir is a full path.
        public static string getConfigsetPath(string confDir, string configSetDir)
        {

            // A local path to the source, probably already includes "conf".
            string ret = Path.Combine(confDir, "solrconfig.xml");
            if (File.Exists(ret))
            {
                return Path.Combine(confDir);
            }

            // a local path to the parent of a "conf" directory 
            ret = Path.Combine(confDir, "conf", "solrconfig.xml");
            if (File.Exists(ret))
            {
                return Path.Combine(confDir, "conf");
            }

            // one of the canned configsets.
            ret = Path.Combine(configSetDir, confDir, "conf", "solrconfig.xml");
            if (File.Exists(ret))
            {
                return Path.Combine(configSetDir, confDir, "conf");
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
            "Could not find solrconfig.xml at {0}, {1} or {2}",
                Path.Combine(configSetDir, "solrconfig.xml"),
                Path.Combine(configSetDir, "conf", "solrconfig.xml"),
                Path.Combine(configSetDir, confDir, "conf", "solrconfig.xml")
            ));
        }

    }
}