using System.Windows;
using CadElectricalToolkit.UI.Views;

namespace CadElectricalToolkit.UiRunner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnOpenAttrNumbering_Click(object sender, RoutedEventArgs e)
        {
            var win = new AutoNumberingWindow(openBlockAttrTab: true);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnOpenFrameNumbering_Click(object sender, RoutedEventArgs e)
        {
            var win = new AutoNumberingWindow(openBlockAttrTab: false);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnOpenBatchAttr_Click(object sender, RoutedEventArgs e)
        {
            var win = new AutoNumberingWindow(tabIndex: 2);
            win.Owner = this;
            win.ShowDialog();
        }

        private void BtnOpenToolInfo_Click(object sender, RoutedEventArgs e)
        {
            var win = new ToolInfoWindow();
            win.Owner = this;
            win.ShowDialog();
        }
    }
}
