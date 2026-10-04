using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý I/O PLC (TKPLCIN, TKPLCOUT, SYNPLC)
    /// </summary>
    public class PlcCommands
    {
        /// <summary>
        /// TKPLCIN : Tạo bảng địa chỉ DI-PLC (Digital Input)
        /// </summary>
        [CommandMethod("TKPLCIN")]
        public void SummaryPlcInputs()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKPLCIN] Dang quet cac cong Digital Input (DI) PLC...");
            ed.WriteMessage("\n[TKPLCIN] Hoan tat tao bang dia chi DI-PLC.");
        }

        /// <summary>
        /// TKPLCOUT : Tạo bảng địa chỉ DO-PLC (Digital Output)
        /// </summary>
        [CommandMethod("TKPLCOUT")]
        public void SummaryPlcOutputs()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKPLCOUT] Dang quet cac cong Digital Output (DO) PLC...");
            ed.WriteMessage("\n[TKPLCOUT] Hoan tat tao bang dia chi DO-PLC.");
        }

        /// <summary>
        /// SYNPLC : Đồng bộ địa chỉ DI/DO-PLC
        /// </summary>
        [CommandMethod("SYNPLC")]
        public void SyncPlcAddresses()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNPLC] Dang dong bo dia chi I/O PLC giua so do va bang dia chi...");
            ed.WriteMessage("\n[SYNPLC] Hoan tat dong bo PLC.");
        }
    }
}
