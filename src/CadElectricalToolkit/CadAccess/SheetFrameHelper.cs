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
        /// Cột được chia đều theo chiều ngang (X) của khung
        /// </summary>
        public string GetColumnLetter(double x, int totalColumns, string[] columnLetters)
        {
            if (Width <= 0 || totalColumns <= 0) return "?";

            double relativeX = x - MinPoint.X;
            double colWidth = Width / totalColumns;
            int colIndex = (int)(relativeX / colWidth);

            colIndex = Math.Max(0, Math.Min(colIndex, totalColumns - 1));

            return colIndex < columnLetters.Length ? columnLetters[colIndex] : (colIndex + 1).ToString();
        }

        /// <summary>
        /// Xác định hàng (1, 2, 3, ...) của một điểm nằm bên trong khung
        /// Hàng 1 ở trên cùng, hàng tăng dần xuống dưới (Y giảm dần)
        /// </summary>
        public int GetRowNumber(double y, int totalRows)
        {
            if (Height <= 0 || totalRows <= 0) return 1;

            double relativeY = MaxPoint.Y - y;  // Y giảm dần = hàng tăng dần
            double rowHeight = Height / totalRows;
            int rowIndex = (int)(relativeY / rowHeight);

            rowIndex = Math.Max(0, Math.Min(rowIndex, totalRows - 1));

            return rowIndex + 1; // 1-based
        }

        /// <summary>
        /// Xác định địa chỉ "Trang-HàngCột" (ví dụ: "2-6B") cho một điểm trong khung
        /// </summary>
        public string GetAddress(Point3d point, int totalRows, int totalColumns, string[] columnLetters)
        {
            int row = GetRowNumber(point.Y, totalRows);
            string col = GetColumnLetter(point.X, totalColumns, columnLetters);
            return $"{SheetNo}-{row}{col}";
        }

        /// <summary>
        /// Kiểm tra xem một điểm có nằm bên trong khung tên này không
        /// </summary>
        public bool ContainsPoint(Point3d point)
        {
            return point.X >= MinPoint.X && point.X <= MaxPoint.X &&
                   point.Y >= MinPoint.Y && point.Y <= MaxPoint.Y;
        }
    }

    /// <summary>
    /// Tiện ích quét và quản lý các khung tên bản vẽ (Sheet Frames)
    /// </summary>
    public static class SheetFrameHelper
    {
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

                    try
                    {
                        frames.Add(new SheetFrame
                        {
                            SheetNo = sheetNo!.Trim(),
                            BlockId = blkRef.ObjectId,
                            Extents = blkRef.GeometricExtents
                        });
                    }
                    catch
                    {
                        // GeometricExtents co the throw neu block khong co hinh hoc
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

                    try
                    {
                        frames.Add(new SheetFrame
                        {
                            SheetNo = sheetNo!.Trim(),
                            BlockId = blkRef.ObjectId,
                            Extents = blkRef.GeometricExtents
                        });
                    }
                    catch { }
                }
            }

            return frames.OrderBy(f => int.TryParse(f.SheetNo, out int n) ? n : 9999).ToList();
        }

        /// <summary>
        /// Tìm khung tên chứa một điểm cụ thể (để xác định trang)
        /// </summary>
        public static SheetFrame? FindFrameContaining(List<SheetFrame> frames, Point3d point)
        {
            return frames.FirstOrDefault(f => f.ContainsPoint(point));
        }
    }
}
