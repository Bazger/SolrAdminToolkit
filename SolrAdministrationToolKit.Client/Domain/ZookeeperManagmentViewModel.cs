﻿using MaterialDesignThemes.Wpf;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SolrAdministrationToolKit.Client.Cloud;
using SolrAdministrationToolKit.Client.Dialogs;

namespace SolrAdministrationToolKit.Client.Domain
{
    //TODO: Cancel to upload or download
    //TODO: Make path and Link config setup with new dialogs
    public class ZookeeperManagmentViewModel : INotifyPropertyChanged
    {
        private const int CheckDialogShowingTimeMillis = 500;

        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        private CancellationTokenSource _cts;

        private string _configsPath;

        private bool _isDirectoryInitialized;
        private bool _isDirectorySelected;
        private bool _isFileSelected;
        private bool _isDirectoryTreeItemSelected;

        private string _selectedLocalPath;
        private string _selectedZkPath;

        private bool _isZkConfigSelected;
        private bool _isZkPathSelected;
        private bool _isZkConnected;

        private Visibility _zkTreeLoadingVisibility;

        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            ZkNode = new ObservableCollection<TreeViewDirectory>();
            RootDirectories = new ObservableCollection<TreeViewDirectory>();

            ConfigsPath = "C:\\Temp";
            UpdateDirectoriesTree(ConfigsPath);

            ZkTreeLoadingVisibility = Visibility.Hidden;
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
        public string ZkHostConnected { get; private set; }
        public string ZkPort { get; set; }
        public string ZkPortConnected { get; private set; }

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

        public bool IsDirectoryInitialized
        {
            get { return _isDirectoryInitialized; }
            set
            {
                this.MutateVerbose(ref _isDirectoryInitialized, value, RaisePropertyChanged());
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

        public Visibility ZkTreeLoadingVisibility
        {
            get { return _zkTreeLoadingVisibility; }
            set
            {
                this.MutateVerbose(ref _zkTreeLoadingVisibility, value, RaisePropertyChanged());
            }
        }

        public ICommand ApplyCommand => new RelayCommand(_ =>
        {
            UpdateDirectoriesTree(ConfigsPath);
        });
        public ICommand ConnectCommand => new RelayCommand(_ =>
        {
            UpdateZkTree(ZkHost, ZkPort);
        });


        public ICommand DirectoriesTreeSelectedItemChangedCommand => new RelayCommand(DirectoryTree_SelectedItemChanged);
        public ICommand ZkTreeSelectedItemChangedCommand => new RelayCommand(ZkTree_SelectedItemChanged);

        public ObservableCollection<string> AAA { get; }
        public ObservableCollection<TreeViewDirectory> RootDirectories { get; }
        public ObservableCollection<TreeViewDirectory> ZkNode { get; }

        private void UpdateDirectoriesTree(string path)
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
                BackToPreviousMenuItemEnabled = true
            };

            //EVENTS
            selectedDir.OpenDirectoryEvent += OpenDirectory;
            selectedDir.BackToPreviousEvent += BackToPrevious;
            selectedDir.ShowInExplorerEvent += ShowInExplorer;
            foreach (var dir in selectedDir.Directories)
            {
                dir.OpenDirectoryEvent += OpenDirectory;
                dir.ShowInExplorerEvent += ShowInExplorer;
            }
            foreach (var file in selectedDir.Files)
            {
                file.ShowInExplorerEvent += ShowInExplorer;
            }

            RootDirectories.Clear();
            RootDirectories.Add(selectedDir);

            IsDirectoryInitialized = true;
        }

        private async void UpdateZkTree(string zkHost, string zkPort)
        {
            ZkTreeLoadingVisibility = Visibility.Visible;
            ZkNode.Clear();
            var tree = await Task.Run(() => ConnectToZkTree(zkHost, zkPort));
            if (tree != null)
            {
                ZkNode.Add(tree);
            }
            ZkTreeLoadingVisibility = Visibility.Hidden;
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
                    ZkHostConnected = ZkHost;
                    ZkPortConnected = ZkPort;
                    return tree;
                }
                catch (Exception)
                {
                    _snackbarMessageQueue.Enqueue("Could not connect to ZK host!", "OK", () => Trace.WriteLine("Actioned"));
                    IsZkConnected = false;
                    ZkHostConnected = null;
                    ZkPortConnected = null;
                }
            }
            return null;
        }

        private async Task<TreeViewDirectory> GetZkTree(SolrZkClient zkCnxn, string zooPath = "/", int depth = 2)
        {
            var children = (await zkCnxn.getChildren(zooPath, null, false));
            if (!children.Any())
            {
                return null;
            }
            var dir = zooPath.Split('/').Length <= depth ? new TreeViewDirectory(zooPath, zooPath) : new TreeViewDirectory(zooPath);
            dir.ShowDataEvent += ShowData;

            if (zooPath.Last() != '/')
            {
                zooPath += "/";
            }
            var files = new List<TreeViewFile>();
            var dirs = new List<TreeViewDirectory>();
            foreach (var child in children)
            {
                if (await ZkMaintenanceUtils.isEphemeral(zkCnxn, zooPath + child))
                {
                    //TODO: Show or not to show (add the checkbox)
                    continue;
                }
                var entry = await GetZkTree(zkCnxn, zooPath + child);
                if (entry == null)
                {
                    var file = new TreeViewFile(zooPath + child, child);
                    file.ShowDataEvent += ShowData;
                    files.Add(file);
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
            IsFileSelected = false;
            _selectedLocalPath = null;
            IsDirectorySelected = false;
            if (args is TreeViewDirectory directory)
            {
                _selectedLocalPath = directory.Path;
                IsDirectorySelected = true;
            }
            else
            {
                if (args is TreeViewFile file)
                {
                    IsFileSelected = true;
                    _selectedLocalPath = file.Path;
                    return;
                }
                IsDirectoryTreeItemSelected = false;
            }
        }

        private void ZkTree_SelectedItemChanged(object args)
        {
            IsZkPathSelected = true;
            IsZkConfigSelected = false;
            _selectedZkPath = null;
            if (args is TreeViewDirectory zkDirectory)
            {
                _selectedZkPath = zkDirectory.Path;
                if (zkDirectory.Path.ToLower().Contains(ZkConfigManager.ConfigsZKnode) && zkDirectory.Path.Split('/').Length == 3)
                {
                    IsZkConfigSelected = true;
                }
            }
            else
            {
                if (args is TreeViewFile zkFile)
                {
                    _selectedZkPath = zkFile.Path;
                    return;
                }
                IsZkPathSelected = false;
            }
        }

        private void OpenDirectory(object sender, EventArgs args)
        {
            if (sender is TreeViewDirectory dir)
            {
                UpdateDirectoriesTree(dir.Path);
            }
        }

        private void BackToPrevious(object sender, EventArgs args)
        {
            if (sender is TreeViewDirectory dir)
            {
                var previousDir = Path.GetDirectoryName(dir.Path);
                UpdateDirectoriesTree(previousDir);
            }
        }

        private void ShowInExplorer(object sender, EventArgs args)
        {
            var path = string.Empty;
            switch (sender)
            {
                case TreeViewDirectory dir:
                    path = dir.Path;
                    break;
                case TreeViewFile file:
                    path = file.Path;
                    break;
            }
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                _snackbarMessageQueue.Enqueue("Path does not exist!", "OK", () => Trace.WriteLine("Actioned"));
                return;
            }
            var argument = "/select, \"" + path + "\"";
            Process.Start("explorer.exe", argument);
        }

        private void ShowData(object sender, EventArgs args)
        {
            var path = string.Empty;
            switch (sender)
            {
                case TreeViewDirectory dir:
                    path = dir.Path;
                    break;
                case TreeViewFile file:
                    path = file.Path;
                    break;
            }
            Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        var data = await zkClient.getData(path, null, null, true);
                        if (data == null)
                        {
                            _snackbarMessageQueue.Enqueue("There is no data to show!", "OK", () => Trace.WriteLine("Actioned"));
                        }
                        else
                        {
                            var relativePath = path.Replace("/", "\\");
                            var dirPath = MainWindow.TempDirectory + Path.GetDirectoryName(relativePath);
                            if (!Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }
                            var filePath = Path.Combine(dirPath, Path.GetFileName(path));
                            File.WriteAllBytes(filePath, data);
                            Process.Start(filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            });
        }

        #region DownConfig

        public ICommand DownConfigDialogCommand => new RelayCommand(ExecuteDownConfigDialog);

        private async void ExecuteDownConfigDialog(object o)
        {
            var configs = ZkNode[0].Directories.ToList().FirstOrDefault(dir => dir.Name.ToLower().Contains(ZkConfigManager.ConfigsZKnode))?
                .Directories.Select(config => config.Name).ToList();
            var selectedZkConfig = IsZkConfigSelected ? _selectedZkPath : null;
            var configName = Path.GetFileName(selectedZkConfig ?? configs?.FirstOrDefault());
            var path = Path.Combine(RootDirectories[0].Path, configName ?? string.Empty);

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
            await DialogHost.Show(view, "RootDialog", DownConfigClosingEventHandler);
        }

        private async void DownConfigClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                _cts.Cancel();
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as ConfigDialogViewModel;
            var dir = dialogModel?.Directory;
            var configName = dialogModel?.ConfigName;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(configName))
            {
                //TODO: Throw the snackbar
                return;
            }

            _cts = new CancellationTokenSource();

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new Dialogs.ProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.downConfig(configName, dir, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            if (!_cts.IsCancellationRequested)
            {
                eventArgs.Session.UpdateContent(new CheckDialog());
                await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                    .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                        TaskScheduler.FromCurrentSynchronizationContext());
            }
            UpdateDirectoriesTree(RootDirectories[0].Path);
        }

        #endregion

        #region UpConfig

        public ICommand UpConfigDialogCommand => new RelayCommand(ExecuteUpConfigDialog);

        private async void ExecuteUpConfigDialog(object o)
        {
            var configName = Path.GetFileName(_selectedLocalPath);
            if (!configName.ToLower().Contains("config"))
            {
                configName += "Config";
            }
            var path = _selectedLocalPath;


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
            await DialogHost.Show(view, "RootDialog", UpConfigClosingEventHandler);
        }

        private async void UpConfigClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                _cts.Cancel();
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as ConfigDialogViewModel;
            var dir = dialogModel?.Directory;
            var configName = dialogModel?.ConfigName;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(configName))
            {
                //TODO: Throw the snackbar
                return;
            }

            _cts = new CancellationTokenSource();

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new Dialogs.ProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.upConfig(dir, configName, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            if (!_cts.IsCancellationRequested)
            {
                eventArgs.Session.UpdateContent(new CheckDialog());
                await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                    .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                        TaskScheduler.FromCurrentSynchronizationContext());
            }
            UpdateZkTree(ZkHostConnected, ZkPortConnected);
        }

        #endregion

        #region MakePath

        public ICommand MakePathDialogCommand => new RelayCommand(ExecuteMakePathDialog);

        private async void ExecuteMakePathDialog(object o)
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var context = new CommonDialogViewModel
            {
                Name = _selectedZkPath ?? "/"
            };
            var view = new MakePathDialog
            {
                DataContext = context
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", MakePathClosingEventHandler);
        }

        private async void MakePathClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as CommonDialogViewModel;
            var path = dialogModel?.Name;


            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new ProgressDialog(false));
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.makePath(path, true);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            eventArgs.Session.UpdateContent(new CheckDialog());
            await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                    TaskScheduler.FromCurrentSynchronizationContext());
            UpdateZkTree(ZkHostConnected, ZkPortConnected);
        }

        #endregion

        #region Put

        public ICommand PutDialogCommand => new RelayCommand(ExecutePutDialog);

        private async void ExecutePutDialog(object o)
        {
            var path = _selectedLocalPath;
            var selectedZkPath = IsZkPathSelected ? _selectedZkPath : null;

            //let's set up a little MVVM, cos that's what the cool kids are doing:        
            var context = new TransferDialogViewModel
            {
                LocalDirectory = path,
                ZkPath = selectedZkPath != null ?
                    Path.Combine(selectedZkPath, Path.GetFileName(path) ?? string.Empty).Replace("\\", "/") :
                    "/" + Path.GetFileName(path)
            };
            var view = new PutDialog
            {
                DataContext = context
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", PutClosingEventHandler);
        }

        private async void PutClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                _cts.Cancel();
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as TransferDialogViewModel;
            var dir = dialogModel?.LocalDirectory;
            var zkPath = dialogModel?.ZkPath;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(zkPath))
            {
                //TODO: Throw the snackbar
                return;
            }


            _cts = new CancellationTokenSource();

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new ProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.uploadToZK(dir, zkPath, ZkConfigManager.UploadFilenameExcludeRegex, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            if (!_cts.IsCancellationRequested)
            {
                eventArgs.Session.UpdateContent(new CheckDialog());
                await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                    .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                        TaskScheduler.FromCurrentSynchronizationContext());
            }
            UpdateZkTree(ZkHostConnected, ZkPortConnected);
        }

        #endregion

        #region Get

        public ICommand GetDialogCommand => new RelayCommand(ExecuteGetDialog);

        private async void ExecuteGetDialog(object o)
        {
            var dir = Path.Combine((IsDirectorySelected ? _selectedLocalPath : null) ?? RootDirectories[0].Path);
            var selectedZkPath = IsZkPathSelected ? _selectedZkPath : null;

            //let's set up a little MVVM, cos that's what the cool kids are doing:        
            var context = new TransferDialogViewModel
            {
                LocalDirectory = Path.Combine(dir, Path.GetFileName(selectedZkPath ?? string.Empty)),
                ZkPath = selectedZkPath,
            };
            var view = new GetDialog
            {
                DataContext = context
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", GetClosingEventHandler);
        }

        private async void GetClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                _cts.Cancel();
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as TransferDialogViewModel;
            var dir = dialogModel?.LocalDirectory;
            var zkPath = dialogModel?.ZkPath;

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(zkPath))
            {
                //TODO: Throw the snackbar
                return;
            }

            _cts = new CancellationTokenSource();

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new ProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.downloadFromZK(zkPath, dir, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            if (!_cts.IsCancellationRequested)
            {
                eventArgs.Session.UpdateContent(new CheckDialog());
                await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                    .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                        TaskScheduler.FromCurrentSynchronizationContext());
            }
            //InitializeConfigsDirectory(RootDirectories[0].Path);
        }

        #endregion

        #region LinkConfig

        public ICommand LinkConfigDialogCommand => new RelayCommand(ExecuteLinkConfigDialog);

        private async void ExecuteLinkConfigDialog(object o)
        {
            var collections = ZkNode[0].Directories.ToList().FirstOrDefault(dir => dir.Name.ToLower().Contains(ZkConfigManager.CollectionsZknode))?
                .Directories.Select(config => config.Name).ToList();
            var configs = ZkNode[0].Directories.ToList().FirstOrDefault(dir => dir.Name.ToLower().Contains(ZkConfigManager.ConfigsZKnode))?
                .Directories.Select(config => config.Name).ToList();

            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var context = new LinkConfigDialogViewModel
            {
                AvailableCollections = collections != null ? new ObservableCollection<string>(collections) : new ObservableCollection<string>(),
                AvailableConfigs = configs != null ? new ObservableCollection<string>(configs) : new ObservableCollection<string>()
            };
            var view = new LinkConfigDialog()
            {
                DataContext = context
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", LinkConfigClosingEventHandler);
        }

        private async void LinkConfigClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as LinkConfigDialogViewModel;
            var configName = dialogModel?.ConfigName;
            var collectionName = dialogModel?.CollectionName;


            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new ProgressDialog(false));
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        var manager = new ZkConfigManager(zkClient);
                        await manager.linkConfSet(collectionName, configName);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            //TODO: Do if ex was not throwen
            eventArgs.Session.UpdateContent(new CheckDialog());
            await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                    TaskScheduler.FromCurrentSynchronizationContext());
            UpdateZkTree(ZkHostConnected, ZkPortConnected);
        }

        #endregion

        #region DeletePath

        public ICommand DeltePathDialogCommand => new RelayCommand(ExecuteDeletePathDialog);

        private async void ExecuteDeletePathDialog(object o)
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var context = new CommonDialogViewModel
            {
                Name = _selectedZkPath ?? "/"
            };
            var view = new DeletePathDialog()
            {
                DataContext = context
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", DeletePathClosingEventHandler);
        }

        private async void DeletePathClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if (Convert.ToBoolean(eventArgs.Parameter) == false)
            {
                _cts.Cancel();
                return;
            }

            var dialogModel = (eventArgs.Session.Content as UserControl)?.DataContext as CommonDialogViewModel;
            var zkPath = dialogModel?.Name;

            _cts = new CancellationTokenSource();

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new ProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler            

            await Task.Run(async () =>
            {
                using (var zkClient = new SolrZkClient($"{ZkHostConnected}:{ZkPortConnected}"))
                {
                    try
                    {
                        await zkClient.clean(zkPath, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        //TODO: Show error;
                    }
                }
            }).ContinueWith((t, _) => { }, null,
                TaskScheduler.FromCurrentSynchronizationContext());
            if (!_cts.IsCancellationRequested)
            {
                eventArgs.Session.UpdateContent(new CheckDialog());
                await Task.Delay(TimeSpan.FromMilliseconds(CheckDialogShowingTimeMillis))
                    .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                        TaskScheduler.FromCurrentSynchronizationContext());
            }
            //TODO: Do if ex was not throwen
            UpdateZkTree(ZkHostConnected, ZkPortConnected);
        }

        #endregion


        public event PropertyChangedEventHandler PropertyChanged;

        private Action<PropertyChangedEventArgs> RaisePropertyChanged()
        {
            return args => PropertyChanged?.Invoke(this, args);
        }
    }


    public interface IContextMenu
    {
        bool OpenDirectoryMenuItemEnabled { get; set; }
        bool BackToPreviousMenuItemEnabled { get; set; }
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
            OpenDirectoryMenuItemEnabled = true;

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
        public event EventHandler ShowInExplorerEvent;
        public event EventHandler ShowDataEvent;

        public ICommand OpenDirectoryCommand => new RelayCommand(_ => OpenDirectoryEvent?.Invoke(this, null));
        public ICommand BackToPreviousCommand => new RelayCommand(_ => BackToPreviousEvent?.Invoke(this, null));
        public ICommand ShowInExplorerCommand => new RelayCommand(_ => ShowInExplorerEvent?.Invoke(this, null));
        public ICommand ShowDataCommand => new RelayCommand(_ => ShowDataEvent?.Invoke(this, null));

        public bool OpenDirectoryMenuItemEnabled { get; set; }
        public bool BackToPreviousMenuItemEnabled { get; set; }
    }

    public class TreeViewFile : IContextMenu
    {
        public string Path { get; }
        public string Name { get; }

        public TreeViewFile(string path) : this(path, System.IO.Path.GetFileName(path))
        {
        }

        public TreeViewFile(string path, string name)
        {
            Path = path;
            Name = name;
        }

        public bool IsExpanded { get; set; }

        public bool OpenDirectoryMenuItemEnabled { get; set; }
        public bool BackToPreviousMenuItemEnabled { get; set; }

        public event EventHandler ShowInExplorerEvent;
        public event EventHandler ShowDataEvent;

        public ICommand ShowInExplorerCommand => new RelayCommand(_ => ShowInExplorerEvent?.Invoke(this, null));
        public ICommand ShowDataCommand => new RelayCommand(_ => ShowDataEvent?.Invoke(this, null));
    }
}