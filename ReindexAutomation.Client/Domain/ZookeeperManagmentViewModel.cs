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
using System.Windows;
using System.Windows.Input;

namespace ReindexAutomation.Client.Domain
{
    public class ZookeeperManagmentViewModel
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            ApplyCommand = new AnotherCommandImplementation(_ => InitializeConfigsDirectory(ConfigsPath));
            RootDirectories = new ObservableCollection<TreeViewDirectory>
            {
                new TreeViewDirectory("C:\\Temp","C:\\")
                {
                    Directories = new ObservableCollection<TreeViewDirectory> {new TreeViewDirectory("C:\\Temp\\A")},
                    Files = new ObservableCollection<TreeViewFile>
                    {
                        new TreeViewFile("C:\\Temp\\BB.txt"),
                        new TreeViewFile("C:\\Temp\\CC.txt")
                    }
                }
            };
        }

        public string ConfigsPath { get; set; }

        public ICommand ApplyCommand { get; }

        private void InitializeConfigsDirectory(string path, int depth = 2)
        {
            if (!Directory.Exists(path))
            {
                _snackbarMessageQueue.Enqueue("Wrong Path!","OK", () => Trace.WriteLine("Actioned"));
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

        public ObservableCollection<TreeViewDirectory> RootDirectories { get; private set; }

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