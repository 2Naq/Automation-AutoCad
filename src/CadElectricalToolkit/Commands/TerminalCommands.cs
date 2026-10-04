using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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
        /// TKCD : Thống kê cầu đấu dây (Xuất bảng AutoCAD Table hoặc in ra dòng lệnh)
        /// </summary>
        [CommandMethod("TKCD")]
        public void SummaryTerminalBlocks()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- THONG KE CAU DAU DAY (TKCD) ---");

            var terminalList = new List<TerminalInfo>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        var attrs = blkRef.GetAttributes(tr);

                        if (blkName.ToUpper().Contains("CAU_DAU") || blkName.ToUpper().Contains("TERMINAL") || attrs.ContainsKey(BlockDefinitionHelper.TagTbName))
                        {
                            string tbTag = attrs.TryGetValue(BlockDefinitionHelper.TagTbName, out string? t) ? t : "TB1";
                            string pinNo = attrs.TryGetValue(BlockDefinitionHelper.TagPinNo, out string? p) ? p : "1";
                            string wireFrom = attrs.TryGetValue(BlockDefinitionHelper.TagWireFrom, out string? wf) ? wf : "";
                            string wireTo = attrs.TryGetValue(BlockDefinitionHelper.TagWireTo, out string? wt) ? wt : "";

                            terminalList.Add(new TerminalInfo
                            {
                                StripTag = tbTag,
                                PinNumber = pinNo,
                                WireFrom = wireFrom,
                                WireTo = wireTo,
                                Position = blkRef.Position
                            });
                        }
                    }
                }
            });

            if (terminalList.Count == 0)
            {
                ed.WriteMessage("\n[TKCD] Khong tim thay block cau dau nao tren ban ve (Dung lenh TAOBLOCKMAU de tao block mau CAU_DAU).");
                return;
            }

            // Sap xep theo Ten cau dau (TB1, TB2) roi den So chan (1, 2, 3...)
            var sortedTerminals = terminalList
                .OrderBy(t => t.StripTag)
                .ThenBy(t => int.TryParse(t.PinNumber, out int n) ? n : 9999)
                .ToList();

            ed.WriteMessage($"\nTim thay {sortedTerminals.Count} chan cau dau thuoc {sortedTerminals.Select(x => x.StripTag).Distinct().Count()} day cau dau:");
            foreach (var group in sortedTerminals.GroupBy(x => x.StripTag))
            {
                ed.WriteMessage($"\n > Day {group.Key}: {group.Count()} chan ({string.Join(", ", group.Select(x => x.PinNumber))})");
            }

            // Hoi nguoi dung co muon tao bang Table truc tiep tren ban ve khong
            var pko = new PromptKeywordOptions("\nBan co muon dat bang thong ke cau dau vao ban ve? [Yes/No] <Yes>: ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                var ppo = new PromptPointOptions("\nChon diem dat bang thong ke cau dau: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        string[] headers = new[] { "STT", "DAY CAU DAU", "CHAN SO", "DAY DEN (FROM)", "DAY DI (TO)", "GHI CHU" };
                        var rows = new List<string[]>();
                        for (int i = 0; i < sortedTerminals.Count; i++)
                        {
                            var item = sortedTerminals[i];
                            rows.Add(new[] { (i + 1).ToString(), item.StripTag, item.PinNumber, item.WireFrom, item.WireTo, "" });
                        }

                        TableHelper.InsertSummaryTable(db, tr, ppr.Value, "BANG THONG KE CAU DAU DAY (TERMINAL STRIP LIST)", headers, rows, colWidth: 32.0);
                    });
                    ed.WriteMessage("\n[TKCD] Da tao bang thong ke cau dau thanh cong tren ban ve!");
                }
            }
        }

        /// <summary>
        /// SYNCD : Đồng bộ và đánh lại số thứ tự chân cầu đấu tự động theo vị trí
        /// </summary>
        [CommandMethod("SYNCD")]
        public void SyncTerminalBlocks()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DONG BO THU TU CHAN CAU DAU (SYNCD) ---");

            var pso = new PromptStringOptions("\nNhap ten day cau dau can dong bo [Enter de chon TB1]: ")
            {
                DefaultValue = "TB1",
                UseDefaultValue = true
            };
            var psr = ed.GetString(pso);
            if (psr.Status != PromptStatus.OK) return;
            string targetStrip = string.IsNullOrWhiteSpace(psr.StringResult) ? "TB1" : psr.StringResult.Trim();

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = $"\nQuet chon cac chan cau dau thuoc day '{targetStrip}': " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon block cau dau nao.");
                return;
            }

            int count = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var termBlocks = new List<(BlockReference blk, Point3d pos)>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        termBlocks.Add((blkRef, blkRef.Position));
                    }
                }

                if (termBlocks.Count == 0) return;

                // Sap xep theo thu tu tu tren xuong duoi (Y giam dan), sau do tu trai sang phai (X tang dan)
                var sorted = termBlocks.OrderByDescending(b => b.pos.Y).ThenBy(b => b.pos.X).ToList();

                int pinNum = 1;
                foreach (var item in sorted)
                {
                    item.blk.SetAttributeValue(BlockDefinitionHelper.TagTbName, targetStrip, tr);
                    item.blk.SetAttributeValue(BlockDefinitionHelper.TagPinNo, pinNum.ToString(), tr);
                    pinNum++;
                    count++;
                }
            });

            ed.WriteMessage($"\n[SYNCD] Da danh lai so thu tu cho {count} chan cau dau thuoc day '{targetStrip}' (1 den {count}) thanh cong!");
        }

        private class TerminalInfo
        {
            public string StripTag { get; set; } = string.Empty;
            public string PinNumber { get; set; } = string.Empty;
            public string WireFrom { get; set; } = string.Empty;
            public string WireTo { get; set; } = string.Empty;
            public Point3d Position { get; set; }
        }
    }
}
