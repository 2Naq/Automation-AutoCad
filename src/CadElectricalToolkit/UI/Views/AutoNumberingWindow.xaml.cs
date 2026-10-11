using System.Windows;
using CadElectricalToolkit.UI.Services;
using CadElectricalToolkit.UI.ViewModels;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Giao diện WPF hiện đại cho tính năng Đánh số thứ tự & Quản lý Attribute hàng loạt.
    /// Kiến trúc MVVM: View mỏng (thin code-behind), toàn bộ trạng thái và logic nằm ở AutoNumberingViewModel.
    /// </summary>
    public partial class AutoNumberingWindow : Window
    {
        public AutoNumberingViewModel ViewModel { get; }

        public AutoNumberingWindow(ICadInteractionService? cadService = null, int tabIndex = 1)
        {
            InitializeComponent();

            cadService ??= new CadInteractionService();
            ViewModel = new AutoNumberingViewModel(cadService)
            {
                OwnerWindow = this,
                SelectedTabIndex = tabIndex
            };

            DataContext = ViewModel;
        }

        public AutoNumberingWindow(int tabIndex) : this(null, tabIndex)
        {
        }

        public AutoNumberingWindow(bool openBlockAttrTab) : this(null, openBlockAttrTab ? 1 : 0)
        {
        }
    }
}
