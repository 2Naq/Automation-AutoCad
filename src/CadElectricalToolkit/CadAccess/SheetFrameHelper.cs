using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Thông tin một khung tên bản vẽ (Title Block Frame) đã được quét và tính toán vùng bao
    /// </summary>
    public class SheetFrame
    {
        /// <summary>
        /// Số trang bản vẽ (giá trị của Tag A00 / SHEET_NO)
        /// </summary>
        public string SheetNo { get; set; } = "";

        /// <summary>
        /// ObjectId của block khung tên
        /// </summary>
        public ObjectId BlockId { get; set; }

        /// <summary>
        /// Vùng bao hình chữ nhật của khung tên (Bounding Box)
        /// </summary>
        public Extents3d Extents { get; set; }

        /// <summary>
        /// Tọa độ góc dưới trái
        /// </summary>
        public Point3d MinPoint => Extents.MinPoint;

        /// <summary>
        /// Tọa độ góc trên phải
        /// </summary>
        public Point3d MaxPoint => Extents.MaxPoint;

        /// <summary>
        /// Chiều rộng khung
        /// </summary>
        public double Width => Math.Abs(MaxPoint.X - MinPoint.X);

        /// <summary>
        /// Chiều cao khung
        /// </summary>
        public double Height => Math.Abs(MaxPoint.Y - MinPoint.Y);

        /// <summary>
        /// Xác định cột (A, B, C, ...) của một điểm nằm bên trong khung
        /// Cột được chia đều theo chiều ngang (X) trong vùng lưới vẽ (loại trừ lề trái/phải nếu có)
        /// </summary>
        public string GetColumnLetter(double x, int totalColumns, string[] columnLetters)
        {
            if (Width <= 0 || totalColumns <= 0) return "?";

            double leftOffset = Width * Core.ElectricalConfig.FrameLeftMarginRatio;
            double rightOffset = Width * Core.ElectricalConfig.FrameRightMarginRatio;
            double gridWidth = Width - leftOffset - rightOffset;
            if (gridWidth <= 0) gridWidth = Width;

            double relativeX = x - (MinPoint.X + leftOffset);
            double colWidth = gridWidth / totalColumns;
            int colIndex = (int)(relativeX / colWidth);

            colIndex = Math.Max(0, Math.Min(colIndex, totalColumns - 1));

            return colIndex < columnLetters.Length ? columnLetters[colIndex] : (colIndex + 1).ToString();
        }

        /// <summary>
        /// Xác định hàng (1, 2, 3, ...) của một điểm nằm bên trong khung
        /// Hàng 1 ở trên cùng, hàng tăng dần xuống dưới (Y giảm dần).
        /// Vùng lưới các hàng 1-6 nằm ở phía trên phần khung tên (Bottom Margin / Title Block).
        /// </summary>
        public int GetRowNumber(double y, int totalRows)
        {
            if (Height <= 0 || totalRows <= 0) return 1;

            double topOffset = Height * Core.ElectricalConfig.FrameTopMarginRatio;
            double bottomOffset = Height * Core.ElectricalConfig.FrameBottomMarginRatio;
            double gridHeight = Height - topOffset - bottomOffset;
            if (gridHeight <= 0) gridHeight = Height;

            double gridTopY = MaxPoint.Y - topOffset;
            double relativeY = gridTopY - y;  // Y giảm dần = hàng tăng dần từ 1..totalRows
            double rowHeight = gridHeight / totalRows;

            int rowIndex = (int)(relativeY / rowHeight);

            rowIndex = Math.Max(0, Math.Min(rowIndex, totalRows - 1));

            return rowIndex + 1; // 1-based (1..totalRows)
        }

        /// <summary>
        /// Xác định địa chỉ "Trang-HàngCột" (ví dụ: "01-4A") cho một điểm trong khung
        /// </summary>
        public string GetAddress(Point3d point, int totalRows, int totalColumns, string[] columnLetters)
        {
            int row = GetRowNumber(point.Y, totalRows);
            string col = GetColumnLetter(point.X, totalColumns, columnLetters);
            return $"{SheetNo}-{row}{col}";
        }

        /// <summary>
        /// Kiểm tra xem một điểm có nằm bên trong khung tên này không (có dung sai biên)
        /// </summary>
        public bool ContainsPoint(Point3d point, double tolerance = 15.0)
        {
            return point.X >= (MinPoint.X - tolerance) && point.X <= (MaxPoint.X + tolerance) &&
                   point.Y >= (MinPoint.Y - tolerance) && point.Y <= (MaxPoint.Y + tolerance);
        }
    }

    /// <summary>
    /// Tiện ích quét và quản lý các khung tên bản vẽ (Sheet Frames)
    /// </summary>
    public static class SheetFrameHelper
    {
        public static readonly string[] DefaultTitleBlockNames = new[]
        {
            Core.ElectricalConfig.BlockTitle, "Frame-a4", "Frame-a3", "Frame-a2", "Frame-a1",
            "KHUNG_TEN", "KHUNGTEN", "TITLE_BLOCK", "TITLEBLOCK", "KHUNG BAN VE", "KHUNG A4", "KHUNG A3", "KHUNG A2", "KHUNG A1"
        };

        public static readonly string[] DefaultSheetNoTags = new[]
        {
            Core.ElectricalConfig.TagSheetNumber, "A00", "TSHEET", "SHEET_NO", "SHEET", "PAGE", "SHT", "NO", "DWG_NO", "SO_TRANG", "TRANG"
        };

        /// <summary>
        /// Quét toàn bộ ModelSpace, tìm tất cả block khung tên và trả về danh sách SheetFrame
        /// </summary>
        public static List<SheetFrame> ScanAllSheetFrames(
            Database db,
            Transaction tr,
            string[] titleBlockNames,
            string[] sheetNoTags)
        {
            var frames = new List<SheetFrame>();
            var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

            foreach (var id in blockIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    string blkName = blkRef.GetEffectiveBlockName(tr);
                    bool isTitleBlock = titleBlockNames.Any(n =>
                        string.Equals(blkName, n, StringComparison.OrdinalIgnoreCase));

                    if (!isTitleBlock) continue;

                    // Trích xuất số trang từ các Tag ưu tiên
                    string? sheetNo = blkRef.GetAttributeValue(tr, sheetNoTags);
                    if (string.IsNullOrWhiteSpace(sheetNo)) continue;

                    if (TryGetExtents(blkRef, out Extents3d ext))
                    {
                        frames.Add(new SheetFrame
                        {
                            SheetNo = sheetNo!.Trim(),
                            BlockId = blkRef.ObjectId,
                            Extents = ext
                        });
                    }
                }
            }

            return frames.OrderBy(f => int.TryParse(f.SheetNo, out int n) ? n : 9999).ToList();
        }

        /// <summary>
        /// Quét từ danh sách ObjectId do người dùng quét chọn
        /// </summary>
        public static List<SheetFrame> ScanSelectedFrames(
            Transaction tr,
            IEnumerable<ObjectId> selectedIds,
            string[] sheetNoTags)
        {
            var frames = new List<SheetFrame>();

            foreach (var id in selectedIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    string? sheetNo = blkRef.GetAttributeValue(tr, sheetNoTags);
                    if (string.IsNullOrWhiteSpace(sheetNo)) continue;

                    if (TryGetExtents(blkRef, out Extents3d ext))
                    {
                        frames.Add(new SheetFrame
                        {
                            SheetNo = sheetNo!.Trim(),
                            BlockId = blkRef.ObjectId,
                            Extents = ext
                        });
                    }
                }
            }

            return frames.OrderBy(f => int.TryParse(f.SheetNo, out int n) ? n : 9999).ToList();
        }

        /// <summary>
        /// Tìm khung tên chứa một điểm cụ thể (để xác định trang).
        /// Nếu bản vẽ chỉ có 1 khung tên duy nhất, tự động trả về khung tên đó.
        /// Nếu có nhiều khung tên, ưu tiên khung bao chứa điểm hoặc khung tên gần nhất.
        /// </summary>
        public static SheetFrame? FindFrameContaining(List<SheetFrame> frames, Point3d point)
        {
            if (frames == null || frames.Count == 0) return null;
            if (frames.Count == 1) return frames[0];

            var exact = frames.FirstOrDefault(f => f.ContainsPoint(point));
            if (exact != null) return exact;

            // Fallback: Tìm khung có tâm gần điểm nhất
            return frames.OrderBy(f =>
            {
                double midX = (f.MinPoint.X + f.MaxPoint.X) / 2.0;
                double midY = (f.MinPoint.Y + f.MaxPoint.Y) / 2.0;
                double dx = point.X - midX;
                double dy = point.Y - midY;
                return dx * dx + dy * dy;
            }).FirstOrDefault();
        }

        /// <summary>
        /// Tự động quét toàn bộ khung tên trên bản vẽ (nhận diện Frame-a4, Frame-a3 hoặc các block có tag A00/SHEET_NO)
        /// </summary>
        public static List<SheetFrame> ScanAllFramesAuto(Database db, Transaction tr)
        {
            var titleBlockNames = DefaultTitleBlockNames;
            var sheetNoTags = DefaultSheetNoTags;

            var frames = new List<SheetFrame>();
            var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

            foreach (var id in blockIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    string blkName = blkRef.GetEffectiveBlockName(tr);
                    bool nameMatches = titleBlockNames.Any(n => string.Equals(blkName, n, StringComparison.OrdinalIgnoreCase));
                    string? sheetNo = blkRef.GetAttributeValue(tr, sheetNoTags);

                    if ((nameMatches || !string.IsNullOrWhiteSpace(sheetNo)) && !string.IsNullOrWhiteSpace(sheetNo))
                    {
                        if (TryGetExtents(blkRef, out Extents3d ext))
                        {
                            if (Math.Abs(ext.MaxPoint.X - ext.MinPoint.X) > 20 && Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y) > 20)
                            {
                                frames.Add(new SheetFrame
                                {
                                    SheetNo = sheetNo!.Trim(),
                                    BlockId = blkRef.ObjectId,
                                    Extents = ext
                                });
                            }
                        }
                    }
                }
            }

            return frames.OrderBy(f => int.TryParse(f.SheetNo, out int n) ? n : 9999).ToList();
        }

        /// <summary>
        /// Lấy vùng bao GeometricExtents một cách an toàn cho BlockReference, có fallback sang Bounds nếu lỗi
        /// </summary>
        public static bool TryGetExtents(BlockReference blkRef, out Extents3d ext)
        {
            return TryGetExtents((Entity)blkRef, out ext);
        }

        /// <summary>
        /// Lấy vùng bao GeometricExtents một cách an toàn cho bất kỳ Entity nào, có fallback sang Bounds nếu lỗi
        /// </summary>
        public static bool TryGetExtents(Entity ent, out Extents3d ext)
        {
            ext = default;
            try
            {
                ext = ent.GeometricExtents;
                return true;
            }
            catch
            {
                try
                {
                    if (ent.Bounds.HasValue)
                    {
                        ext = ent.Bounds.Value;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
