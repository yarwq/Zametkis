using System.Windows;

namespace Zametkis;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => Application.Current.Shutdown();
    }

    private void CreateNewZametki(object sender, RoutedEventArgs e)
    {
        var workDirectory = new WorkDirectory();
        Hide();
        workDirectory.Show();
    }

    private async void ReadZametki(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Заметки Zametkis (*.zametki)|*.zametki"
        };
        if (dialog.ShowDialog() != true)
            return;

        var workDirectory = new WorkDirectory();
        Hide();
        workDirectory.Show();
        await workDirectory.LoadDocument(dialog.FileName);
    }
}