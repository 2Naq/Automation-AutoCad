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

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh quản lý Dây & Gen số (INSGEN, TKGEN, SYNGEN)
    /// </summary>
    public class WireNumberCommands
    {
        private static string _lastWireNo = "101";

        /// <summary>
        /// INSGEN : Tạo / chèn gen số thống kê trên dây (hỗ trợ chèn liên tục tự động tăng)
        /// </summary>
        [CommandMethod("INSGEN")]
        public void InsertWireNumber()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var db = CadDatabaseHelper.ActiveDb;

            // Đảm bảo Block GEN_SO đã tồn tại trong bản vẽ
            CadDatabaseHelper.RunTransaction((tr, curDb) =>
            {
                BlockDefinitionHelper.EnsureWireNumberBlock(curDb, tr);
            });

            ed.WriteMessage("\n--- LENH CHEN GEN SO TREN DAY (INSGEN) ---");
            ed.WriteMessage("\nMeo: Nhan Enter de lay so goi y, click lien tiep tren day de dat gen so, nhan ESC de dung.");

            while (true)
            {
                var pso = new PromptStringOptions($"\nNhap so day <{_lastWireNo}>: ")
                {
                    AllowSpaces = false,
                    DefaultValue = _lastWireNo,
                    UseDefaultValue = true
                };
                var psr = ed.GetString(pso);
                if (psr.Status != PromptStatus.OK) break;

                string currentWireNo = string.IsNullOrWhiteSpace(psr.StringResult) ? _lastWireNo : psr.StringResult.Trim();
                _lastWireNo = currentWireNo;

                var ppo = new PromptPointOptions($"\nChon vi tri dat gen so '{currentWireNo}': ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) break;

                Point3d insertPt = ppr.Value;

                CadDatabaseHelper.RunTransaction((tr, curDb) =>
                {
                    var attributes = new Dictionary<string, string>
                    {
                        { BlockDefinitionHelper.TagWireNo, currentWireNo }
                    };
                    BlockDefinitionHelper.InsertBlock(curDb, tr, BlockDefinitionHelper.BlockWireNumber, insertPt, attributes);
                });

                ed.WriteMessage($" -> Da dat gen so: {currentWireNo}");

                // Tự động tăng số gợi ý nếu đuôi là số (ví dụ: 101 -> 102)
                _lastWireNo = IncrementSuffixNumber(currentWireNo);
            }
        }

        /// <summary>
        /// TKGEN : Thống kê gen số (để in ống lồng, tạo bảng trong CAD hoặc xuất Excel/CSV)
        /// </summary>
        [CommandMethod("TKGEN")]
        public void SummaryWireNumbers()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKGEN] Dang quet toan bo gen so tren ban ve...");

            var wireCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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
                            string? wireVal = blkRef.GetAttributeValue(BlockDefinitionHelper.TagWireNo, tr);
                            if (string.IsNullOrEmpty(wireVal))
                            {
                                var attrs = blkRef.GetAttributes(tr);
                                if (attrs.Count > 0) wireVal = attrs.Values.First();
                            }

                            if (!string.IsNullOrEmpty(wireVal))
                            {
                                wireVal = wireVal!.Trim();
                                if (!wireCounts.ContainsKey(wireVal)) wireCounts[wireVal] = 0;
                                wireCounts[wireVal]++;
                            }
                        }
                    }
                }
            });

            if (wireCounts.Count == 0)
            {
                ed.WriteMessage("\n[TKGEN] Khong tim thay block gen so nao tren ban ve (Dung lenh INSGEN de tao).");
                return;
            }

            var sortedList = wireCounts.OrderBy(k => k.Key).ToList();

            // 1. In ra Console
            ed.WriteMessage($"\n--- BANG THONG KE GEN SO ({sortedList.Count} loai day, Tong cong: {sortedList.Sum(x => x.Value)} dau gen) ---");
            for (int i = 0; i < sortedList.Count; i++)
            {
                ed.WriteMessage($"\n {i + 1,3}. Gen: {sortedList[i].Key,-15} | So luong: {sortedList[i].Value,4}");
            }
            ed.WriteMessage("\n--------------------------------------------------------------------------------");

            // 2. Hỏi đặt bảng Table vào bản vẽ
            var pko = new PromptKeywordOptions("\nBan co muon dat bang thong ke vao ban ve? [Yes/No] <Yes>: ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                var ppo = new PromptPointOptions("\nChon diem dat bang thong ke: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        string[] headers = new[] { "STT", "SO GEN / DAY", "SO LUONG", "GHI CHU" };
                        var rows = new List<string[]>();
                        for (int i = 0; i < sortedList.Count; i++)
                        {
                            rows.Add(new[] { (i + 1).ToString(), sortedList[i].Key, sortedList[i].Value.ToString(), "" });
                        }

                        TableHelper.InsertSummaryTable(db, tr, ppr.Value, "BANG THONG KE GEN SO IN ONG LONG", headers, rows);
                    });
                    ed.WriteMessage("\n[TKGEN] Da tao bang thong ke gen so thanh cong tren ban ve!");
                }
            }

            // 3. Hỏi xuất file CSV cho máy in ống lồng
            var pkoCsv = new PromptKeywordOptions("\nXuat danh sach gen so ra file CSV (may in ong long)? [Yes/No] <No>: ");
            pkoCsv.Keywords.Add("Yes");
            pkoCsv.Keywords.Add("No");
            pkoCsv.Keywords.Default = "No";

            var pkrCsv = ed.GetKeywords(pkoCsv);
            if (pkrCsv.Status == PromptStatus.OK && pkrCsv.StringResult == "Yes")
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Luu file danh sach in ong long (CSV)",
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"GenSo_InOngLong_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("STT,SoDay,SoLuong");
                    for (int i = 0; i < sortedList.Count; i++)
                    {
                        sb.AppendLine($"{i + 1},{sortedList[i].Key},{sortedList[i].Value}");
                    }
                    try
                    {
                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        ed.WriteMessage($"\n[TKGEN] Da xuat file in ong long: {sfd.FileName}");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[TKGEN] Loi xuat file: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// SYNGEN : Đồng bộ tên gen số theo trang bản vẽ (Sắp xếp theo tọa độ từ trên xuống, từ trái sang)
        /// </summary>
        [CommandMethod("SYNGEN")]
        public void SyncWireNumbersByPage()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- DONG BO TEN GEN SO THEO TRANG BAN VE (SYNGEN) ---");

            var pioPage = new PromptIntegerOptions("\nNhap so trang (Page Number) can dong bo <1>: ")
            {
                DefaultValue = 1,
                UseDefaultValue = true
            };
            var pirPage = ed.GetInteger(pioPage);
            if (pirPage.Status != PromptStatus.OK) return;
            int pageNo = pirPage.Value;

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = $"\nQuet chon cac block gen so thuoc trang {pageNo}: " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon block gen so nao.");
                return;
            }

            int count = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var wireBlocks = new List<(BlockReference blk, Point3d pos)>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        if (blkName.ToUpper().Contains("GEN") || blkName.ToUpper().Contains("WIRE"))
                        {
                            wireBlocks.Add((blkRef, blkRef.Position));
                        }
                    }
                }

                if (wireBlocks.Count == 0) return;

                // Sắp xếp tọa độ từ trên xuống dưới (Y giảm dần), sau đó từ trái sang phải (X tăng dần)
                var sorted = wireBlocks.OrderByDescending(b => b.pos.Y).ThenBy(b => b.pos.X).ToList();

                int wireSeq = 1;
                foreach (var item in sorted)
                {
                    string newWireNo = $"{pageNo}{wireSeq:D2}"; // Vi du: Trang 3 -> 301, 302, 303...
                    bool ok = item.blk.SetAttributeValue(BlockDefinitionHelper.TagWireNo, newWireNo, tr);
                    if (!ok)
                    {
                        // Neu khong co tag WIRE_NO, set vao attribute dau tien
                        foreach (ObjectId attId in item.blk.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference attRef)
                            {
                                attRef.TextString = newWireNo;
                                break;
                            }
                        }
                    }
                    wireSeq++;
                    count++;
                }
            });

            ed.WriteMessage($"\n[SYNGEN] Da cap nhat {count} gen so theo trang {pageNo} ({pageNo}01, {pageNo}02...) thanh cong!");
        }

        private static string IncrementSuffixNumber(string input)
        {
            if (string.IsNullOrEmpty(input)) return "1";

            int i = input.Length - 1;
            while (i >= 0 && char.IsDigit(input[i])) i--;

            string prefix = input.Substring(0, i + 1);
            string digits = input.Substring(i + 1);

            if (int.TryParse(digits, out int num))
            {
                return $"{prefix}{(num + 1).ToString().PadLeft(digits.Length, '0')}";
            }
            return input + "1";
        }
    }
}
