using System.Configuration;
using MaterialDesignThemes.Wpf;
using System;

namespace SolrAdministrationToolKit.Client.Domain
{
    public class MainWindowViewModel
    {
        public MainWindowViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            if (snackbarMessageQueue == null) throw new ArgumentNullException(nameof(snackbarMessageQueue));

            Sections = new[]
            {
                new Section("Home", "Home", new Home(),
                    new[]
                    {
                        new DocumentationLink(DocumentationLinkType.Wiki,
                            $"{ConfigurationManager.AppSettings["GitHub"]}/wiki", "WIKI"),
                        DocumentationLink.DemoPageLink<Home>()
                    }
                ),
                new Section("SolrCloud","SolrCloud Managment", new SolrCloudManagment{DataContext = new SolrCloudManagmentViewModel(snackbarMessageQueue)}, null),
                new Section("Index","Solr Index Managment", new SolrIndexManagment{DataContext = new SolrIndexManagmentViewModel(snackbarMessageQueue)}, null),
                new Section("Service","Solr Service Managment", new SolrServiceManagment{DataContext = new SolrServiceMangmentViewModel(snackbarMessageQueue)}, null),
                new Section("Data Import","Data Import Managment", new DataImportManagment{DataContext = new DataImportManagmentViewModel(snackbarMessageQueue)}, null),
                new Section("Zookeeper","Zookeper Managment", new ZookeeperManagment{DataContext = new ZookeeperManagmentViewModel(snackbarMessageQueue)}, null)
            };
        }

        public Section[] Sections { get; }
    }
}