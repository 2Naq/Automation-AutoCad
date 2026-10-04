using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Các lệnh quản lý Bản vẽ, Khung tên & Mũi tên To-From (TKDMBV, SYNREV, SYNTF, GBV, TBV)
    /// </summary>
    public class SheetCommands
    {
        /// <summary>
        /// TKDMBV : Thống kê danh mục bản vẽ (Tạo bảng mục lục bản vẽ từ các khung tên)
        /// </summary>
        [CommandMethod("TKDMBV")]
        public void SummaryDrawingList()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- THONG KE DANH MUC BAN VE (TKDMBV) ---");

            var sheetList = new List<SheetItem>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        var attrs = blkRef.GetAttributes(tr);

                        // Kiem tra neu la khung ten (chua tag SHEET_NO hoac ten block chua KHUNG / TITLE)
                        if (attrs.ContainsKey(ElectricalConfig.TagSheetNumber) ||
                            blkName.ToUpper().Contains("KHUNG") ||
                            blkName.ToUpper().Contains("TITLE"))
                        {
                            string sheetNo = blkRef.GetAttributeValue(tr, ElectricalConfig.TagSheetNumber, "SHEET", "PAGE", "SO_TRANG") ?? "";
                            string sheetName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagSheetName, "TITLE", "TEN_BV", "NAME") ?? "BAN VE";
                            string rev = blkRef.GetAttributeValue(tr, ElectricalConfig.TagRevision, "REV", "LAN_SUA") ?? "00";
                            string date = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDate, "DATE", "NGAY") ?? DateTime.Now.ToString("yyyy-MM-dd");

                            if (!string.IsNullOrWhiteSpace(sheetNo))
                            {
                                sheetList.Add(new SheetItem
                                {
                                    SheetNo = sheetNo.Trim(),
                                    SheetName = sheetName.Trim(),
                                    Revision = rev.Trim(),
                                    Date = date.Trim(),
                                    Position = blkRef.Position
                                });
                            }
                        }
                    }
                }
            });

            if (sheetList.Count == 0)
            {
                ed.WriteMessage("\n[TKDMBV] Khong tim thay khung ten nao co Tag 'SHEET_NO' tren ban ve.");
                return;
            }

            // Sap xep danh sach theo So trang
            var sortedSheets = sheetList
                .OrderBy(s => int.TryParse(s.SheetNo, out int n) ? n : 9999)
                .ThenBy(s => s.SheetNo)
                .ToList();

            ed.WriteMessage($"\n[TKDMBV] Tim thay {sortedSheets.Count} trang ban ve:");
            for (int i = 0; i < sortedSheets.Count; i++)
            {
                ed.WriteMessage($"\n  Trang {sortedSheets[i].SheetNo,-6} | {sortedSheets[i].SheetName,-30} | Rev: {sortedSheets[i].Revision,-3} | Ngay: {sortedSheets[i].Date}");
            }

            // Hoi nguoi dung co muon dat bang Table vao ban ve khong
            var pko = new PromptKeywordOptions("\nBan co muon dat bang Danh muc ban ve vao CAD? [Yes/No] <Yes>: ");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Yes")
            {
                var ppo = new PromptPointOptions("\nChon diem dat bang Danh muc ban ve: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status == PromptStatus.OK)
                {
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        string[] headers = new[] { "STT", "SO BAN VE", "TEN BAN VE", "LAN SUA", "NGAY THANG" };
                        var rows = new List<string[]>();
                        for (int i = 0; i < sortedSheets.Count; i++)
                        {
                            var s = sortedSheets[i];
                            rows.Add(new[] { (i + 1).ToString(), s.SheetNo, s.SheetName, s.Revision, s.Date });
                        }

                        TableHelper.InsertSummaryTable(db, tr, ppr.Value, "DANH MUC BAN VE (DRAWING SHEET INDEX)", headers, rows, colWidth: 38.0);
                    });
                    ed.WriteMessage("\n[TKDMBV] Da tao bang Danh muc ban ve thanh cong!");
                }
            }
        }

        /// <summary>
        /// SYNREV : Cập nhật số lần thay đổi của trang bản vẽ (Revision History)
        /// </summary>
        [CommandMethod("SYNREV")]
        public void SyncRevisions()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- CAP NHAT REVISION TRANG BAN VE (SYNREV) ---");

            var psoRev = new PromptStringOptions("\nNhap ky hieu Revision moi <01>: ")
            {
                DefaultValue = "01",
                UseDefaultValue = true
            };
            var psrRev = ed.GetString(psoRev);
            if (psrRev.Status != PromptStatus.OK) return;
            string newRev = psrRev.StringResult.Trim();

            string defaultDate = DateTime.Now.ToString("yyyy-MM-dd");
            var psoDate = new PromptStringOptions($"\nNhap ngay thang sua doi <{defaultDate}>: ")
            {
                DefaultValue = defaultDate,
                UseDefaultValue = true
            };
            var psrDate = ed.GetString(psoDate);
            if (psrDate.Status != PromptStatus.OK) return;
            string newDate = psrDate.StringResult.Trim();

            var pkoScope = new PromptKeywordOptions("\nPham vi cap nhat: [All/Select] <All>: ");
            pkoScope.Keywords.Add("All");
            pkoScope.Keywords.Add("Select");
            pkoScope.Keywords.Default = "All";
            var pkrScope = ed.GetKeywords(pkoScope);
            if (pkrScope.Status != PromptStatus.OK) return;

            int updatedCount = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                List<ObjectId> targetIds;
                if (pkrScope.StringResult == "Select")
                {
                    var filter = SelectionHelper.CreateTypeFilter("INSERT");
                    var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nQuet chon cac khung ten can cap nhat: " }, filter);
                    if (selRes.Status != PromptStatus.OK || selRes.Value == null) return;
                    targetIds = selRes.Value.Cast<SelectedObject>().Select(x => x.ObjectId).ToList();
                }
                else
                {
                    targetIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                }

                foreach (var id in targetIds)
                {
                    if (tr.GetObject(id, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        var attrs = blkRef.GetAttributes(tr);
                        if (attrs.ContainsKey(ElectricalConfig.TagSheetNumber) || attrs.ContainsKey(ElectricalConfig.TagRevision))
                        {
                            blkRef.SetAttributeValue(ElectricalConfig.TagRevision, newRev, tr);
                            blkRef.SetAttributeValue(ElectricalConfig.TagDate, newDate, tr);
                            updatedCount++;
                        }
                    }
                }
            });

            ed.WriteMessage($"\n[SYNREV] Da cap nhat Revision = '{newRev}', Ngay = '{newDate}' cho {updatedCount} khung ten!");
        }

        /// <summary>
        /// SYNTF : Cập nhật địa chỉ mũi tên tín hiệu TO-FROM liên trang
        /// </summary>
        [CommandMethod("SYNTF")]
        public void SyncToFromArrows()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- CAP NHAT DIA CHI MUI TEN TO-FROM (SYNTF) ---");

            var arrowList = new List<ArrowItem>();

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                // 1. Quet tat ca khung ten de lay toa do pham vi tung trang
                var sheets = new List<(string SheetNo, Extents3d Box)>();
                var allBlocks = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

                foreach (var id in allBlocks)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        var attrs = blkRef.GetAttributes(tr);
                        if (attrs.ContainsKey(ElectricalConfig.TagSheetNumber))
                        {
                            string sNo = attrs[ElectricalConfig.TagSheetNumber];
                            try
                            {
                                sheets.Add((sNo, blkRef.GeometricExtents));
                            }
                            catch { }
                        }
                    }
                }

                // 2. Quet cac block mui ten tin hieu (Block chua tag SIG_NAME hoac TO_PAGE / FROM_PAGE)
                foreach (var id in allBlocks)
                {
                    if (tr.GetObject(id, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr).ToUpper();
                        var attrs = blkRef.GetAttributes(tr);

                        bool isArrow = blkName.Contains("TO") || blkName.Contains("FROM") || blkName.Contains("ARROW") || attrs.ContainsKey("SIG_NAME");
                        if (isArrow)
                        {
                            string sigName = blkRef.GetAttributeValue(tr, "SIG_NAME", "SIGNAL", "TAG", "NAME") ?? "";
                            if (!string.IsNullOrWhiteSpace(sigName))
                            {
                                // Xac dinh trang ma mui ten nay dang nam trong
                                string pageNo = "?";
                                foreach (var sheet in sheets)
                                {
                                    if (blkRef.Position.X >= sheet.Box.MinPoint.X && blkRef.Position.X <= sheet.Box.MaxPoint.X &&
                                        blkRef.Position.Y >= sheet.Box.MinPoint.Y && blkRef.Position.Y <= sheet.Box.MaxPoint.Y)
                                    {
                                        pageNo = sheet.SheetNo;
                                        break;
                                    }
                                }

                                arrowList.Add(new ArrowItem
                                {
                                    Block = blkRef,
                                    SignalName = sigName.Trim(),
                                    CurrentPage = pageNo,
                                    IsTo = blkName.Contains("TO")
                                });
                            }
                        }
                    }
                }

                // 3. Bat cap cac mui ten co cung SignalName va ghi so trang cheo nhau
                int linkedCount = 0;
                var groups = arrowList.GroupBy(x => x.SignalName, StringComparer.OrdinalIgnoreCase);

                foreach (var grp in groups)
                {
                    var items = grp.ToList();
                    if (items.Count >= 2)
                    {
                        // Mui ten nguon va mui ten dich
                        var source = items.FirstOrDefault(x => x.IsTo) ?? items[0];
                        var target = items.FirstOrDefault(x => !x.IsTo) ?? items[1];

                        // Ghi so trang dich vao nguon
                        source.Block.SetAttributeValue("TO_PAGE", target.CurrentPage, tr);
                        source.Block.SetAttributeValue("PAGE_REF", target.CurrentPage, tr);

                        // Ghi so trang nguon vao dich
                        target.Block.SetAttributeValue("FROM_PAGE", source.CurrentPage, tr);
                        target.Block.SetAttributeValue("PAGE_REF", source.CurrentPage, tr);

                        linkedCount += 2;
                    }
                }

                ed.WriteMessage($"\n[SYNTF] Da lien ket va cap nhat dia chi trang cho {linkedCount} mui ten tin hieu ({groups.Count()} cap tin hieu)!");
            });
        }

        /// <summary>
        /// GBV : Ghép bản vẽ outline / Sắp xếp khung bản vẽ theo ma trận hàng & cột
        /// </summary>
        [CommandMethod("GBV")]
        public void MergeDrawings()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- SAP XEP GHEP KHUNG BAN VE (GBV) ---");

            var filter = SelectionHelper.CreateTypeFilter("INSERT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nQuet chon cac khung ban ve can sap xep: " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count <= 1)
            {
                ed.WriteMessage("\nCan chon it nhat 2 khung ban ve.");
                return;
            }

            var pioCols = new PromptIntegerOptions("\nNhap so khung tren moi hang (Columns) <5>: ")
            {
                DefaultValue = 5,
                UseDefaultValue = true
            };
            var pirCols = ed.GetInteger(pioCols);
            if (pirCols.Status != PromptStatus.OK) return;
            int cols = Math.Max(1, pirCols.Value);

            var pdoDist = new PromptDoubleOptions("\nKhoang cach giua cac khung (Spacing X/Y) <50.0>: ")
            {
                DefaultValue = 50.0,
                UseDefaultValue = true
            };
            var pdrDist = ed.GetDouble(pdoDist);
            if (pdrDist.Status != PromptStatus.OK) return;
            double spacing = pdrDist.Value;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockList = new List<BlockReference>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blkRef)
                    {
                        blockList.Add(blkRef);
                    }
                }

                // Sap xep theo so trang
                blockList = blockList.OrderBy(b =>
                {
                    string sNo = b.GetAttributeValue(tr, ElectricalConfig.TagSheetNumber) ?? "";
                    return int.TryParse(sNo, out int n) ? n : 9999;
                }).ToList();

                Point3d baseOrigin = blockList.First().Position;
                double sheetWidth = 420.0;  // Chuan A3
                double sheetHeight = 297.0;

                try
                {
                    var ext = blockList.First().GeometricExtents;
                    sheetWidth = Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
                    sheetHeight = Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
                }
                catch { }

                for (int i = 0; i < blockList.Count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;

                    double newX = baseOrigin.X + col * (sheetWidth + spacing);
                    double newY = baseOrigin.Y - row * (sheetHeight + spacing);

                    var currentPos = blockList[i].Position;
                    var moveVec = new Vector3d(newX - currentPos.X, newY - currentPos.Y, 0);
                    blockList[i].TransformBy(Matrix3d.Displacement(moveVec));
                }

                ed.WriteMessage($"\n[GBV] Da sap xep {blockList.Count} khung ban ve thanh {cols} cot thanh cong!");
            });
        }

        /// <summary>
        /// TBV : Tách bản vẽ (Tách từng khung tên trong 1 file lớn thành các file DWG riêng rẽ)
        /// </summary>
        [CommandMethod("TBV")]
        public void SplitDrawings()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n--- TACH BAN VE THANH CAC FILE DWG RIENG (TBV) ---");

            var fbd = new FolderBrowserDialog
            {
                Description = "Chon thu muc de luu cac file DWG duoc tach ra:"
            };

            if (fbd.ShowDialog() != DialogResult.OK) return;
            string outDir = fbd.SelectedPath;

            int exportedSheets = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);
                foreach (var id in blockIds)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                    {
                        var attrs = blkRef.GetAttributes(tr);
                        if (attrs.ContainsKey(ElectricalConfig.TagSheetNumber))
                        {
                            string sheetNo = attrs[ElectricalConfig.TagSheetNumber].Trim();
                            string sheetName = attrs.TryGetValue(ElectricalConfig.TagSheetName, out string? name) ? name.Trim() : "SHEET";

                            // Loai bo ky tu dac biet khoi ten file
                            foreach (char c in Path.GetInvalidFileNameChars())
                            {
                                sheetName = sheetName.Replace(c, '_');
                                sheetNo = sheetNo.Replace(c, '_');
                            }

                            string outFileName = Path.Combine(outDir, $"{sheetNo}_{sheetName}.dwg");

                            try
                            {
                                // Tao database moi va wblock khung ban ve
                                using (var newDb = new Database(true, false))
                                {
                                    var ext = blkRef.GeometricExtents;
                                    var idCol = new ObjectIdCollection { blkRef.ObjectId };

                                    // Wblock sang file moi
                                    db.Wblock(newDb, idCol, blkRef.Position, DuplicateRecordCloning.Ignore);
                                    newDb.SaveAs(outFileName, DwgVersion.Current);
                                    exportedSheets++;
                                    ed.WriteMessage($"\n -> Da xuat: {Path.GetFileName(outFileName)}");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                ed.WriteMessage($"\n Loi khi tach trang {sheetNo}: {ex.Message}");
                            }
                        }
                    }
                }
            });

            ed.WriteMessage($"\n[TBV] Hoan tat tach {exportedSheets} trang ban ve vao thu muc: {outDir}");
        }

        private class SheetItem
        {
            public string SheetNo { get; set; } = string.Empty;
            public string SheetName { get; set; } = string.Empty;
            public string Revision { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public Point3d Position { get; set; }
        }

        private class ArrowItem
        {
            public BlockReference Block { get; set; } = null!;
            public string SignalName { get; set; } = string.Empty;
            public string CurrentPage { get; set; } = string.Empty;
            public bool IsTo { get; set; }
        }
    }
}
