using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace SolrAdministrationToolKit.Client.Domain
{
    public class ConfigDialogViewModel : CommonDialogViewModel
    {
        private string _configName;
        private string _directory;


        public string ConfigName
        {
            get { return _configName; }
            set
            {
                this.MutateVerbose(ref _configName, value, RaisePropertyChanged());
            }
        }

        public string Directory
        {
            get { return _directory; }
            set
            {
                this.MutateVerbose(ref _directory, value, RaisePropertyChanged());
            }
        }

        public ObservableCollection<string> AvailableDirectories { get; set; }
        public ObservableCollection<string> AvailableConfigs { get; set; }

        public ICommand ConfigNameChanged => new RelayCommand(ConfigName_Changed);

        private void ConfigName_Changed(object args)
        {
            Directory = Path.Combine(Path.GetDirectoryName(Directory) ?? string.Empty, ConfigName);
        }
    }
}