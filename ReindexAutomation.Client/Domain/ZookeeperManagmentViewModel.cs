using System.Configuration;
using ReindexAutomation.Client.Domain;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Transitions;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ReindexAutomation.Client.Cloud;

namespace ReindexAutomation.Client.Domain
{
    public class ZookeeperManagmentViewModel : INotifyPropertyChanged
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        private bool _isDirectorySelected;
        private bool _isFileSelected;
        private bool _isDirectoryTreeItemSelected;

        private bool _isZkConfigSelected;
        private bool _isZkPathSelected;

        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            ZkNode = new ObservableCollection<TreeViewDirectory>();
            RootDirectories = new ObservableCollection<TreeViewDirectory>();

            InitializeConfigsDirectory("C:\\Windows");

            ApplyCommand = new RelayCommand(_ => InitializeConfigsDirectory(ConfigsPath));
            ConnectCommand = new RelayCommand(async _ =>
            {
                var a = await Task.Run(() => ConnectToZkTree(ZkHost, ZkPort));
                ZkNode.Clear();
                ZkNode.Add(a);
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


        private void InitializeConfigsDirectory(string path, int depth = 2)
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
                    return GetZkTree(zkClient).Result;
                }
                catch (Exception)
                {
                    _snackbarMessageQueue.Enqueue("Could not connect to ZK host!", "OK", () => Trace.WriteLine("Actioned"));
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
            if (args is TreeViewDirectory)
            {
                IsDirectorySelected = true;
                IsFileSelected = false;
            }
            else
            {
                IsDirectorySelected = false;
                IsFileSelected = true;
            }
        }

        private void ZkTree_SelectedItemChanged(object args)
        {
            IsZkPathSelected = true;
            if (args is TreeViewDirectory)
            {
                var dir = args as TreeViewDirectory;
                if (dir.Path.ToLower().Contains("configs") && dir.Path.Split('/').Length == 3)
                {
                    IsZkConfigSelected = true;
                }
            }
            else
            {
                IsZkConfigSelected = false;
            }
        }


        #region BUTTON PRESS CHECK

        public ICommand RunDialogCommand => new RelayCommand(ExecuteRunDialog);

        public ICommand RunExtendedDialogCommand => new RelayCommand(ExecuteRunExtendedDialog);

        private async void ExecuteRunDialog(object o)
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var view = new UpConfigDialog
            {
                DataContext = new ConfigDialogViewModel
                {
                    SelectedConfigName = "BicepsConfig",
                    AvailableConfigs = new ObservableCollection<string> { "BicepsConfig" }
                }
            };

            //show the dialog
            var result = await DialogHost.Show(view, "RootDialog", ClosingEventHandler);

            //check the result...
            Console.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
        }

        private void ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            Console.WriteLine("You can intercept the closing event, and cancel here.");
        }

        private async void ExecuteRunExtendedDialog(object o)
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var view = new SampleDialog
            {
                DataContext = new SampleDialogViewModel()
            };

            //show the dialog
            var result = await DialogHost.Show(view, "RootDialog", ExtendedOpenedEventHandler, ExtendedClosingEventHandler);

            //check the result...
            Console.WriteLine("Dialog was closed, the CommandParameter used to close it was: " + (result ?? "NULL"));
        }

        private void ExtendedOpenedEventHandler(object sender, DialogOpenedEventArgs eventargs)
        {
            Console.WriteLine("You could intercept the open and affect the dialog using eventArgs.Session.");
        }

        private void ExtendedClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            if ((bool)eventArgs.Parameter == false) return;

            //OK, lets cancel the close...
            eventArgs.Cancel();

            //...now, lets update the "session" with some new content!
            eventArgs.Session.UpdateContent(new SampleProgressDialog());
            //note, you can also grab the session when the dialog opens via the DialogOpenedEventHandler

            //lets run a fake operation for 3 seconds then close this baby.
            Task.Delay(TimeSpan.FromSeconds(3))
                .ContinueWith((t, _) => eventArgs.Session.Close(false), null,
                    TaskScheduler.FromCurrentSynchronizationContext());
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