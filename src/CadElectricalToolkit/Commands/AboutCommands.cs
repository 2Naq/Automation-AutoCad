using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.UI;
using CadElectricalToolkit.UI.Views;

namespace CadElectricalToolkit.Commands
{
    public class AboutCommands
    {
        [CommandMethod("ELECINFO")]
        [CommandMethod("TT")]
        public void ShowToolInfo()
        {
            var window = new ToolInfoWindow();
            window.ShowModal();
        }

        [CommandMethod("TAOBLOCKMAU")]
        [CommandMethod("SAMPLEBLOCKS")]
        public void CreateSampleBlocksCommand()
        {
            var ed = CadAccess.CadDatabaseHelper.ActiveEd;
            CadAccess.CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                CadAccess.BlockDefinitionHelper.CreateAllSampleBlocks(db, tr);
            });
            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n[TAOBLOCKMAU] Da khoi tao thanh cong cac Block dien mau:");
            ed.WriteMessage("\n 1. 'GEN_SO'    : Block danh so gen day (Tag: WIRE_NO)");
            ed.WriteMessage("\n 2. 'CAU_DAU'   : Block tram cau dau (Tags: TB_TAG, PIN_NO, WIRE_FROM, WIRE_TO)");
            ed.WriteMessage("\n 3. 'KHUNG_TEN' : Khung ten A3 chuan (Tags: SHEET_NO, SHEET_NAME, REV)");
            ed.WriteMessage("\n 4. 'RL_COIL'   : Cuon hut ro-le (Tags: TAG, PINS)");
            ed.WriteMessage("\n 5. 'RL_NO'     : Tiep diem thuong mo (Tags: TAG, PIN_NO)");
            ed.WriteMessage("\n 6. 'PLC_DI'    : Cong Digital Input PLC (Tags: PLC_ADDR, DESC)");
            ed.WriteMessage("\nGo lenh INSERT de chen thu hoac dung cac lenh INSGEN, TKGEN, TKCD!");
            ed.WriteMessage("\n=======================================================\n");
        }
    }
}
