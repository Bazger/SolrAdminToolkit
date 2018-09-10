using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ReindexAutomation.Client.Domain;

namespace ReindexAutomation.Client.Domain
{
    public class ConfigDialogViewModel : SampleDialogViewModel
    {
        public string SelectedConfigName { get; set; }

        public ObservableCollection<string> AvailableConfigs { get; set; }
    }
}