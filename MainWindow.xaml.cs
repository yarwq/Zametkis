using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Zametkis;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Window zametki;
    private WorkDirectory workDirectory;
    public MainWindow()
    {
        InitializeComponent();
         zametki = Window.GetWindow(this);
         workDirectory = new WorkDirectory();
    }

    private void CreateNewZametki(object sender, RoutedEventArgs e)
    {
        //throw new NotImplementedException();
            zametki.Visibility = Visibility.Hidden;
            workDirectory.Show();
    }
    
    private void ReadZametki(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Заметки Zametkis (*.zametki)|*.zametki"
        };
        if (dialog.ShowDialog() != true)
            return;

        workDirectory.LoadDocument(dialog.FileName);
        zametki.Visibility = Visibility.Hidden;
        workDirectory.Show();
    }
}