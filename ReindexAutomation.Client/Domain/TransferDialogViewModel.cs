using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace ReindexAutomation.Client.Domain
{
    public class TransferDialogViewModel : CommonDialogViewModel
    {
        private string _zkPath;
        private string _localDirectory;

        public string ZkPath
        {
            get { return _zkPath; }
            set
            {
                this.MutateVerbose(ref _zkPath, value, RaisePropertyChanged());
            }
        }

        public string LocalDirectory
        {
            get { return _localDirectory; }
            set
            {
                this.MutateVerbose(ref _localDirectory, value, RaisePropertyChanged());
            }
        }
    }
}