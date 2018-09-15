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

namespace SolrAdministrationToolKit.Client
{
    /// <summary>
    /// Interaction logic for Home.xaml
    /// </summary>
    public partial class ZookeeperManagment : UserControl
    {
        public ZookeeperManagment()
        {
            InitializeComponent();
        }

        private void DirectoriesTree_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            foreach (TreeViewItem dataNode in DirectoriesTree.Items)
            {
                dataNode.IsExpanded = true;
            }
        }
    }
}
