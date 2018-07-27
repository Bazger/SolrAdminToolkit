using System.Configuration;
using ReindexAutomation.Client.Domain;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Transitions;
using System.Windows.Controls;
using System;

namespace ReindexAutomation.Client.Domain
{
    public class MainWindowViewModel
    {
        public MainWindowViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            if (snackbarMessageQueue == null) throw new ArgumentNullException(nameof(snackbarMessageQueue));

            DemoItems = new[]
            {
                new DemoItem("Home", new Home(),
                    new[]
                    {
                        new DocumentationLink(DocumentationLinkType.Wiki,
                            $"{ConfigurationManager.AppSettings["GitHub"]}/wiki", "WIKI"),
                        DocumentationLink.DemoPageLink<Home>()
                    }
                )
            };
        }

        public DemoItem[] DemoItems { get; }
    }
}