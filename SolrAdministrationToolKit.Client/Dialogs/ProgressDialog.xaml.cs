using System.Windows;
using System.Windows.Controls;

namespace SolrAdministrationToolKit.Client.Dialogs
{
    /// <summary>
    /// Interaction logic for SampleProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : UserControl
    {
        public ProgressDialog(bool cancelButtonVisibility = true)
        {
            InitializeComponent();
            if (!cancelButtonVisibility)
            {
                Panel.Children.Remove(CancelButton);
                Panel.VerticalAlignment = VerticalAlignment.Center;
                Panel.HorizontalAlignment = HorizontalAlignment.Center;
                CancelButton.VerticalAlignment = VerticalAlignment.Center;
            }
        }
    }
}
