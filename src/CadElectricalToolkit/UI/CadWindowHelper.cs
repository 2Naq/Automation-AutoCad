using System;
using System.Windows;
using System.Windows.Interop;
using cadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadElectricalToolkit.UI
{
    /// <summary>
    /// Tiện ích hỗ trợ quản lý và hiển thị cửa sổ WPF trong môi trường AutoCAD .NET
    /// </summary>
    public static class CadWindowHelper
    {
        /// <summary>
        /// Hiển thị WPF Window dưới dạng Modal Dialog gắn liền với cửa sổ chính của AutoCAD
        /// </summary>
        public static bool? ShowModal(this Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            try
            {
                if (cadApp.MainWindow != null && cadApp.MainWindow.Handle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(window);
                    helper.Owner = cadApp.MainWindow.Handle;
                }
            }
            catch
            {
                // Bỏ qua lỗi gán Owner khi chạy trong chế độ Test Runner hoặc UI Designer
            }

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return window.ShowDialog();
        }
    }
}
