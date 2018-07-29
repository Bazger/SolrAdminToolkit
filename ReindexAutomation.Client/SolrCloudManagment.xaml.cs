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

        }

        private void LinksCard_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //Normalization for TextBox size when window size decreasing
            var tempWidth = GetSizeOfTextBox(LinksCard, LinksTextBox);
            if (tempWidth >= LinksTextBox.MinWidth)
            {
                LinksTextBox.Width = tempWidth;
            }

            tempWidth = GetSizeOfTextBox(ConfigurationCard, ConfigurationTextBox);
            if (tempWidth >= ConfigurationTextBox.MinWidth)
            {
                ConfigurationTextBox.Width = tempWidth;
            }
        }

        public static double GetSizeOfTextBox(Control parent, Control textBoxControl)
        {
            return parent.ActualWidth - parent.Margin.Left - parent.Padding.Left - parent.Padding.Right - textBoxControl.Margin.Right - textBoxControl.Margin.Left - 20;
        }
    }
}
