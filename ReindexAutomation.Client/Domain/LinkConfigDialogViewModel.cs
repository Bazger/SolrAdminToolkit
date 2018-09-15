using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace ReindexAutomation.Client.Domain
{
    public class LinkConfigDialogViewModel : CommonDialogViewModel
    {
        private string _configName;
        private string _collectionName;


        public string ConfigName
        {
            get { return _configName; }
            set
            {
                this.MutateVerbose(ref _configName, value, RaisePropertyChanged());
            }
        }

        public string CollectionName
        {
            get { return _collectionName; }
            set
            {
                this.MutateVerbose(ref _collectionName, value, RaisePropertyChanged());
            }
        }

        public ObservableCollection<string> AvailableCollections { get; set; }
        public ObservableCollection<string> AvailableConfigs { get; set; }

    }
}