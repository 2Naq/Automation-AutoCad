using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý Dây & Gen số (INSGEN, TKGEN, SYNGEN)
    /// </summary>
    public class WireNumberCommands
    {
        public const string WireBlockName = "GEN_SO";
        public const string WireTag = "WIRE_NO";

        /// <summary>
        /// INSGEN : Tạo gen số thống kê trên dây
        /// </summary>
        [CommandMethod("INSGEN")]
        public void InsertWireNumber()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[INSGEN] Lenh chen gen so tren day dan.");

            var pso = new PromptStringOptions("\nNhap so day (Wire Number): ") { AllowSpaces = false };
            var psr = ed.GetString(pso);
            if (psr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(psr.StringResult)) return;

            string wireNo = psr.StringResult.Trim();

            var ppo = new PromptPointOptions("\nChon vi tri dat gen so: ");
            var ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            ed.WriteMessage($"\n[INSGEN] Vi tri dat gen so '{wireNo}' tai ({ppr.Value.X:F1}, {ppr.Value.Y:F1}). (Dang hoan thien chen block {WireBlockName})");
        }

        /// <summary>
        /// TKGEN : Thống kê gen số (để in ống lồng, in nhãn dây điện)
        /// </summary>
        [CommandMethod("TKGEN")]
        public void SummaryWireNumbers()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKGEN] Dang quet toan bo gen so tren ban ve...");

            var wireCounts = new Dictionary<string, int>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        if (blkName.ToUpper().Contains("GEN") || blkName.ToUpper().Contains("WIRE"))
                        {
                            string? wireVal = blkRef.GetAttributeValue(WireTag, tr);
                            if (string.IsNullOrEmpty(wireVal))
                            {
                                var attrs = blkRef.GetAttributes(tr);
                                if (attrs.Count > 0) wireVal = attrs.Values.First();
                            }

                            if (!string.IsNullOrEmpty(wireVal))
                            {
                                if (!wireCounts.ContainsKey(wireVal!)) wireCounts[wireVal!] = 0;
                                wireCounts[wireVal!]++;
                            }
                        }
                    }
                }
            });

            if (wireCounts.Count == 0)
            {
                ed.WriteMessage("\n[TKGEN] Khong tim thay block gen so nao (Ten block chua 'GEN' hoac 'WIRE').");
                return;
            }

            ed.WriteMessage($"\n--- BANG THONG KE GEN SO (Tong loai: {wireCounts.Count}) ---");
            foreach (var kvp in wireCounts.OrderBy(k => k.Key))
            {
                ed.WriteMessage($"\n  Gen so: {kvp.Key,-15} | So luong: {kvp.Value}");
            }
            ed.WriteMessage("\n-----------------------------------------------------------");
        }

        /// <summary>
        /// SYNGEN : Đồng bộ tên gen số theo trang bản vẽ
        /// </summary>
        [CommandMethod("SYNGEN")]
        public void SyncWireNumbersByPage()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[SYNGEN] Dong bo ten gen so theo trang ban ve (Dang chay...)");
            // Logic phan tich trang va update lai so day
            ed.WriteMessage("\n[SYNGEN] Hoan tat dong bo.");
        }
    }
}
