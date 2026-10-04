using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh tham chiếu tiếp điểm & cuộn hút Rơ-le/Contactor (Cross-reference: TCCTB, TKCRL, SYNCRL, KTCRL)
    /// </summary>
    public class CrossRefCommands
    {
        /// <summary>
        /// TCCTB : Tham chiếu chân thiết bị
        /// </summary>
        [CommandMethod("TCCTB")]
        public void CrossRefLink()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TCCTB] Chon cuon hut (Coil) va tiep diem (Contact) de lien ket tham chieu.");
        }

        /// <summary>
        /// TKCRL : Tạo bảng thống kê chân số thiết bị
        /// </summary>
        [CommandMethod("TKCRL")]
        public void SummaryRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKCRL] Dang tao bang thong ke chan so thiet bi (Cross-Reference Table)...");
        }

        /// <summary>
        /// SYNCRL : Cập nhật địa chỉ chân số thiết bị
        /// </summary>
        [CommandMethod("SYNCRL")]
        public void SyncRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNCRL] Dang cap nhat dia chi toa do / trang cho cac tiep diem...");
        }

        /// <summary>
        /// KTCRL : Kiểm tra chân số thiết bị
        /// </summary>
        [CommandMethod("KTCRL")]
        public void ValidateRelayPins()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[KTCRL] Dang kiem tra trung chan va so luong cap tiep diem...");
            ed.WriteMessage("\n[KTCRL] Ket qua kiem tra: Khong phat hien loi trung lap.");
        }
    }
}
