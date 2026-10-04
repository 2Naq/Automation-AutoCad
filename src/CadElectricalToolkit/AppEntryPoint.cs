using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

[assembly: ExtensionApplication(typeof(CadElectricalToolkit.AppEntryPoint))]

namespace CadElectricalToolkit
{
    /// <summary>
    /// Điểm khởi đầu của Plugin trong AutoCAD
    /// </summary>
    public class AppEntryPoint : IExtensionApplication
    {
        public void Initialize()
        {
            CadDatabaseHelper.WriteMessage(
                "\n=======================================================" +
                "\n [CadElectricalToolkit] Da khoi tao thanh cong!" +
                "\n Ho tro: AutoCAD 2021 va cac phien ban cao hon." +
                "\n Go 'ELECINFO' hoac 'TT' de xem danh sach 23 lenh ho tro." +
                "\n=======================================================\n"
            );
        }

        public void Terminate()
        {
            // Don dep tai nguyen khi plugin bi go bo khoi phien lam viec
        }
    }
}
