using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;

namespace ReindexAutomation.Client.Utils
{
    public class ZkConfigManager
    {
        private const string ConfigsZKnode = "/configs";

        //TODO: Check the regex
        public static readonly Regex UploadFilenameExcludeRegex = new Regex("^\\..*$");

        private SolrZkClient zkClient;

        public ZkConfigManager(SolrZkClient zkClient)
        {
            this.zkClient = zkClient;
        }

        public void uploadConfigDir(string dir, string configName)
        {
            zkClient.uploadToZK(dir, ConfigsZKnode + "/" + configName, UploadFilenameExcludeRegex);
        }

        public void uploadConfigDir(string dir, string configName, Regex filenameExclusions)
        {
            zkClient.uploadToZK(dir, ConfigsZKnode + "/" + configName, filenameExclusions);
        }

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

        //TODO: This func
        //private void copyConfigDirFromZk

        private async Task copyConfigDirFromZk(string fromZkPath, string toZkPath, ISet<string> copiedToZkPaths = null)
        {
            try
            {
                var files = await zkClient.getChildren(fromZkPath, null, true);
                foreach (var file in files)
                {
                    var children = await zkClient.getChildren(fromZkPath + "/" + file, null, true);
                    if (children.Any())
                    {
                        var toZkFilePath = toZkPath + "/" + file;
                        var data = await zkClient.getData(fromZkPath + "/" + file, null, null, true);
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

        public void copyConfigDir(string fromConfig, string toConfig, HashSet<string> copiedToZkPaths = null)
        {
            copyConfigDirFromZk(ConfigsZKnode + "/" + fromConfig, ConfigsZKnode + "/" + toConfig, copiedToZkPaths);
        }

        //public static string getConfigsetPath(string confDir, string configSetDir)
        //{

        //    // A local path to the source, probably already includes "conf".
        //    string ret = Paths.get(confDir, "solrconfig.xml").normalize();
        //    if (File.Exists(ret))
        //    {
        //        return Paths.get(confDir).normalize();
        //    }

        //    // a local path to the parent of a "conf" directory 
        //    ret = Paths.get(confDir, "conf", "solrconfig.xml").normalize();
        //    if (File.Exists(ret))
        //    {
        //        return Paths.get(confDir, "conf").normalize();
        //    }

        //    // one of the canned configsets.
        //    ret = Paths.get(configSetDir, confDir, "conf", "solrconfig.xml").normalize();
        //    if (File.Exists(ret))
        //    {
        //        return Paths.get(configSetDir, confDir, "conf").normalize();
        //    }


        //    throw new ArgumentException(String.format(Locale.ROOT,
        //    "Could not find solrconfig.xml at %s, %s or %s",
        //    Paths.get(configSetDir, "solrconfig.xml").normalize().toAbsolutePath().toString(),
        //    Paths.get(configSetDir, "conf", "solrconfig.xml").normalize().toAbsolutePath().toString(),
        //    Paths.get(configSetDir, confDir, "conf", "solrconfig.xml").normalize().toAbsolutePath().toString()
        //    ));
        //}

    }
}