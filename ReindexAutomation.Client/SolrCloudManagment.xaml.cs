using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReindexAutomation.Client
{
    /// <summary>
    /// Interaction logic for Home.xaml
    /// </summary>
    public partial class SolrCloudManagment : UserControl
    {
        public SolrCloudManagment()
        {
            InitializeComponent();
        }

        private void SolrCloudManagment_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double newWindowHeight = e.NewSize.Height;
            double newWindowWidth = e.NewSize.Width;
            Debug.WriteLine(newWindowWidth + ":" + newWindowHeight);
            LinksTextBox.MaxWidth = e.NewSize.Width - 80;
        }
    }
}
