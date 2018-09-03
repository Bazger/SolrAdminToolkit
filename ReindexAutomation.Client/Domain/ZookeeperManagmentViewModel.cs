using System.Configuration;
using ReindexAutomation.Client.Domain;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Transitions;
using System.Windows.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ReindexAutomation.Client.Cloud;

namespace ReindexAutomation.Client.Domain
{
    public class ZookeeperManagmentViewModel
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            ApplyCommand = new AnotherCommandImplementation(_ => InitializeConfigsDirectory(ConfigsPath));
            ConnectCommand = new AnotherCommandImplementation(async _ =>
            {
                var a = await Task.Run(() => ConnectToZkTree(ZkHost, ZkPort));
                ZkNode.Clear();
                ZkNode.Add(a);
            });
            RootDirectories = new ObservableCollection<TreeViewDirectory>
            {
                new TreeViewDirectory("C:\\Temp", "C:\\")
                {
                    Directories = new ObservableCollection<TreeViewDirectory> {new TreeViewDirectory("C:\\Temp\\A")},
                    Files = new ObservableCollection<TreeViewFile>
                    {
                        new TreeViewFile("C:\\Temp\\BB.txt"),
                        new TreeViewFile("C:\\Temp\\CC.txt")
                    }
                }
            };
            ZkNode = new ObservableCollection<TreeViewDirectory>();
        }

        public string ConfigsPath { get; set; }
        public string ZkHost { get; set; }
        public string ZkPort { get; set; }

        public ICommand ApplyCommand { get; }
        public ICommand ConnectCommand { get; }

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
                    var nodes = ZkMaintenanceUtils.GetTree(zkClient).Result;
                    var tree = new TreeViewDirectory("/", "/")
                    {
                        Directories = nodes.Select(n => new TreeViewDirectory(n)).ToList()
                    };
                    return tree;
                }
                catch (Exception ex)
                {
                    _snackbarMessageQueue.Enqueue("Could not connect to ZK host!", "OK", () => Trace.WriteLine("Actioned"));
                }
            }
            return null;
        }

        public ObservableCollection<TreeViewDirectory> RootDirectories { get; private set; }
        public ObservableCollection<TreeViewDirectory> ZkNode { get; private set; }


        #region BUTTON PRESS CHECK

        public ICommand RunDialogCommand => new AnotherCommandImplementation(ExecuteRunDialog);

        public ICommand RunExtendedDialogCommand => new AnotherCommandImplementation(ExecuteRunExtendedDialog);

        private async void ExecuteRunDialog(object o)
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var view = new SampleDialog
            {
                DataContext = new SampleDialogViewModel()
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