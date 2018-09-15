using System.Windows;
using System.Windows.Controls;

namespace SolrAdministrationToolKit.Client
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
            //Normalization for TextBox size when window size decreasing ONLY WHEN sclollbar HrozizontalVisibility equals Auto
            //var tempWidth = GetWidthOfTextBox(LinksCard, LinksTextBox);
            //if (tempWidth >= LinksTextBox.MinWidth)
            //{
            //    LinksTextBox.Width = tempWidth;
            //}

            //tempWidth = GetWidthOfTextBox(ConfigurationCard, ConfigurationTextBox);
            //if (tempWidth >= ConfigurationTextBox.MinWidth)
            //{
            //    ConfigurationTextBox.Width = tempWidth;
            //}

            //var tempHeight = GetHeightOfTextBox(ConfigurationCard, ConfigurationTextBox);
            //if (tempHeight >= ConfigurationTextBox.MinHeight)
            //{
            //    ConfigurationTextBox.MinHeight = tempHeight;
            //}
        }

        public static double GetWidthOfTextBox(Control parent, Control textBoxControl)
        {
            return parent.ActualWidth - parent.Margin.Left - parent.Padding.Left - parent.Padding.Right - textBoxControl.Margin.Right - textBoxControl.Margin.Left - 20;
        }

        public static double GetHeightOfTextBox(Control parent, Control textBoxControl)
        {
            return parent.ActualHeight - parent.Margin.Top - parent.Padding.Top - parent.Padding.Bottom - textBoxControl.Margin.Bottom - textBoxControl.Margin.Top - 20;
        }
    }
}
