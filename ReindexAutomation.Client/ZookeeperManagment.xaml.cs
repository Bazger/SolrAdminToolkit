using System.Windows;
using System.Windows.Controls;

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
