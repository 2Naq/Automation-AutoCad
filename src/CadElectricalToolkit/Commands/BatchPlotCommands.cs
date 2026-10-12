using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.UI;
using CadElectricalToolkit.UI.Services;
using CadElectricalToolkit.UI.ViewModels;
using CadElectricalToolkit.UI.Views;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh AutoCAD mở hộp thoại In Hàng Loạt và Xuất PDF Thông Minh
    /// </summary>
    public class BatchPlotCommands
    {
        [CommandMethod("INBV")]
        [CommandMethod("INPDF")]
        [CommandMethod("PUBLISHPDF")]
        [CommandMethod("PLOTPDF")]
        [CommandMethod("INHANG")]
        public void ShowBatchPlotDialog()
        {
            var cadService = new CadInteractionService();
            var viewModel = new BatchPlotViewModel(cadService);
            var window = new BatchPlotWindow(viewModel);
            window.ShowModal();
        }
    }
}
