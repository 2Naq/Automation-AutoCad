using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý Bản vẽ, Khung tên & Mũi tên To-From (TKDMBV, SYNREV, SYNTF, GBV, TBV)
    /// </summary>
    public class SheetCommands
    {
        /// <summary>
        /// TKDMBV : Thống kê danh mục bản vẽ
        /// </summary>
        [CommandMethod("TKDMBV")]
        public void SummaryDrawingList()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKDMBV] Dang quet danh muc khung ten de tao bang danh muc ban ve...");
        }

        /// <summary>
        /// SYNREV : Cập nhật số lần thay đổi của trang bản vẽ (Revision History)
        /// </summary>
        [CommandMethod("SYNREV")]
        public void SyncRevisions()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNREV] Cap nhat so lan thay doi (Rev) cua trang ban ve...");
        }

        /// <summary>
        /// SYNTF : Cập nhật địa chỉ mũi tên TO-FROM
        /// </summary>
        [CommandMethod("SYNTF")]
        public void SyncToFromArrows()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNTF] Dang quet cac cap tin hieu mui ten To/From va cap nhat so trang...");
        }

        /// <summary>
        /// GBV : Ghép bản vẽ outline
        /// </summary>
        [CommandMethod("GBV")]
        public void MergeDrawings()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[GBV] Lenh ghep ban ve outline.");
        }

        /// <summary>
        /// TBV : Tách bản vẽ
        /// </summary>
        [CommandMethod("TBV")]
        public void SplitDrawings()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TBV] Lenh tach tung khung ten ban ve thanh file DWG rieng biet.");
        }
    }
}
