using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.Core;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh truy vết liên kết trang, tìm vị trí cuộn coil hoặc mũi tên tín hiệu Arrow To/From
    /// (TIMTRANG, TRACELINK, TRACE, XOALINK, CLRLINK)
    /// </summary>
    public class TraceCommands
    {
        public const string TraceLayerName = "_TRACE_GUIDE";

        /// <summary>
        /// TIMTRANG / TRACELINK / TRACE:
        /// Tìm xem cuộn coil hoặc arrow block đang thuộc về trang nào.
        /// Khi tìm được sẽ vẽ đường line chỉ dẫn từ đối tượng nguồn đến khung tên Frame-a4 của trang đích.
        /// </summary>
        [CommandMethod("TIMTRANG")]
        [CommandMethod("TRACELINK")]
        [CommandMethod("TRACE")]
        [CommandMethod("TIMCOIL")]
        [CommandMethod("TIMLINK")]
        public void TraceTargetPage()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n==================================================================");
            ed.WriteMessage("\n  TRUY VET LIEN KET TRANG & VE LINE CHI DAN (TIMTRANG / TRACE)");
            ed.WriteMessage("\n==================================================================");

            while (true)
            {
                var peo = new PromptEntityOptions("\nChon block Tiep diem, Cuon coil hoac Mui ten (Arrow To/From) can tim trang [Xoa/Thoat] <Thoat>: ");
                peo.SetRejectMessage("\nDoi tuong duoc chon phai la Block (BlockReference).");
                peo.AddAllowedClass(typeof(BlockReference), true);
                peo.Keywords.Add("Xoa");
                peo.Keywords.Add("Thoat");

                var per = ed.GetEntity(peo);

                if (per.Status == PromptStatus.Keyword)
                {
                    if (string.Equals(per.StringResult, "Xoa", StringComparison.OrdinalIgnoreCase))
                    {
                        ClearAllTraceGuidesCommand();
                        continue;
                    }
                    break;
                }

                if (per.Status != PromptStatus.OK)
                {
                    break;
                }

                ObjectId selectedId = per.ObjectId;
                Point3d? targetZoomPoint = null;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var ent = tr.GetObject(selectedId, OpenMode.ForRead);
                    BlockReference? srcBlkRef = ent as BlockReference;

                    // Neu nguoi dung click vao Attribute thi lay block cha
                    if (ent is AttributeReference att)
                    {
                        srcBlkRef = tr.GetObject(att.OwnerId, OpenMode.ForRead) as BlockReference;
                    }

                    if (srcBlkRef == null)
                    {
                        ed.WriteMessage("\n[CANH BAO] Khong doc duoc thong tin block duoc chon.");
                        return;
                    }

                    string blkName = srcBlkRef.GetEffectiveBlockName(tr);

                    // 1. Trích xuất thông tin liên kết (Địa chỉ đích & Tên tín hiệu/thiết bị)
                    var (targetAddress, targetSheetNo, wireOrDevName, linkType) = ExtractLinkInfo(srcBlkRef, tr);

                    // 2. Quét tất cả khung tên để tìm khung tương ứng
                    var frames = SheetFrameHelper.ScanAllFramesAuto(db, tr);
                    if (frames.Count == 0)
                    {
                        ed.WriteMessage("\n[LOI] Khong tim thay khung ten ban ve nao tren ban ve de dinh vi.");
                        return;
                    }

                    // Nếu đối tượng không có địa chỉ đích (ví dụ là chính cuộn hút Coil hoặc block khác),
                    // xác định xem đối tượng này đang nằm trong Frame nào
                    if (string.IsNullOrWhiteSpace(targetSheetNo) && string.IsNullOrWhiteSpace(targetAddress))
                    {
                        var currentFrame = SheetFrameHelper.FindFrameContaining(frames, srcBlkRef.Position);
                        if (currentFrame != null)
                        {
                            targetSheetNo = currentFrame.SheetNo;
                            targetAddress = $"Trang {currentFrame.SheetNo}";
                            linkType = "Doi tuong tren trang";
                            ed.WriteMessage($"\n-> Doi tuong '{blkName}' thuoc ve Khung ten Trang: '{currentFrame.SheetNo}'");
                        }
                        else
                        {
                            ed.WriteMessage($"\n[TIMTRANG] Block '{blkName}' khong chua thuoc tinh dia chi lien ket (ADDRESS_TO, ADDRESS_COIL...) va khong nam trong khung ten nao.");
                            return;
                        }
                    }

                    ed.WriteMessage($"\n-> Nguon: Block '{blkName}' [{linkType}]");
                    if (!string.IsNullOrWhiteSpace(wireOrDevName))
                    {
                        ed.WriteMessage($" (Tin hieu/Thiet bi: '{wireOrDevName}')");
                    }
                    ed.WriteMessage($" -> Dia chi can tim: '{targetAddress}' (Trang {targetSheetNo})");

                    SheetFrame? targetFrame = frames.FirstOrDefault(f => MatchesSheetNo(f.SheetNo, targetSheetNo));

                    if (targetFrame == null)
                    {
                        ed.WriteMessage($"\n[CANH BAO] Khong tim thay khung ten nao co so trang '{targetSheetNo}'.");
                        ed.WriteMessage($"\n          Cac trang hien co tren ban ve: [{string.Join(", ", frames.Select(f => f.SheetNo))}]");
                        return;
                    }

                    // 3. Xác định vị trí điểm nguồn (Source Point & Box)
                    Point3d srcCenter = srcBlkRef.Position;
                    Extents3d srcExtents;
                    bool hasSrcExt = SheetFrameHelper.TryGetExtents(srcBlkRef, out srcExtents);
                    if (hasSrcExt)
                    {
                        srcCenter = new Point3d(
                            (srcExtents.MinPoint.X + srcExtents.MaxPoint.X) / 2.0,
                            (srcExtents.MinPoint.Y + srcExtents.MaxPoint.Y) / 2.0,
                            0);
                    }
                    else
                    {
                        srcExtents = new Extents3d(
                            new Point3d(srcCenter.X - 5, srcCenter.Y - 5, 0),
                            new Point3d(srcCenter.X + 5, srcCenter.Y + 5, 0));
                    }

                    // 4. Xác định vị trí điểm đích trên khung tên (Target Point & Box)
                    // Ưu tiên vị trí ô số trang (A00 / TRANG) của khung tên đích
                    var (targetPt, targetBoxExt) = LocateTargetFramePageBox(targetFrame, tr);
                    targetZoomPoint = targetPt;

                    // 5. Tìm xem trên trang đích có block đối ứng cụ thể không (ví dụ: Cuộn coil r1, hoặc Arrow-From N)
                    BlockReference? partnerBlock = FindPartnerBlockOnFrame(db, tr, targetFrame, wireOrDevName, linkType);

                    Point3d? partnerPt = null;
                    Extents3d partnerExt = default;
                    if (partnerBlock != null && SheetFrameHelper.TryGetExtents(partnerBlock, out partnerExt))
                    {
                        partnerPt = new Point3d(
                            (partnerExt.MinPoint.X + partnerExt.MaxPoint.X) / 2.0,
                            (partnerExt.MinPoint.Y + partnerExt.MaxPoint.Y) / 2.0,
                            0);
                    }

                    // 6. Tạo Layer chuyên dụng _TRACE_GUIDE (Không in ấn)
                    EnsureTraceLayer(db, tr);

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    // a. Vẽ hộp bao quanh block nguồn (Màu Cyan - 4)
                    var srcBoxPoly = CreateBox(srcExtents, 3.0, 4);
                    srcBoxPoly.Layer = TraceLayerName;
                    ms.AppendEntity(srcBoxPoly);
                    tr.AddNewlyCreatedDBObject(srcBoxPoly, true);

                    // b. Vẽ hộp bao quanh ô số trang của khung đích (Màu Đỏ / Magenta - 1 hoặc 6)
                    var targetBoxPoly = CreateBox(targetBoxExt, 2.0, 1);
                    targetBoxPoly.Layer = TraceLayerName;
                    ms.AppendEntity(targetBoxPoly);
                    tr.AddNewlyCreatedDBObject(targetBoxPoly, true);

                    // c. Vẽ đường Line chỉ dẫn từ block nguồn đến khung tên đích (Màu Vàng - 2)
                    var guideLine = new Line(srcCenter, targetPt)
                    {
                        ColorIndex = 2,
                        Layer = TraceLayerName
                    };
                    ms.AppendEntity(guideLine);
                    tr.AddNewlyCreatedDBObject(guideLine, true);

                    // d. Nếu tìm thấy block đối ứng trên trang đích, vẽ thêm chỉ dẫn trực tiếp
                    if (partnerBlock != null && partnerPt.HasValue)
                    {
                        var partnerBoxPoly = CreateBox(partnerExt, 3.0, 4);
                        partnerBoxPoly.Layer = TraceLayerName;
                        ms.AppendEntity(partnerBoxPoly);
                        tr.AddNewlyCreatedDBObject(partnerBoxPoly, true);

                        var subLine = new Line(targetPt, partnerPt.Value)
                        {
                            ColorIndex = 3, // Màu Xanh lá - 3
                            Layer = TraceLayerName
                        };
                        ms.AppendEntity(subLine);
                        tr.AddNewlyCreatedDBObject(subLine, true);

                        ed.WriteMessage($"\n   -> Tim thay doi tuong doi ung '{partnerBlock.GetEffectiveBlockName(tr)}' tren trang {targetSheetNo}!");
                    }

                    ed.WriteMessage($"\n[TIMTRANG] HOAN TAT! Da ve duong chi dan tu block ve Khung ten Trang '{targetSheetNo}'!");
                });

                // Tùy chọn Zoom đến trang đích nếu người dùng muốn
                if (targetZoomPoint.HasValue)
                {
                    var pko = new PromptKeywordOptions("\n[Z: Zoom den trang dich / ENTER: Chon tiep block khac / ESC: Thoat]: ");
                    pko.Keywords.Add("Z");
                    pko.Keywords.Default = "Z";
                    pko.AllowNone = true;

                    var pkr = ed.GetKeywords(pko);
                    if (pkr.Status == PromptStatus.OK && string.Equals(pkr.StringResult, "Z", StringComparison.OrdinalIgnoreCase))
                    {
                        ZoomToPoint(ed, targetZoomPoint.Value, 300.0);
                    }
                }
            }

            ed.WriteMessage("\nKet thuc lenh TIMTRANG.\n");
        }

        /// <summary>
        /// XOALINK / CLRLINK: Xóa toàn bộ các đường chỉ dẫn truy vết đã vẽ trên layer _TRACE_GUIDE
        /// </summary>
        [CommandMethod("XOALINK")]
        [CommandMethod("CLRLINK")]
        [CommandMethod("XTRANG")]
        public void ClearAllTraceGuidesCommand()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            int count = 0;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var allEnts = SelectionHelper.GetAllEntitiesOfType<Entity>(db, tr);
                foreach (var id in allEnts)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        if (string.Equals(ent.Layer, TraceLayerName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!ent.IsWriteEnabled)
                            {
                                ent.UpgradeOpen();
                            }
                            ent.Erase(true);
                            count++;
                        }
                    }
                }
            });

            ed.WriteMessage($"\n[XOALINK] Da xoa {count} doi tuong duong chi dan (layer '{TraceLayerName}').\n");
        }

        #region Helper Methods

        /// <summary>
        /// Trích xuất địa chỉ đích, số trang, tên tín hiệu/thiết bị từ block
        /// </summary>
        private static (string Address, string SheetNo, string Name, string Type) ExtractLinkInfo(BlockReference blkRef, Transaction tr)
        {
            string blkName = blkRef.GetEffectiveBlockName(tr).ToUpper();
            var attrs = blkRef.GetAttributes(tr);

            string address = "";
            string wireOrDevName = "";
            string type = "Block";

            // 1. Kiểm tra Mũi tên tín hiệu Arrow To/From
            string[] arrowAddrTags = { "ADDRESS_TO", "ADDRESS_FROM", "TO_PAGE", "FROM_PAGE", "PAGE_REF", "REF", "DEST_PAGE" };
            foreach (var tag in arrowAddrTags)
            {
                if (attrs.TryGetValue(tag, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    address = val.Trim();
                    type = "Mui ten tin hieu (Arrow)";
                    break;
                }
            }

            // Tên dây/tín hiệu của mũi tên
            string[] wireTags = { "WIRE_NAME", "SIG_NAME", "SIGNAL", "TAG", "WIRE_NO" };
            foreach (var tag in wireTags)
            {
                if (attrs.TryGetValue(tag, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    wireOrDevName = val.Trim();
                    break;
                }
            }

            // 2. Kiểm tra Tiếp điểm Rơ-le (ADDRESS_COIL)
            if (string.IsNullOrWhiteSpace(address))
            {
                foreach (var tag in ElectricalConfig.TagAddressContactAliases)
                {
                    if (attrs.TryGetValue(tag, out var val) && !string.IsNullOrWhiteSpace(val) && val != "---" && val != "-")
                    {
                        address = val.Trim();
                        type = "Tiep diem (Contact)";
                        break;
                    }
                }
            }

            // Tên thiết bị (r1, KM1,...)
            if (string.IsNullOrWhiteSpace(wireOrDevName))
            {
                string[] devTags = { ElectricalConfig.TagDeviceName, "NAME", "TAG", "DEVICE" };
                foreach (var tag in devTags)
                {
                    if (attrs.TryGetValue(tag, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        wireOrDevName = val.Trim();
                        break;
                    }
                }
            }

            // 3. Kiểm tra nếu là Cuộn hút Coil hoặc KHUNG 14 CHAN
            if (string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(wireOrDevName))
            {
                if (blkName.Contains("COIL") || blkName.Contains("RELAY") || blkName.Contains("CONTACTOR"))
                {
                    type = "Cuon hut (Coil)";
                }
                else if (string.Equals(blkName, ElectricalConfig.BlockRelayFrame, StringComparison.OrdinalIgnoreCase))
                {
                    type = "KHUNG 14 CHAN";
                    // Lấy địa chỉ đầu tiên có trong khung
                    foreach (var addrTag in ElectricalConfig.TagRelayFrameAddresses)
                    {
                        if (attrs.TryGetValue(addrTag, out var val) && !string.IsNullOrWhiteSpace(val) && val != "-")
                        {
                            address = val.Split(',')[0].Trim();
                            break;
                        }
                    }
                }
            }

            // Phân tách số trang từ địa chỉ (ví dụ: "02-A" -> "02", "01-5B" -> "01", "04/21" -> "04")
            string sheetNo = ParseSheetNo(address);

            return (address, sheetNo, wireOrDevName, type);
        }

        /// <summary>
        /// Phân tách số trang từ chuỗi địa chỉ
        /// </summary>
        public static string ParseSheetNo(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return "";
            address = address.Trim();

            if (address.Contains("/"))
            {
                return address.Split('/')[0].Trim();
            }

            if (address.Contains("-"))
            {
                return address.Split('-')[0].Trim();
            }

            return address;
        }

        /// <summary>
        /// So khớp số trang linh hoạt (hỗ trợ so sánh "02" == "2")
        /// </summary>
        public static bool MatchesSheetNo(string sheetNo1, string sheetNo2)
        {
            if (string.Equals(sheetNo1, sheetNo2, StringComparison.OrdinalIgnoreCase)) return true;

            if (int.TryParse(sheetNo1, out int n1) && int.TryParse(sheetNo2, out int n2))
            {
                return n1 == n2;
            }

            return false;
        }

        /// <summary>
        /// Xác định tọa độ và vùng bao của ô số trang trên khung tên đích
        /// </summary>
        private static (Point3d Center, Extents3d Extents) LocateTargetFramePageBox(SheetFrame frame, Transaction tr)
        {
            string[] sheetTags = { ElectricalConfig.TagSheetNumber, "A00", "TSHEET", "TRANG", "SHEET_NO", "PAGE", "SHEET", "DWG_NO" };

            if (frame.BlockId != ObjectId.Null)
            {
                if (tr.GetObject(frame.BlockId, OpenMode.ForRead) is BlockReference frameRef)
                {
                    foreach (ObjectId attId in frameRef.AttributeCollection)
                    {
                        if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                        {
                            if (sheetTags.Any(t => string.Equals(attRef.Tag, t, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (SheetFrameHelper.TryGetExtents(attRef, out Extents3d attExt))
                                {
                                    var center = new Point3d(
                                        (attExt.MinPoint.X + attExt.MaxPoint.X) / 2.0,
                                        (attExt.MinPoint.Y + attExt.MaxPoint.Y) / 2.0,
                                        0);
                                    return (center, attExt);
                                }

                                var pos = attRef.AlignmentPoint != Point3d.Origin ? attRef.AlignmentPoint : attRef.Position;
                                var box = new Extents3d(new Point3d(pos.X - 10, pos.Y - 5, 0), new Point3d(pos.X + 10, pos.Y + 5, 0));
                                return (pos, box);
                            }
                        }
                    }
                }
            }

            // Fallback: Vị trí góc dưới phải của khung tên
            double fallbackX = frame.MaxPoint.X - frame.Width * 0.08;
            double fallbackY = frame.MinPoint.Y + frame.Height * 0.04;
            var fbCenter = new Point3d(fallbackX, fallbackY, 0);
            var fbBox = new Extents3d(new Point3d(fallbackX - 15, fallbackY - 8, 0), new Point3d(fallbackX + 15, fallbackY + 8, 0));
            return (fbCenter, fbBox);
        }

        /// <summary>
        /// Tìm block đối ứng cụ thể nằm bên trong khung tên đích (ví dụ Cuộn coil r1, hoặc Arrow From tương ứng)
        /// </summary>
        private static BlockReference? FindPartnerBlockOnFrame(
            Database db,
            Transaction tr,
            SheetFrame targetFrame,
            string nameOrSignal,
            string linkType)
        {
            if (string.IsNullOrWhiteSpace(nameOrSignal)) return null;

            var allBlocks = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

            foreach (var id in allBlocks)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    if (!targetFrame.ContainsPoint(blkRef.Position)) continue;

                    string blkName = blkRef.GetEffectiveBlockName(tr).ToUpper();
                    var attrs = blkRef.GetAttributes(tr);

                    // Nếu nguồn là Tiếp điểm -> Tìm cuộn hút Coil hoặc KHUNG 14 CHAN trên trang đích
                    if (linkType.Contains("Contact") || linkType.Contains("Tiep diem"))
                    {
                        string devName = blkRef.GetAttributeValue(tr, ElectricalConfig.TagDeviceName, "NAME", "TAG") ?? "";
                        if (string.Equals(devName, nameOrSignal, StringComparison.OrdinalIgnoreCase))
                        {
                            bool isContact = attrs.Keys.Any(k => ElectricalConfig.TagAddressContactAliases.Contains(k, StringComparer.OrdinalIgnoreCase));
                            if (!isContact || blkName.Contains("COIL"))
                            {
                                return blkRef;
                            }
                        }
                    }
                    // Nếu nguồn là Mũi tên Arrow -> Tìm mũi tên đối ứng mang cùng Signal / WireName
                    else if (linkType.Contains("Arrow") || linkType.Contains("Mui ten"))
                    {
                        string sig = blkRef.GetAttributeValue(tr, "WIRE_NAME", "SIG_NAME", "SIGNAL", "TAG", "NAME") ?? "";
                        if (string.Equals(sig, nameOrSignal, StringComparison.OrdinalIgnoreCase))
                        {
                            return blkRef;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Đảm bảo layer _TRACE_GUIDE tồn tại trong Database với thuộc tính không in ấn (Plottable = false)
        /// </summary>
        private static ObjectId EnsureTraceLayer(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(TraceLayerName))
            {
                return lt[TraceLayerName];
            }

            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = TraceLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2), // Màu vàng
                IsPlottable = false, // Không in ấn ra giấy
                Description = "Duong chi dan truy vet lien ket trang - CadElectricalToolkit"
            };

            var id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }

        /// <summary>
        /// Tạo hình chữ nhật bao quanh đối tượng dạng Polyline khép kín
        /// </summary>
        private static Polyline CreateBox(Extents3d ext, double padding, short colorIndex)
        {
            var pline = new Polyline(4);
            double minX = ext.MinPoint.X - padding;
            double minY = ext.MinPoint.Y - padding;
            double maxX = ext.MaxPoint.X + padding;
            double maxY = ext.MaxPoint.Y + padding;

            pline.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
            pline.Closed = true;
            pline.ColorIndex = colorIndex;
            return pline;
        }

        /// <summary>
        /// Điều chỉnh khung nhìn AutoCAD Viewport tập trung vào tọa độ chỉ định
        /// </summary>
        private static void ZoomToPoint(Editor ed, Point3d targetPoint, double viewHeight = 300.0)
        {
            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    view.CenterPoint = new Point2d(targetPoint.X, targetPoint.Y);
                    view.Height = viewHeight;
                    ed.SetCurrentView(view);
                }
            }
            catch { }
        }

        #endregion
    }
}
