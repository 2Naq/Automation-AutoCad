using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.Core;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý I/O PLC (TKPLCIN, TKPLCOUT, SYNPLC)
    /// </summary>
    public class PlcCommands
    {
        /// <summary>
        /// TKPLCIN : Tạo bảng địa chỉ Digital Input (DI) PLC
        /// </summary>
        [CommandMethod("TKPLCIN")]
        public void SummaryPlcInputs()
        {
            SummarizePlcIo(isInput: true);
        }

        /// <summary>
        /// TKPLCOUT : Tạo bảng địa chỉ Digital Output (DO) PLC
        /// </summary>
        [CommandMethod("TKPLCOUT")]
        public void SummaryPlcOutputs()
        {
            SummarizePlcIo(isInput: false);
        }

        /// <summary>
        /// SYNPLC : Đồng bộ và đánh lại địa chỉ I/O PLC tự động tăng dần (Hỗ trợ định dạng X0, Y0, I0.0, Q0.0)
        /// </summary>
        [CommandMethod("SYNPLC")]
        public void SyncPlcAddresses()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DONG BO DIA CHI I/O PLC (SYNPLC) ---");

            var psoPrefix = new PromptStringOptions("\nNhap tien to dia chi PLC (vi du: X, Y, I, Q) <X>: ")
            {
                DefaultValue = "X",
                UseDefaultValue = true
            };
            var psrPrefix = ed.GetString(psoPrefix);
            if (psrPrefix.Status != PromptStatus.OK) return;
            string prefix = psrPrefix.StringResult.Trim().ToUpper();

            var pioStart = new PromptIntegerOptions("\nNhap chi so bat dau <0>: ")
            {
                DefaultValue = 0,
                UseDefaultValue = true
            };
            var pirStart = ed.GetInteger(pioStart);
            if (pirStart.Status != PromptStatus.OK) return;
            int startIndex = pirStart.Value;

            var pkoMode = new PromptKeywordOptions("\nChon he dem: [Octal(BatPhan)/Decimal(ThapPhan)] <Octal>: ");
            pkoMode.Keywords.Add("Octal");
            pkoMode.Keywords.Add("Decimal");
            pkoMode.Keywords.Default = "Octal";
            var pkrMode = ed.GetKeywords(pkoMode);
            bool isOctal = pkrMode.Status == PromptStatus.OK && pkrMode.StringResult == "Octal";

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nQuet chon cac block PLC I/O can danh so dia chi: " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon block PLC nao.");
                return;
            }

            int count = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var plcBlocks = new List<BlockReference>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        plcBlocks.Add(blkRef);
                    }
                }

                // Sap xep tu tren xuong duoi, tu trai sang phai
                plcBlocks = plcBlocks.OrderByDescending(b => b.Position.Y).ThenBy(b => b.Position.X).ToList();

                int currentNum = startIndex;
                foreach (var blk in plcBlocks)
                {
                    string addrStr = isOctal ? Convert.ToString(currentNum, 8) : currentNum.ToString();
                    string fullAddress = $"{prefix}{addrStr}";

                    blk.SetAttributeValue(ElectricalConfig.TagPlcAddress, fullAddress, tr);
                    blk.SetAttributeValue("ADDRESS", fullAddress, tr);

                    currentNum++;
                    count++;
                }
            });

            ed.WriteMessage($"\n[SYNPLC] Da danh lai dia chi cho {count} block PLC tu '{prefix}{startIndex}' thanh cong!");
        }

        private static void SummarizePlcIo(bool isInput)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            string ioType = isInput ? "DIGITAL INPUT (DI)" : "DIGITAL OUTPUT (DO)";
            ed.WriteMessage($"\n--- THONG KE DIA CHI PLC {ioType} ---");

            var plcList = new List<PlcItem>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr).ToUpper();
                        var attrs = blkRef.GetAttributes(tr);

                        bool matchType = isInput
                            ? (blkName.Contains("IN") || blkName.Contains("DI") || attrs.Any(a => a.Value.StartsWith("X", StringComparison.OrdinalIgnoreCase) || a.Value.StartsWith("I", StringComparison.OrdinalIgnoreCase)))
                            : (blkName.Contains("OUT") || blkName.Contains("DO") || attrs.Any(a => a.Value.StartsWith("Y", StringComparison.OrdinalIgnoreCase) || a.Value.StartsWith("Q", StringComparison.OrdinalIgnoreCase)));

                        if (matchType || attrs.ContainsKey(ElectricalConfig.TagPlcAddress) || attrs.ContainsKey("ADDRESS"))
                        {
                            string addr = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPlcAddress, "ADDRESS", "ADDR", "PLC_ADDR") ?? "";
                            string desc = blkRef.GetAttributeValue(tr, ElectricalConfig.TagPlcDescription, "DESC", "NAME", "DESCRIPTION") ?? "";

                            if (!string.IsNullOrWhiteSpace(addr))
                            {
                                plcList.Add(new PlcItem
                                {
                                    Address = addr.Trim(),
                                    Description = desc.Trim(),
                                    Position = blkRef.Position
                                });
                            }
                        }
                    }
                }
            });

            if (plcList.Count == 0)
            {
                ed.WriteMessage($"\n[TKPLC] Khong tim thay block PLC {ioType} nao tren ban ve (Dung lenh TAOBLOCKMAU de tao block PLC_DI).");
                return;
            }

            var sortedPlc = plcList.OrderBy(x => x.Address).ToList();
            ed.WriteMessage($"\n[TKPLC] Tim thay {sortedPlc.Count} dia chi {ioType}:");
            for (int i = 0; i < sortedPlc.Count; i++)
            {
                ed.WriteMessage($"\n  {i + 1,2}. Dia chi: {sortedPlc[i].Address,-10} | Mo ta: {sortedPlc[i].Description}");
            }

            // Hoi dat bang vao CAD
            var pko = new PromptKeywordOptions($"\nBan co muon dat bang thong ke dia chi {ioType} vao ban ve? [Yes/No] <Yes>: ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                var ppo = new PromptPointOptions("\nChon diem dat bang: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        string[] headers = new[] { "STT", "DIA CHI PLC", "MO TA CHUC NANG / THIET BI", "VI TRI (X, Y)" };
                        var rows = new List<string[]>();
                        for (int i = 0; i < sortedPlc.Count; i++)
                        {
                            var item = sortedPlc[i];
                            rows.Add(new[] { (i + 1).ToString(), item.Address, item.Description, $"({item.Position.X:F0}, {item.Position.Y:F0})" });
                        }

                        TableHelper.InsertSummaryTable(db, tr, ppr.Value, $"BANG DIA CHI PLC {ioType}", headers, rows, colWidth: 35.0);
                    });
                    ed.WriteMessage("\n[TKPLC] Da tao bang dia chi PLC thanh cong!");
                }
            }
        }

        private class PlcItem
        {
            public string Address { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Point3d Position { get; set; }
        }
    }
}
