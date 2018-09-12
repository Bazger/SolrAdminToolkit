﻿using MaterialDesignThemes.Wpf;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ReindexAutomation.Client.Cloud;

namespace ReindexAutomation.Client.Domain
{
    //TODO: Add option to open ZK file in notepad (Double click on file)
    //TODO: Open directory in browser
    //TODO: Add caution for bad dirs
    //TODO: Zk Node and Host make stable 
    //TODO: Clear and LinkConfig buttons

    public class ZookeeperManagmentViewModel : INotifyPropertyChanged
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        private string _configsPath;

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
            InitializeConfigsDirectory(ConfigsPath);
        }

        public string ConfigsPath
        {
            get { return _configsPath; }
            set
            {
                this.MutateVerbose(ref _configsPath, value, RaisePropertyChanged());
            }
        }

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

        public ICommand ApplyCommand => new RelayCommand(_ => InitializeConfigsDirectory(ConfigsPath));
        public ICommand ConnectCommand => new RelayCommand(async _ =>
        {
            var tree = await Task.Run(() => ConnectToZkTree(ZkHost, ZkPort));
            ZkNode.Clear();
            ZkNode.Add(tree);
        });

        public ICommand DirectoriesTreeSelectedItemChangedCommand => new RelayCommand(DirectoryTree_SelectedItemChanged);
        public ICommand ZkTreeSelectedItemChangedCommand => new RelayCommand(ZkTree_SelectedItemChanged);

        public ObservableCollection<TreeViewDirectory> RootDirectories { get; }
        public ObservableCollection<TreeViewDirectory> ZkNode { get; }

        private void InitializeConfigsDirectory(string path)
        {
            if (Path.GetPathRoot(path) != path)
            {
                path = path.TrimEnd('\\');
            }
            Debug.WriteLine(path);
            if (!Directory.Exists(path))
            {
                _snackbarMessageQueue.Enqueue("Wrong Path!", "OK", () => Trace.WriteLine("Actioned"));
                return;
            }
            ConfigsPath = path;
            var selectedDir = new TreeViewDirectory(path, path)
            {
                Directories = Directory.GetDirectories(path).Select(p => new TreeViewDirectory(p)).ToList(),
                Files = Directory.GetFiles(path).Select(p => new TreeViewFile(p)).ToList(),
                IsExpanded = true,
                BackToPreviousMenuItemVisibility = Visibility.Visible
            };
            selectedDir.OpenDirectoryEvent += OpenDirectory;
            selectedDir.BackToPreviousEvent += BackToPrevious;
            foreach (var dir in selectedDir.Directories)
            {
                dir.OpenDirectoryEvent += OpenDirectory;
            }

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
                    tree.IsExpanded = true;
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

        private void OpenDirectory(object sender, EventArgs args)
        {
            if (sender is TreeViewDirectory dir)
            {
                InitializeConfigsDirectory(dir.Path);
            }
        }

        private void BackToPrevious(object sender, EventArgs args)
        {
            if (sender is TreeViewDirectory dir)
            {
                var previousDir = Path.GetDirectoryName(dir.Path);
                InitializeConfigsDirectory(previousDir);
            }
        }

        #region DownConfig

        public ICommand DownConfigDialogCommand => new RelayCommand(ExecuteDownConfigDialog);

        private async void ExecuteDownConfigDialog(object o)
        {
            var configs = ZkNode[0].Directories.ToList().FirstOrDefault(dir => dir.Name.ToLower().Contains("configs"))?
                .Directories.Select(config => config.Name).ToList();
            var configName = Path.GetFileName(_selectedZkConfig?.Name ?? configs?.FirstOrDefault());
            var path = Path.Combine(/*_selectedDirectory?.Path ?? */RootDirectories[0].Path, configName ?? string.Empty);

            //let's set up a little MVVM, cos that's what the cool kids are doing:        
            var context = new ConfigDialogViewModel
            {
                Directory = path,
                ConfigName = configName,
                AvailableDirectories = new ObservableCollection<string> { path },
                AvailableConfigs = configs != null ? new ObservableCollection<string>(configs) : new ObservableCollection<string>()
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
            InitializeConfigsDirectory(RootDirectories[0].Path);
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


    public class TreeViewDirectory : IContextMenu
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
            IsExpanded = false;
            OpenDirectoryMenuItemVisibility = Visibility.Visible;
            BackToPreviousMenuItemVisibility = Visibility.Collapsed;

            Directories = new List<TreeViewDirectory>();
            Files = new List<TreeViewFile>();
        }

        public IList<TreeViewDirectory> Directories { get; set; }
        public IList<TreeViewFile> Files { get; set; }

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

        public bool IsExpanded { get; set; }


        public event EventHandler OpenDirectoryEvent;
        public event EventHandler BackToPreviousEvent;

        public ICommand OpenDirectoryCommand => new RelayCommand(_ => OpenDirectoryEvent?.Invoke(this, null));
        public ICommand BackToPreviousCommand => new RelayCommand(_ => BackToPreviousEvent?.Invoke(this, null));

        public Visibility OpenDirectoryMenuItemVisibility { get; set; }
        public Visibility BackToPreviousMenuItemVisibility { get; set; }
    }

    public interface IContextMenu
    {
        Visibility OpenDirectoryMenuItemVisibility { get; set; }
        Visibility BackToPreviousMenuItemVisibility { get; set; }
    }

    public class TreeViewFile : IContextMenu
    {
        public string Path { get; }
        public string Name { get; }

        public TreeViewFile(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            OpenDirectoryMenuItemVisibility = Visibility.Collapsed;
            BackToPreviousMenuItemVisibility = Visibility.Collapsed;
        }

        public Visibility OpenDirectoryMenuItemVisibility { get; set; }
        public Visibility BackToPreviousMenuItemVisibility { get; set; }
    }
}