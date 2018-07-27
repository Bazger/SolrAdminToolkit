using System.Configuration;
using ReindexAutomation.Client.Domain;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Transitions;
using System.Windows.Controls;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace ReindexAutomation.Client.Domain
{
    public class ZookeeperManagmentViewModel
    {
        public ZookeeperManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            ApplyCommand = new AnotherCommandImplementation(_ => GetFiles(ConfigsPath));
        }

        public string ConfigsPath { get; set; }

        public ICommand ApplyCommand { get; }

        private void GetFiles(string path, int depth = 2)
        {
            if (!Directory.Exists(path))
            {
                return;
            }           
            var a = Directory.GetDirectories(path).ToList();            
            var b = a.Count();
        }

        public string SelectedConfig { get; set; }
    }
}