using System.Collections.Generic;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;

namespace SolrAdministrationToolKit.Client.Domain
{
    public class SolrCloudManagmentViewModel
    {
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public SolrCloudManagmentViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            CommandButtons = new ObservableCollection<CommandButton>
            {
                new CommandButton
                {
                    Label = "Create\r\nCollection",
                    IconKinds = new List<string>{"Numeric1BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Add\r\nGW Core",
                    IconKinds = new List<string>{"Numeric2BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Reload OLD\r\ncollection",
                    IconKinds = new List<string>{"Numeric3BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Commit OLD\r\ncollection",
                    IconKinds = new List<string>{"Numeric4BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Reload NEW\r\ncollection",
                    IconKinds = new List<string>{"Numeric5BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Commit NEW\r\ncollection",
                    IconKinds = new List<string>{"Numeric6BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Create\r\nReplicas",
                    IconKinds = new List<string>{"Numeric7BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Remove\r\nReplicas",
                    IconKinds = new List<string>{"Numeric8BoxOutline"}
                },
                new CommandButton
                {
                    Label = "Create\r\nAlias",
                    IconKinds = new List<string>{"Numeric9BoxOutline" }
                }
            };
        }

        public ObservableCollection<CommandButton> CommandButtons { get; private set; }
    }
}
