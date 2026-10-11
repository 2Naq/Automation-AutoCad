using System.Windows;
using CadElectricalToolkit.UI.ViewModels;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Giao diện WPF hiện đại hiển thị danh sách lệnh và thông tin Plugin.
    /// Kiến trúc MVVM: View mỏng (thin code-behind), toàn bộ trạng thái và logic nằm ở ToolInfoViewModel.
    /// </summary>
    public partial class ToolInfoWindow : Window
    {
        public ToolInfoViewModel ViewModel { get; }

        public ToolInfoWindow()
        {
            InitializeComponent();
            ViewModel = new ToolInfoViewModel { OwnerWindow = this };
            DataContext = ViewModel;
        }
    }
}
