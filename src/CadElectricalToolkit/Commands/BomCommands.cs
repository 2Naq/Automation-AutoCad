using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.Core;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh thống kê thiết bị & xuất dữ liệu Excel (TKDMTB, CAD2EXCEL)
    /// </summary>
    public class BomCommands
    {
        /// <summary>
        /// TKDMTB : Thống kê danh mục thiết bị (BOM - Bill of Materials)
        /// Tự động quét toàn bộ thiết bị, gom nhóm theo Model/Part Number, đếm số lượng và tạo bảng BOM
        /// </summary>
        [CommandMethod("TKDMTB")]
        public void SummaryDeviceList()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- THONG KE DANH MUC THIET BI (BOM - TKDMTB) ---");

            var rawDevices = new List<BomDeviceItem>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr).ToUpper();

                        // Bo qua cac block gen day va khung ten
                        if (blkName.Contains("GEN") || blkName.Contains("KHUNG") || blkName.Contains("TITLE"))
                        {
                            continue;
                        }

                        var attrs = blkRef.GetAttributes(tr);
                        string? tag = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "TAG", "NAME", "MA_TB");
                        
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            string model = blkRef.GetAttributeValue(tr, "MODEL", "PART_NUMBER", "TYPE", "CATALOG") ?? blkName;
                            string mfg = blkRef.GetAttributeValue(tr, "MFG", "BRAND", "HANG_SX", "MAKER") ?? "---";
                            string desc = blkRef.GetAttributeValue(tr, "DESC", "DESCRIPTION", "TEN_TB", "MO_TA") ?? blkName;

                            rawDevices.Add(new BomDeviceItem
                            {
                                Tag = tag!.Trim(),
                                Model = model.Trim(),
                                Manufacturer = mfg.Trim(),
                                Description = desc.Trim(),
                                Position = blkRef.Position
                            });
                        }
                    }
                }
            });

            if (rawDevices.Count == 0)
            {
                ed.WriteMessage("\n[TKDMTB] Khong tim thay block thiet bi nao co Tag 'NAME' hoac 'TAG' tren ban ve.");
                return;
            }

            // Gom nhom theo Model hoac Description de tinh so luong
            var bomGroups = rawDevices
                .GroupBy(d => new { d.Model, d.Manufacturer, d.Description })
                .OrderBy(g => g.Key.Model)
                .ToList();

            ed.WriteMessage($"\n[TKDMTB] Tim thay {rawDevices.Count} thiet bi ({bomGroups.Count} chung loai vat tu):");
            int idx = 1;
            foreach (var g in bomGroups)
            {
                string tagsStr = string.Join(", ", g.Select(x => x.Tag));
                ed.WriteMessage($"\n  {idx,2}. Model: {g.Key.Model,-18} | SL: {g.Count(),3} | Hang: {g.Key.Manufacturer,-10} | Vi tri: {tagsStr}");
                idx++;
            }

            // 1. Hoi dat bang BOM vao CAD
            var pko = new PromptKeywordOptions("\nBan co muon dat bang BOM vao ban ve? [Yes/No] <Yes>: ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                var ppo = new PromptPointOptions("\nChon diem dat bang BOM: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        string[] headers = new[] { "STT", "MODEL / MA THIET BI", "MO TA KY THUAT", "HANG SX", "SO LUONG", "VI TRI (TAG)" };
                        var rows = new List<string[]>();
                        int stt = 1;

                        foreach (var g in bomGroups)
                        {
                            string tagsStr = string.Join(", ", g.Select(x => x.Tag));
                            rows.Add(new[]
                            {
                                stt.ToString(),
                                g.Key.Model,
                                g.Key.Description,
                                g.Key.Manufacturer,
                                g.Count().ToString(),
                                tagsStr
                            });
                            stt++;
                        }

                        TableHelper.InsertSummaryTable(db, tr, ppr.Value, "BANG THONG KE DANH MUC THIET BI (BOM)", headers, rows, colWidth: 35.0);
                    });
                    ed.WriteMessage("\n[TKDMTB] Da tao bang BOM thanh cong tren ban ve!");
                }
            }

            // 2. Hoi xuat Excel/CSV
            var pkoCsv = new PromptKeywordOptions("\nXuat danh muc thiet bi ra file CSV/Excel de bao gia / mua hang? [Yes/No] <No>: ");
            pkoCsv.Keywords.Add("Yes");
            pkoCsv.Keywords.Add("No");
            pkoCsv.Keywords.Default = "No";

            var pkrCsv = ed.GetKeywords(pkoCsv);
            if (pkrCsv.Status == PromptStatus.OK && pkrCsv.StringResult == "Yes")
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Luu bang BOM thiet bi (CSV)",
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"BOM_ThietBi_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("STT,Model,MoTa,HangSX,SoLuong,ViTriTags");
                    int stt = 1;
                    foreach (var g in bomGroups)
                    {
                        string tagsStr = string.Join(";", g.Select(x => x.Tag));
                        sb.AppendLine($"{stt},\"{g.Key.Model}\",\"{g.Key.Description}\",\"{g.Key.Manufacturer}\",{g.Count()},\"{tagsStr}\"");
                        stt++;
                    }

                    try
                    {
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        ed.WriteMessage($"\n[TKDMTB] Da xuat file BOM: {sfd.FileName}");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[TKDMTB] Loi khi ghi file: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// CAD2EXCEL : Xuất thông tin Block Attribute và Text ra file CSV/Excel
        /// </summary>
        [CommandMethod("CAD2EXCEL")]
        public void ExportCadToExcel()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChon cac Text hoac Block Attribute muon xuat Excel: " });
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon doi tuong nao.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("EntityType,BlockName,Handle,Tag/Property,Value,PosX,PosY");

            int exportedCount = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);

                    if (ent is DBText dbText)
                    {
                        sb.AppendLine($"Text,,{dbText.Handle},TextString,\"{dbText.TextString.Replace("\"", "\"\"")}\",{dbText.Position.X:F2},{dbText.Position.Y:F2}");
                        exportedCount++;
                    }
                    else if (ent is MText mText)
                    {
                        sb.AppendLine($"MText,,{mText.Handle},Contents,\"{mText.Text.Replace("\"", "\"\"")}\",{mText.Location.X:F2},{mText.Location.Y:F2}");
                        exportedCount++;
                    }
                    else if (ent is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        foreach (ObjectId attId in blkRef.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                            {
                                sb.AppendLine($"BlockAttribute,{blkName},{blkRef.Handle},{attRef.Tag},\"{attRef.TextString.Replace("\"", "\"\"")}\",{attRef.Position.X:F2},{attRef.Position.Y:F2}");
                                exportedCount++;
                            }
                        }
                    }
                }
            });

            if (exportedCount == 0)
            {
                ed.WriteMessage("\nKhong tim thay du lieu Text hoac Attribute de xuat.");
                return;
            }

            var sfd = new SaveFileDialog
            {
                Title = "Chon noi luu file Excel / CSV",
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"CadExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    ed.WriteMessage($"\n[CAD2EXCEL] Da xuat thanh cong {exportedCount} dong du lieu ra file: {sfd.FileName}");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[CAD2EXCEL] Loi khi ghi file: {ex.Message}");
                }
            }
        }

        private class BomDeviceItem
        {
            public string Tag { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string Manufacturer { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Autodesk.AutoCAD.Geometry.Point3d Position { get; set; }
        }
    }
}
