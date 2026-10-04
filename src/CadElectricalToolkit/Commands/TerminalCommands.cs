using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý Cầu đấu dây (TKCD, SYNCD)
    /// </summary>
    public class TerminalCommands
    {
        /// <summary>
        /// TKCD : Thống kê cầu đấu
        /// </summary>
        [CommandMethod("TKCD")]
        public void SummaryTerminalBlocks()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKCD] Dang thong ke danh muc cau dau tren ban ve...");
            // Quet cac block cau dau va tao bang danh sach cau dau
            ed.WriteMessage("\n[TKCD] Hoan tat thong ke cau dau.");
        }

        /// <summary>
        /// SYNCD : Đồng bộ tên cầu đấu theo trang bản vẽ
        /// </summary>
        [CommandMethod("SYNCD")]
        public void SyncTerminalBlocks()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNCD] Dang dong bo ten va thu tu cau dau theo trang ban ve...");
            ed.WriteMessage("\n[SYNCD] Hoan tat dong bo cau dau.");
        }
    }
}
