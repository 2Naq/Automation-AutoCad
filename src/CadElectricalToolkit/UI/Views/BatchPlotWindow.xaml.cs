using System.Windows;
using CadElectricalToolkit.UI.ViewModels;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Interaction logic for BatchPlotWindow.xaml
    /// </summary>
    public partial class BatchPlotWindow : Window
    {
        public BatchPlotViewModel ViewModel { get; }

        public BatchPlotWindow(BatchPlotViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            viewModel.SetOwnerWindow(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
