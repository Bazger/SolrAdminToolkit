using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ReindexAutomation.Client.Domain;

namespace ReindexAutomation.Client.Domain
{
    public class ConfigDialogViewModel : SampleDialogViewModel
    {
        public string ConfigName { get; set; }
        public string Directory { get; set; }

        public ObservableCollection<string> AvailableDirectories { get; set; }
        public ObservableCollection<string> AvailableConfigs { get; set; }
    }
}