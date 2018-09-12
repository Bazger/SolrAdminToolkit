using MaterialDesignThemes.Wpf;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ReindexAutomation.Client.Cloud;
using ReindexAutomation.Client.Utils;

namespace ReindexAutomation.Client.Domain
{
    //TODO: Add treeview right click menu
    //TODO: Add option to opent ZK file in notepad (Double click on file)
    //TODO: Add option to copy directory
    //TODO: Get directory for 2nd layer
    //TODO: Add caution for bad dirs
    //TODO: Zk Node and Host make stable 
    //TODO: Clear and LinkConfig buttons

    public class ZookeeperManagmentViewModel : INotifyPropertyChanged
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        private bool _isDirectorySelected;
        private bool _isFileSelected;
        private bool _isDirectoryTreeItemSelected;

        private TreeViewDirectory _selectedDirectory;
        private TreeViewDirectory _selectedZkConfig;

        private bool _isZkConfigSelected;
        private bool _isZkPathSelected;
        private bool _isZkConnected;

        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            ZkNode = new ObservableCollection<TreeViewDirectory>();
            RootDirectories = new ObservableCollection<TreeViewDirectory>();

            ConfigsPath = "C:\\Temp";
            InitializeStartupDirectory(ConfigsPath);

            ApplyCommand = new RelayCommand(_ => InitializeStartupDirectory(ConfigsPath));
            ConnectCommand = new RelayCommand(async _ =>
            {
                var tree = await Task.Run(() => ConnectToZkTree(ZkHost, ZkPort));
                ZkNode.Clear();
                ZkNode.Add(tree);
            });
            DirectoriesTreeSelectedItemChangedCommand = new RelayCommand(DirectoryTree_SelectedItemChanged);
            ZkTreeSelectedItemChangedCommand = new RelayCommand(ZkTree_SelectedItemChanged);
        }

        public string ConfigsPath { get; set; }
        public string ZkHost { get; set; }
        public string ZkPort { get; set; }

        public bool IsDirectorySelected
        {
            get { return _isDirectorySelected; }
            set
            {
                this.MutateVerbose(ref _isDirectorySelected, value, RaisePropertyChanged());
            }
        }

        public bool IsFileSelected
        {
            get { return _isFileSelected; }
            set
            {
                this.MutateVerbose(ref _isFileSelected, value, RaisePropertyChanged());
            }
        }

        public bool IsDirectoryTreeItemSelected
        {
            get { return _isDirectoryTreeItemSelected; }
            set
            {
                this.MutateVerbose(ref _isDirectoryTreeItemSelected, value, RaisePropertyChanged());
            }
        }

        public bool IsZkConnected
        {
            get { return _isZkConnected; }
            set
            {
                this.MutateVerbose(ref _isZkConnected, value, RaisePropertyChanged());
            }
        }

        public bool IsZkConfigSelected
        {
            get { return _isZkConfigSelected; }
            set
            {
                this.MutateVerbose(ref _isZkConfigSelected, value, RaisePropertyChanged());
            }
        }

        public bool IsZkPathSelected
        {
            get { return _isZkPathSelected; }
            set
            {
                this.MutateVerbose(ref _isZkPathSelected, value, RaisePropertyChanged());
            }
        }

        public ICommand ApplyCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DirectoriesTreeSelectedItemChangedCommand { get; }
        public ICommand ZkTreeSelectedItemChangedCommand { get; }

        public ObservableCollection<TreeViewDirectory> RootDirectories { get; }
        public ObservableCollection<TreeViewDirectory> ZkNode { get; }

        private void InitializeStartupDirectory(string path, int depth = 2)
        {
            Debug.WriteLine(path);
            if (!Directory.Exists(path))
            {
                _snackbarMessageQueue.Enqueue("Wrong Path!", "OK", () => Trace.WriteLine("Actioned"));
                return;
            }
            var selectedDir = new TreeViewDirectory(path, path)
            {
                Directories = Directory.GetDirectories(path).Select(p => new TreeViewDirectory(p)),
                Files = Directory.GetFiles(path).Select(p => new TreeViewFile(p))
            };

            RootDirectories.Clear();
            RootDirectories.Add(selectedDir);
        }

        private TreeViewDirectory ConnectToZkTree(string zkHost, string zkPort)
        {
            using (var zkClient = new SolrZkClient($"{zkHost}:{zkPort}"))
            {
                try
                {
                    var tree = GetZkTree(zkClient).Result;
                    IsZkConnected = true;
                    return tree;
                }
                catch (Exception)
                {
                    _snackbarMessageQueue.Enqueue("Could not connect to ZK host!", "OK", () => Trace.WriteLine("Actioned"));
                    IsZkConnected = false;
                }
            }
            return null;
        }

        private static async Task<TreeViewDirectory> GetZkTree(SolrZkClient zkCnxn, string zooPath = "/", int depth = 2)
        {
            var children = (await zkCnxn.getChildren(zooPath, null, false));
            if (!children.Any())
            {
                return null;
            }
            var dir = zooPath.Split('/').Length <= depth ? new TreeViewDirectory(zooPath, zooPath) : new TreeViewDirectory(zooPath);

            if (zooPath.Last() != '/')
            {
                zooPath += "/";
            }
            var files = new List<TreeViewFile>();
            var dirs = new List<TreeViewDirectory>();
            foreach (var child in children)
            {
                var entry = await GetZkTree(zkCnxn, zooPath + child);
                if (entry == null)
                {
                    files.Add(new TreeViewFile(child));
                }
                else
                {
                    dirs.Add(entry);
                }
            }

            dir.Directories = dirs;
            dir.Files = files;

            return dir;
        }

        private void DirectoryTree_SelectedItemChanged(object args)
        {
            IsDirectoryTreeItemSelected = true;
            if (args is TreeViewDirectory directory)
            {
                _selectedDirectory = directory;
                IsDirectorySelected = true;
                IsFileSelected = false;
            }
            else
            {
                IsDirectorySelected = false;
                _selectedDirectory = null;
                if (args is TreeViewFile)
                {
                    IsFileSelected = true;
                    return;
                }
                IsFileSelected = false;
                IsDirectoryTreeItemSelected = false;
            }
        }

        private void ZkTree_SelectedItemChanged(object args)
        {
            IsZkPathSelected = true;
            if (args is TreeViewDirectory zkDirectory && zkDirectory.Path.ToLower().Contains("configs") && zkDirectory.Path.Split('/').Length == 3)
            {
                IsZkConfigSelected = true;
                _selectedZkConfig = zkDirectory;
            }
            else
            {
                IsZkConfigSelected = false;
                _selectedZkConfig = null;
                if (args is TreeViewFile || args is TreeViewDirectory)
                {
                    return;
                }
                IsZkPathSelected = false;
            }
        }

        #region DownConfig

        public ICommand DownConfigDialogCommand => new RelayCommand(ExecuteDownConfigDialog);

        private async void ExecuteDownConfigDialog(object o)
        {
            var configName = Path.GetFileName(_selectedZkConfig.Name);
            var path = Path.Combine(/*_selectedDirectory?.Path ?? */RootDirectories[0].Path, configName);

            //let's set up a little MVVM, cos that's what the cool kids are doing:        
            var context = new ConfigDialogViewModel
            {
                Directory = path,
                ConfigName = configName,
                AvailableDirectories = new ObservableCollection<string> { path },
                AvailableConfigs = new ObservableCollection<string> { configName }
            };
            var view = new DownConfigDialog
            {
                DataContext = context
            };

            //show the dialog
            var result = await DialogHost.Show(view, "RootDialog", DownConfigClosingEventHandler);

            //check the result...
            Debug.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
        }

        private async void DownConfigClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if ((bool)eventArgs.Parameter == false) { return; }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as ConfigDialogViewModel;
            var dir = dialogModel?.Directory;
            var configName = dialogModel?.ConfigName;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(configName))
            {
                //TODO: Throw the snackbar
                return;
            }

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new SampleProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            //lets run a fake operation for 3 seconds then close this baby.
            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHost}:{ZkPort}"))
                {
                    try
                    {
                        await zkClient.downConfig(configName, dir);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                TaskScheduler.FromCurrentSynchronizationContext());
            InitializeStartupDirectory(RootDirectories[0].Path);
        }

        #endregion


        #region UpConfig

        public ICommand UpConfigDialogCommand => new RelayCommand(ExecuteUpConfigDialog);

        private async void ExecuteUpConfigDialog(object o)
        {
            var configName = Path.GetFileName(_selectedDirectory.Name);
            if (!configName.ToLower().Contains("config"))
            {
                configName += "Config";
            }
            var path = _selectedDirectory.Path;//FileHelper.MinimizePath(_selectedDirectory.Path, 60);


            //let's set up a little MVVM, cos that's what the cool kids are doing:        
            var context = new ConfigDialogViewModel
            {
                Directory = path,
                ConfigName = configName,
                AvailableDirectories = new ObservableCollection<string> { path },
                AvailableConfigs = new ObservableCollection<string> { configName }
            };
            var view = new UpConfigDialog
            {
                DataContext = context
            };

            //show the dialog
            var result = await DialogHost.Show(view, "RootDialog", UpConfigClosingEventHandler);

            //check the result...
            Debug.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
        }

        private async void UpConfigClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if ((bool)eventArgs.Parameter == false) { return; }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as ConfigDialogViewModel;
            var dir = dialogModel?.Directory;
            var configName = dialogModel?.ConfigName;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(configName))
            {
                //TODO: Throw the snackbar
                return;
            }

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new SampleProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            //lets run a fake operation for 3 seconds then close this baby.
            await Task.Run(() =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHost}:{ZkPort}"))
                {
                    try
                    {
                        zkClient.upConfig(dir, configName);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                TaskScheduler.FromCurrentSynchronizationContext());
            var tree = await Task.Run(() => ConnectToZkTree(ZkHost, ZkPort));
            ZkNode.Clear();
            ZkNode.Add(tree);
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }
    }


    public class TreeViewDirectory
    {
        public string Path { get; }
        public string Name { get; }

        public TreeViewDirectory(string path) : this(path, System.IO.Path.GetFileName(path))
        {
        }

        public TreeViewDirectory(string path, string name)
        {
            Path = path;
            Name = name;

            Directories = new List<TreeViewDirectory>();
            Files = new List<TreeViewFile>();
        }
        public IEnumerable<TreeViewDirectory> Directories { get; set; }
        public IEnumerable<TreeViewFile> Files { get; set; }

        public IEnumerable<object> Items
        {
            get
            {
                var childNodes = new List<object>();
                childNodes.AddRange(Directories);
                childNodes.AddRange(Files);

                return childNodes;
            }
        }
    }

    public class TreeViewFile
    {
        public string Path { get; }
        public string Name { get; }

        public TreeViewFile(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }
    }
}