using System.Collections.ObjectModel;
using System.Windows;

namespace EchoUI.Views;

public partial class SheetsEditorWindow : Window
{
    public ObservableCollection<SheetRow> Rows { get; } = [];

    public SheetsEditorWindow()
    {
        InitializeComponent();
        DataContext = this;

        for (var i = 0; i < 20; i++)
            Rows.Add(new SheetRow());
    }

    public sealed class SheetRow
    {
        public string A { get; set; } = string.Empty;
        public string B { get; set; } = string.Empty;
        public string C { get; set; } = string.Empty;
        public string D { get; set; } = string.Empty;
    }
}
