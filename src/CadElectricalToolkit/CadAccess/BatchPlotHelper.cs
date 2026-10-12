using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.EditorInput;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Thông tin của một trang bản vẽ được đưa vào danh sách in hàng loạt
    /// </summary>
    public class PlotSheetItem
    {
        public bool IsSelected { get; set; } = true;
        public int OrderIndex { get; set; }
        public string SheetNo { get; set; } = "";
        public string DrawingTitle { get; set; } = "";
        public ObjectId BlockId { get; set; }
        public Extents3d Extents { get; set; }

        public Point3d MinPoint => Extents.MinPoint;
        public Point3d MaxPoint => Extents.MaxPoint;
        public double Width => Math.Abs(MaxPoint.X - MinPoint.X);
        public double Height => Math.Abs(MaxPoint.Y - MinPoint.Y);
        public bool IsLandscape => Width >= Height;

        /// <summary>
        /// Ước lượng khổ giấy (A4, A3, hoặc Tùy chỉnh) dựa trên kích thước khung
        /// </summary>
        public string PaperSizeEstimate
        {
            get
            {
                double maxDim = Math.Max(Width, Height);
                if (maxDim >= 270 && maxDim <= 330) return "A4";
                if (maxDim >= 390 && maxDim <= 450) return "A3";
                if (maxDim >= 560 && maxDim <= 630) return "A2";
                if (maxDim >= 800 && maxDim <= 880) return "A1";
                return $"{Math.Round(Width)}x{Math.Round(Height)}";
            }
        }
    }

    /// <summary>
    /// Thông tin vị trí của một mũi tên liên trang để tạo Hyperlink trên PDF
    /// </summary>
    public class WireArrowPdfLink
    {
        public string WireName { get; set; } = "";
        public string BaseSignal { get; set; } = "";
        public bool IsToArrow { get; set; }
        public int SourcePageIndex { get; set; } // 1-based page index in PDF
        public Point3d CadPosition { get; set; }
        public Extents3d SheetExtents { get; set; }
        public int TargetPageIndex { get; set; } // 1-based target page in PDF
        public Point3d TargetCadPosition { get; set; }
        public Extents3d TargetSheetExtents { get; set; }
    }

    /// <summary>
    /// Tùy chọn cấu hình in hàng loạt
    /// </summary>
    public class BatchPlotOptions
    {
        public string DeviceName { get; set; } = "AutoCAD PDF (General Documentation).pc3";
        public string MediaName { get; set; } = "ISO_full_bleed_A4_(297.00_x_210.00_MM)";
        public bool AutoMatchMediaSize { get; set; } = true;
        public string PlotStyleSheet { get; set; } = "monochrome.ctb";
        public bool IsMonochrome { get; set; } = true;
        public bool MergeIntoSinglePdf { get; set; } = true;
        public bool EnableInteractiveHyperlinks { get; set; } = true;
        public bool MatchBaseSignalOnly { get; set; } = true;
        public string OutputFilePath { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public bool OpenAfterPlot { get; set; } = true;
    }

    /// <summary>
    /// Báo cáo tiến trình in
    /// </summary>
    public class PlotProgressReport
    {
        public int CurrentSheetIndex { get; set; }
        public int TotalSheets { get; set; }
        public string StatusMessage { get; set; } = "";
        public double Percentage => TotalSheets > 0 ? (double)CurrentSheetIndex / TotalSheets * 100 : 0;
    }

    /// <summary>
    /// Kết quả thực thi in hàng loạt
    /// </summary>
    public class BatchPlotResult
    {
        public bool Success { get; set; }
        public int TotalPagesPlotted { get; set; }
        public int TotalLinksCreated { get; set; }
        public string OutputFilePath { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    /// <summary>
    /// Lớp xử lý nghiệp vụ in hàng loạt và tạo liên kết PDF thông minh
    /// </summary>
    public static class BatchPlotHelper
    {
        /// <summary>
        /// Quét toàn bộ khung bản vẽ trong ModelSpace để chuẩn bị in
        /// </summary>
        public static List<PlotSheetItem> ScanAllFramesForPlotting(Database db, Transaction tr)
        {
            var sheetFrames = SheetFrameHelper.ScanAllSheetFrames(
                db, tr,
                SheetFrameHelper.DefaultTitleBlockNames,
                SheetFrameHelper.DefaultSheetNoTags);

            var result = new List<PlotSheetItem>();
            int order = 1;

            foreach (var frame in sheetFrames)
            {
                string cleanSheetNo = SanitizeSheetNo(frame.SheetNo, order);
                string title = "";

                if (tr.GetObject(frame.BlockId, OpenMode.ForRead) is BlockReference blkRef)
                {
                    title = ExtractTitleFromBlock(blkRef, tr, cleanSheetNo);
                }

                result.Add(new PlotSheetItem
                {
                    IsSelected = true,
                    OrderIndex = order++,
                    SheetNo = cleanSheetNo,
                    DrawingTitle = title,
                    BlockId = frame.BlockId,
                    Extents = frame.Extents
                });
            }

            return SortSheets(result, true);
        }

        /// <summary>
        /// Quét các khung bản vẽ từ tập hợp ObjectId do người dùng quét chọn
        /// </summary>
        public static List<PlotSheetItem> ScanSelectedFramesForPlotting(Transaction tr, IEnumerable<ObjectId> selectedIds)
        {
            var sheetFrames = SheetFrameHelper.ScanSelectedFrames(
                tr, selectedIds, SheetFrameHelper.DefaultSheetNoTags);

            var result = new List<PlotSheetItem>();
            int order = 1;

            foreach (var frame in sheetFrames)
            {
                string cleanSheetNo = SanitizeSheetNo(frame.SheetNo, order);
                string title = "";

                if (tr.GetObject(frame.BlockId, OpenMode.ForRead) is BlockReference blkRef)
                {
                    title = ExtractTitleFromBlock(blkRef, tr, cleanSheetNo);
                }

                result.Add(new PlotSheetItem
                {
                    IsSelected = true,
                    OrderIndex = order++,
                    SheetNo = cleanSheetNo,
                    DrawingTitle = title,
                    BlockId = frame.BlockId,
                    Extents = frame.Extents
                });
            }

            return SortSheets(result, true);
        }

        private static string ExtractTitleFromBlock(BlockReference blkRef, Transaction tr, string sheetNo)
        {
            string? title = blkRef.GetAttributeValue(tr,
                "TITLE", "TEN_BV", "NAME", "DRAWING_NAME", "TEN_BAN_VE", "DESC",
                "TIEU_DE", "TIÊU ĐỀ", "TÊN BẢN VẼ", "DRAWING_TITLE", "DWG_TITLE",
                "SHEET_TITLE", "NOI_DUNG", "DESCRIPTION", "TEN");

            if (!string.IsNullOrWhiteSpace(title) && title != "---" && title != "?" && !string.Equals(title, "TITLE", StringComparison.OrdinalIgnoreCase))
            {
                return title!.Trim();
            }

            // Quét toàn bộ attribute collection nếu chưa tìm thấy
            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                {
                    string tag = attRef.Tag.ToUpperInvariant();
                    if (tag.Contains("TITLE") || tag.Contains("TEN") || tag.Contains("NAME") || tag.Contains("DESC"))
                    {
                        string val = attRef.TextString?.Trim() ?? "";
                        if (!string.IsNullOrWhiteSpace(val) && val != "---" && val != "?" && !val.Equals("TITLE", StringComparison.OrdinalIgnoreCase))
                        {
                            return val;
                        }
                    }
                }
            }

            return $"Bản vẽ trang {sheetNo}";
        }

        private static string SanitizeSheetNo(string rawSheetNo, int fallbackOrder)
        {
            if (string.IsNullOrWhiteSpace(rawSheetNo) ||
                string.Equals(rawSheetNo, "SHEET", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawSheetNo, "PAGE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawSheetNo, "TSHEET", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawSheetNo, "A00", StringComparison.OrdinalIgnoreCase))
            {
                return fallbackOrder.ToString("D2");
            }
            return rawSheetNo.Trim();
        }

        /// <summary>
        /// Sắp xếp thứ tự bản vẽ (theo số trang hoặc theo tọa độ X-Y)
        /// </summary>
        public static List<PlotSheetItem> SortSheets(List<PlotSheetItem> sheets, bool bySheetNumber)
        {
            if (bySheetNumber)
            {
                var sorted = sheets.OrderBy(s =>
                {
                    var match = Regex.Match(s.SheetNo, @"\d+");
                    return match.Success ? int.Parse(match.Value) : 99999;
                }).ThenBy(s => s.SheetNo).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].OrderIndex = i + 1;
                }
                return sorted;
            }
            else
            {
                // Sắp xếp theo tọa độ: Từ trên xuống dưới (Y giảm dần), rồi từ trái sang phải (X tăng dần)
                var sorted = sheets.OrderByDescending(s => Math.Round(s.MinPoint.Y / 100.0))
                                   .ThenBy(s => s.MinPoint.X).ToList();

                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].OrderIndex = i + 1;
                }
                return sorted;
            }
        }

        /// <summary>
        /// Lấy danh sách máy in / plotter khả dụng trong AutoCAD
        /// </summary>
        public static List<string> GetAvailablePlotDevices()
        {
            var devices = new List<string>();
            try
            {
                var psv = PlotSettingsValidator.Current;
                StringCollection devList = psv.GetPlotDeviceList();
                foreach (string dev in devList)
                {
                    devices.Add(dev);
                }
            }
            catch
            {
                // Fallback nếu chạy ngoài session CAD
                devices.Add("AutoCAD PDF (General Documentation).pc3");
                devices.Add("DWG To PDF.pc3");
                devices.Add("AutoCAD PDF (High Quality Print).pc3");
                devices.Add("AutoCAD PDF (Smallest File).pc3");
            }

            // Sắp xếp ưu tiên các PDF driver lên đầu
            var priority = new[] {
                "AutoCAD PDF (General Documentation).pc3",
                "DWG To PDF.pc3",
                "AutoCAD PDF (High Quality Print).pc3",
                "AutoCAD PDF (Smallest File).pc3"
            };

            var sorted = new List<string>();
            foreach (var p in priority)
            {
                if (devices.Contains(p))
                {
                    sorted.Add(p);
                    devices.Remove(p);
                }
            }
            sorted.AddRange(devices);
            return sorted;
        }

        /// <summary>
        /// Lấy danh sách khổ giấy cho một máy in cụ thể
        /// </summary>
        public static List<string> GetMediaNamesForDevice(string deviceName)
        {
            var mediaList = new List<string>();
            try
            {
                using (PlotSettings ps = new PlotSettings(true))
                {
                    var psv = PlotSettingsValidator.Current;
                    psv.SetPlotConfigurationName(ps, deviceName, null);
                    psv.RefreshLists(ps);
                    StringCollection names = psv.GetCanonicalMediaNameList(ps);
                    foreach (string name in names)
                    {
                        mediaList.Add(name);
                    }
                }
            }
            catch
            {
                // Fallback mặc định
                mediaList.Add("ISO_full_bleed_A4_(297.00_x_210.00_MM)");
                mediaList.Add("ISO_full_bleed_A4_(210.00_x_297.00_MM)");
                mediaList.Add("ISO_full_bleed_A3_(420.00_x_297.00_MM)");
                mediaList.Add("ISO_full_bleed_A3_(297.00_x_420.00_MM)");
                mediaList.Add("ISO_A4_(297.00_x_210.00_MM)");
                mediaList.Add("ISO_A3_(420.00_x_297.00_MM)");
            }

            return mediaList;
        }

        /// <summary>
        /// Lấy danh sách bảng nét in (CTB) khả dụng
        /// </summary>
        public static List<string> GetAvailableStyleSheets()
        {
            var styles = new List<string>();
            try
            {
                var psv = PlotSettingsValidator.Current;
                StringCollection list = psv.GetPlotStyleSheetList();
                foreach (string s in list)
                {
                    styles.Add(s);
                }
            }
            catch
            {
                styles.Add("monochrome.ctb");
                styles.Add("acad.ctb");
                styles.Add("grayscale.ctb");
            }

            var priority = new[] { "monochrome.ctb", "acad.ctb", "grayscale.ctb" };
            var sorted = new List<string>();
            foreach (var p in priority)
            {
                if (styles.Contains(p))
                {
                    sorted.Add(p);
                    styles.Remove(p);
                }
            }
            sorted.AddRange(styles);
            return sorted;
        }

        /// <summary>
        /// Tìm tên Media chuẩn phù hợp cho khung (A4 hay A3, Ngang hay Dọc)
        /// </summary>
        public static string ResolveMediaForSheet(
            List<string> availableMedia,
            PlotSheetItem sheet,
            string fallbackMedia)
        {
            bool isLandscape = sheet.IsLandscape;
            string size = sheet.PaperSizeEstimate;

            // Tìm Media tràn lề (full bleed) trước
            string? match = null;
            if (size == "A4")
            {
                if (isLandscape)
                {
                    match = availableMedia.FirstOrDefault(m => m.IndexOf("full_bleed_A4_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A4_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    match = availableMedia.FirstOrDefault(m => m.IndexOf("full_bleed_A4_(210", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A4_(210", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
            else if (size == "A3")
            {
                if (isLandscape)
                {
                    match = availableMedia.FirstOrDefault(m => m.IndexOf("full_bleed_A3_(420", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A3_(420", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    match = availableMedia.FirstOrDefault(m => m.IndexOf("full_bleed_A3_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A3_(297", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? availableMedia.FirstOrDefault(m => m.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            return match ?? fallbackMedia;
        }

        /// <summary>
        /// Quét tất cả mũi tên trong bản vẽ và lập bản đồ liên kết nhảy trang PDF
        /// </summary>
        public static List<WireArrowPdfLink> BuildPdfArrowLinks(
            Database db,
            Transaction tr,
            List<PlotSheetItem> selectedSheets,
            bool matchBaseSignal)
        {
            var links = new List<WireArrowPdfLink>();
            var blockIds = SelectionHelper.GetAllEntitiesOfType<BlockReference>(db, tr);

            var rawArrows = new List<(string wireName, string baseSignal, bool isTo, Point3d pos, int pageIndex, Extents3d sheetExt)>();

            foreach (var id in blockIds)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blkRef)
                {
                    string blkName = blkRef.GetEffectiveBlockName(tr);
                    bool isTo = blkName.IndexOf("To", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isFrom = blkName.IndexOf("From", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Kiểm tra tag WIRE_NAME
                    string? wireName = blkRef.GetAttributeValue(tr, Core.ElectricalConfig.TagArrowWire, "WIRE_NAME", "WIRE", "NET");
                    if (string.IsNullOrWhiteSpace(wireName)) continue;

                    if (!isTo && !isFrom)
                    {
                        if (blkRef.GetAttributeValue(tr, Core.ElectricalConfig.TagArrowAddressTo, "ADDRESS_TO") != null) isTo = true;
                        else if (blkRef.GetAttributeValue(tr, Core.ElectricalConfig.TagArrowAddressFrom, "ADDRESS_FROM") != null) isFrom = true;
                        else continue;
                    }

                    Point3d pos = blkRef.Position;

                    // Xác định mũi tên này thuộc sheet nào trong danh sách in
                    int pageIndex = -1;
                    Extents3d sheetExt = default;

                    for (int i = 0; i < selectedSheets.Count; i++)
                    {
                        var s = selectedSheets[i];
                        if (pos.X >= s.MinPoint.X - 10 && pos.X <= s.MaxPoint.X + 10 &&
                            pos.Y >= s.MinPoint.Y - 10 && pos.Y <= s.MaxPoint.Y + 10)
                        {
                            pageIndex = i + 1; // 1-based page index
                            sheetExt = s.Extents;
                            break;
                        }
                    }

                    if (pageIndex == -1) continue;

                    string baseSignal = wireName!.Trim();
                    if (matchBaseSignal)
                    {
                        int dashIdx = baseSignal.IndexOf('-');
                        if (dashIdx > 0)
                        {
                            baseSignal = baseSignal.Substring(0, dashIdx).Trim();
                        }
                    }

                    rawArrows.Add((wireName.Trim(), baseSignal, isTo, pos, pageIndex, sheetExt));
                }
            }

            // Ghép cặp To <-> From theo wireName hoặc baseSignal
            var toArrows = rawArrows.Where(a => a.isTo).ToList();
            var fromArrows = rawArrows.Where(a => !a.isTo).ToList();

            foreach (var to in toArrows)
            {
                var match = fromArrows.FirstOrDefault(f =>
                    string.Equals(matchBaseSignal ? f.baseSignal : f.wireName,
                                  matchBaseSignal ? to.baseSignal : to.wireName,
                                  StringComparison.OrdinalIgnoreCase) &&
                    f.pageIndex != to.pageIndex);

                if (match != default && match.pageIndex > 0)
                {
                    // Link từ To -> From
                    links.Add(new WireArrowPdfLink
                    {
                        WireName = to.wireName,
                        BaseSignal = to.baseSignal,
                        IsToArrow = true,
                        SourcePageIndex = to.pageIndex,
                        CadPosition = to.pos,
                        SheetExtents = to.sheetExt,
                        TargetPageIndex = match.pageIndex,
                        TargetCadPosition = match.pos,
                        TargetSheetExtents = match.sheetExt
                    });

                    // Link từ From -> To (chiều ngược lại)
                    links.Add(new WireArrowPdfLink
                    {
                        WireName = match.wireName,
                        BaseSignal = match.baseSignal,
                        IsToArrow = false,
                        SourcePageIndex = match.pageIndex,
                        CadPosition = match.pos,
                        SheetExtents = match.sheetExt,
                        TargetPageIndex = to.pageIndex,
                        TargetCadPosition = to.pos,
                        TargetSheetExtents = to.sheetExt
                    });
                }
            }

            return links;
        }

        /// <summary>
        /// Thực thi in hàng loạt các bản vẽ ra file PDF
        /// </summary>
        public static BatchPlotResult ExecuteBatchPlot(
            Document doc,
            List<PlotSheetItem> sheetsToPlot,
            BatchPlotOptions options,
            IProgress<PlotProgressReport>? progress = null)
        {
            var result = new BatchPlotResult();
            if (sheetsToPlot.Count == 0)
            {
                result.ErrorMessage = "Không có bản vẽ nào được chọn để in.";
                return result;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "CadElectrical_Plot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var tempPdfFiles = new List<string>();
            List<WireArrowPdfLink> arrowLinks = new List<WireArrowPdfLink>();

            // Lưu biến hệ thống BACKGROUNDPLOT
            short oldBgPlot = 0;
            try
            {
                oldBgPlot = (short)Application.GetSystemVariable("BACKGROUNDPLOT");
                Application.SetSystemVariable("BACKGROUNDPLOT", 0);
            }
            catch { }

            try
            {
                // 1. Quét mũi tên liên trang nếu người dùng kích hoạt tạo hyperlink tương tác
                if (options.EnableInteractiveHyperlinks && options.MergeIntoSinglePdf)
                {
                    using (doc.LockDocument())
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        arrowLinks = BuildPdfArrowLinks(doc.Database, tr, sheetsToPlot, options.MatchBaseSignalOnly);
                        tr.Commit();
                    }
                }

                // 2. Lấy danh sách khổ giấy của thiết bị
                var availableMedia = GetMediaNamesForDevice(options.DeviceName);

                // 3. Tiến hành in từng sheet ra file PDF tạm
                int total = sheetsToPlot.Count;
                for (int i = 0; i < total; i++)
                {
                    var sheet = sheetsToPlot[i];
                    string tempPdf = Path.Combine(tempDir, $"page_{i + 1:D4}.pdf");

                    progress?.Report(new PlotProgressReport
                    {
                        CurrentSheetIndex = i + 1,
                        TotalSheets = total,
                        StatusMessage = $"Đang in trang {i + 1}/{total} (Sheet {sheet.SheetNo})..."
                    });

                    string mediaToUse = options.AutoMatchMediaSize
                        ? ResolveMediaForSheet(availableMedia, sheet, options.MediaName)
                        : options.MediaName;

                    bool ok = PlotSingleSheet(doc, sheet, tempPdf, options.DeviceName, mediaToUse, options.PlotStyleSheet);
                    if (!ok || !File.Exists(tempPdf))
                    {
                        result.ErrorMessage = $"Lỗi khi in bản vẽ Sheet {sheet.SheetNo}. Vui lòng kiểm tra lại máy in hoặc khổ giấy.";
                        return result;
                    }

                    tempPdfFiles.Add(tempPdf);
                }

                // 4. Ghép file PDF hoặc sao chép ra thư mục đích
                if (options.MergeIntoSinglePdf)
                {
                    progress?.Report(new PlotProgressReport
                    {
                        CurrentSheetIndex = total,
                        TotalSheets = total,
                        StatusMessage = "Đang ghép các trang và tạo liên kết nhảy trang PDF..."
                    });

                    string finalPdfPath = options.OutputFilePath;
                    if (string.IsNullOrWhiteSpace(finalPdfPath))
                    {
                        string docDir = Path.GetDirectoryName(doc.Name) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        string docBase = Path.GetFileNameWithoutExtension(doc.Name);
                        finalPdfPath = Path.Combine(docDir, $"{docBase}_MultiPage.pdf");
                    }

                    // Đảm bảo thư mục lưu tồn tại
                    string? dir = Path.GetDirectoryName(finalPdfPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    int linksCreated = MergePdfsWithLinks(tempPdfFiles, finalPdfPath, arrowLinks, options.EnableInteractiveHyperlinks);

                    result.Success = true;
                    result.TotalPagesPlotted = total;
                    result.TotalLinksCreated = linksCreated;
                    result.OutputFilePath = finalPdfPath;
                }
                else
                {
                    // Xuất từng file riêng lẻ
                    string outFolder = options.OutputFolder;
                    if (string.IsNullOrWhiteSpace(outFolder))
                    {
                        outFolder = Path.GetDirectoryName(doc.Name) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                    Directory.CreateDirectory(outFolder);

                    for (int i = 0; i < total; i++)
                    {
                        var sheet = sheetsToPlot[i];
                        string sheetTitleClean = string.Join("_", (sheet.DrawingTitle ?? "").Split(Path.GetInvalidFileNameChars()));
                        string fileName = string.IsNullOrWhiteSpace(sheetTitleClean)
                            ? $"Sheet_{sheet.SheetNo}.pdf"
                            : $"Sheet_{sheet.SheetNo}_{sheetTitleClean}.pdf";

                        string destPath = Path.Combine(outFolder, fileName);
                        File.Copy(tempPdfFiles[i], destPath, true);
                    }

                    result.Success = true;
                    result.TotalPagesPlotted = total;
                    result.OutputFilePath = outFolder;
                }

                // 5. Tự động mở file sau khi in nếu được chọn
                if (result.Success && options.OpenAfterPlot && !string.IsNullOrWhiteSpace(result.OutputFilePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(result.OutputFilePath);
                    }
                    catch { }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Lỗi trong quá trình in hàng loạt: " + ex.Message;
                return result;
            }
            finally
            {
                // Khôi phục biến hệ thống BACKGROUNDPLOT
                try
                {
                    Application.SetSystemVariable("BACKGROUNDPLOT", oldBgPlot);
                }
                catch { }

                // Xóa thư mục tạm
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Tính toán vùng in Extents2d theo hệ tọa độ hiển thị DCS (Display Coordinate System)
        /// Bắt buộc cho ModelSpace để không bị in ra trang trắng khi góc nhìn bị pan/zoom.
        /// </summary>
        public static Extents2d CalculatePlotWindowDcs(Editor ed, PlotSheetItem sheet)
        {
            double minX = Math.Min(sheet.MinPoint.X, sheet.MaxPoint.X);
            double minY = Math.Min(sheet.MinPoint.Y, sheet.MaxPoint.Y);
            double maxX = Math.Max(sheet.MinPoint.X, sheet.MaxPoint.X);
            double maxY = Math.Max(sheet.MinPoint.Y, sheet.MaxPoint.Y);

            if (maxX <= minX + 1.0) maxX = minX + 297.0;
            if (maxY <= minY + 1.0) maxY = minY + 210.0;

            Point3d ptMin = new Point3d(minX, minY, 0);
            Point3d ptMax = new Point3d(maxX, maxY, 0);

            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    Matrix3d mat = Matrix3d.PlaneToWorld(view.ViewDirection);
                    mat = Matrix3d.Displacement(view.Target - Point3d.Origin) * mat;
                    mat = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * mat;
                    Matrix3d wcs2dcs = mat.Inverse();

                    Point3d dcsPt1 = ptMin.TransformBy(wcs2dcs);
                    Point3d dcsPt2 = ptMax.TransformBy(wcs2dcs);

                    double dMinX = Math.Min(dcsPt1.X, dcsPt2.X);
                    double dMinY = Math.Min(dcsPt1.Y, dcsPt2.Y);
                    double dMaxX = Math.Max(dcsPt1.X, dcsPt2.X);
                    double dMaxY = Math.Max(dcsPt1.Y, dcsPt2.Y);

                    return new Extents2d(new Autodesk.AutoCAD.Geometry.Point2d(dMinX, dMinY), new Autodesk.AutoCAD.Geometry.Point2d(dMaxX, dMaxY));
                }
            }
            catch
            {
                return new Extents2d(new Autodesk.AutoCAD.Geometry.Point2d(minX, minY), new Autodesk.AutoCAD.Geometry.Point2d(maxX, maxY));
            }
        }

        /// <summary>
        /// In một khung bản vẽ đơn lẻ ra một file PDF tạm
        /// </summary>
        private static bool PlotSingleSheet(
            Document doc,
            PlotSheetItem sheet,
            string outputPdfPath,
            string deviceName,
            string mediaName,
            string plotStyleSheet)
        {
            Database db = doc.Database;

            // Đợi nếu AutoCAD đang bận xử lý tác vụ in trước
            int waitTicks = 0;
            while (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting && waitTicks++ < 40)
            {
                System.Threading.Thread.Sleep(50);
            }

            if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
            {
                return false;
            }

            PlotInfo? plotInfo = null;
            PlotSettings? plotSettings = null;

            // Căn góc nhìn AutoCAD tới khung bản vẽ để tải đầy đủ đồ họa vào viewport
            try
            {
                double vMinX = Math.Min(sheet.MinPoint.X, sheet.MaxPoint.X);
                double vMinY = Math.Min(sheet.MinPoint.Y, sheet.MaxPoint.Y);
                double vMaxX = Math.Max(sheet.MinPoint.X, sheet.MaxPoint.X);
                double vMaxY = Math.Max(sheet.MinPoint.Y, sheet.MaxPoint.Y);

                using (ViewTableRecord view = new ViewTableRecord())
                {
                    view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d((vMinX + vMaxX) / 2.0, (vMinY + vMaxY) / 2.0);
                    view.Height = Math.Max(vMaxY - vMinY, 10.0);
                    view.Width = Math.Max(vMaxX - vMinX, 10.0);
                    doc.Editor.SetCurrentView(view);
                }
            }
            catch { }

            // GIAI ĐOẠN 1: Cấu hình PlotSettings & PlotInfo trong một Transaction ngắn, commit ngay để nhả lock DB
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                Layout layout = (Layout)tr.GetObject(btr.LayoutId, OpenMode.ForRead);

                plotSettings = new PlotSettings(layout.ModelType);
                plotSettings.CopyFrom(layout);
                var psv = PlotSettingsValidator.Current;

                // 1. Cấu hình thiết bị in trước
                psv.SetPlotConfigurationName(plotSettings, deviceName, null);
                psv.RefreshLists(plotSettings);

                // 2. Xác thực và đặt tên khổ giấy khả dụng
                var canonicals = psv.GetCanonicalMediaNameList(plotSettings);
                string actualMedia = mediaName;
                if (!canonicals.Cast<string>().Contains(mediaName, StringComparer.OrdinalIgnoreCase))
                {
                    actualMedia = canonicals.Cast<string>().FirstOrDefault(m => m.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0)
                               ?? (canonicals.Count > 0 ? canonicals[0] : "");
                }

                if (!string.IsNullOrEmpty(actualMedia))
                {
                    try
                    {
                        psv.SetCanonicalMediaName(plotSettings, actualMedia);
                    }
                    catch { }
                }

                // 3. Hướng in: Ngang (0 độ) hoặc Dọc (90 độ)
                PlotRotation rot = sheet.IsLandscape ? PlotRotation.Degrees000 : PlotRotation.Degrees090;
                psv.SetPlotRotation(plotSettings, rot);

                // 4. Đặt bảng nét in (CTB)
                if (!string.IsNullOrWhiteSpace(plotStyleSheet))
                {
                    try
                    {
                        var styles = psv.GetPlotStyleSheetList();
                        if (styles.Cast<string>().Any(s => string.Equals(s, plotStyleSheet, StringComparison.OrdinalIgnoreCase)))
                        {
                            psv.SetCurrentStyleSheet(plotSettings, plotStyleSheet);
                        }
                    }
                    catch { }
                }

                // 5. Đặt vùng in là Window với tọa độ DCS (Display Coordinate System) để không bị lệch/trắng trang
                Extents2d winExt = CalculatePlotWindowDcs(doc.Editor, sheet);
                psv.SetPlotWindowArea(plotSettings, winExt);
                psv.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);

                // 6. Căn giữa và co giãn vừa khổ giấy (Bắt buộc gọi sau khi đã đặt PlotType.Window)
                psv.SetPlotCentered(plotSettings, true);
                psv.SetUseStandardScale(plotSettings, true);
                psv.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit);

                plotInfo = new PlotInfo();
                plotInfo.Layout = btr.LayoutId;
                plotInfo.OverrideSettings = plotSettings;

                var piv = new PlotInfoValidator();
                piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled;
                piv.Validate(plotInfo);

                tr.Commit();
            }

            // GIAI ĐOẠN 2: Thực thi PlotEngine HOÀN TOÀN NGOÀI TRANSACTION để tránh deadlock đồ họa AutoCAD
            try
            {
                using (doc.LockDocument())
                using (PlotEngine pe = PlotFactory.CreatePublishEngine())
                {
                    string docName = string.IsNullOrWhiteSpace(doc.Name) ? "Drawing" : doc.Name;
                    pe.BeginPlot(null, null);
                    pe.BeginDocument(plotInfo, docName, null, 1, true, outputPdfPath);

                    using (PlotPageInfo ppi = new PlotPageInfo())
                    {
                        pe.BeginPage(ppi, plotInfo, true, null);
                    }

                    pe.BeginGenerateGraphics(null);
                    pe.EndGenerateGraphics(null);

                    pe.EndPage(null);
                    pe.EndDocument(null);
                    pe.EndPlot(null);
                }
            }
            finally
            {
                plotSettings?.Dispose();
                plotInfo?.Dispose();
            }

            // Đợi file temp được đóng và sẵn sàng (thử tối đa 2 giây)
            int retries = 20;
            while (retries-- > 0)
            {
                if (File.Exists(outputPdfPath))
                {
                    try
                    {
                        using (var fs = File.Open(outputPdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (fs.Length > 0) return true;
                        }
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            return File.Exists(outputPdfPath);
        }

        /// <summary>
        /// Ghép các file PDF tạm thành một file hoàn chỉnh và inject Hyperlink cho mũi tên
        /// </summary>
        private static int MergePdfsWithLinks(
            List<string> tempPdfs,
            string finalPdfPath,
            List<WireArrowPdfLink> links,
            bool enableLinks)
        {
            int linksCreatedCount = 0;

            using (PdfDocument outputDoc = new PdfDocument())
            {
                // 1. Nhập từng trang từ các file temp
                foreach (string pdfFile in tempPdfs)
                {
                    using (PdfDocument inputDoc = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Import))
                    {
                        for (int pageIdx = 0; pageIdx < inputDoc.PageCount; pageIdx++)
                        {
                            outputDoc.AddPage(inputDoc.Pages[pageIdx]);
                        }
                    }
                }

                // 2. Tạo liên kết nhảy trang thông minh nếu tùy chọn được bật
                if (enableLinks && links.Count > 0)
                {
                    foreach (var link in links)
                    {
                        int srcPageNum = link.SourcePageIndex;
                        int destPageNum = link.TargetPageIndex;

                        if (srcPageNum < 1 || srcPageNum > outputDoc.PageCount) continue;
                        if (destPageNum < 1 || destPageNum > outputDoc.PageCount) continue;

                        PdfPage srcPage = outputDoc.Pages[srcPageNum - 1];

                        double pageWidth = srcPage.Width.Point;
                        double pageHeight = srcPage.Height.Point;

                        // Tọa độ CAD tương đối của mũi tên trong khung
                        double frameMinX = link.SheetExtents.MinPoint.X;
                        double frameMinY = link.SheetExtents.MinPoint.Y;
                        double frameW = Math.Abs(link.SheetExtents.MaxPoint.X - frameMinX);
                        double frameH = Math.Abs(link.SheetExtents.MaxPoint.Y - frameMinY);

                        if (frameW <= 0 || frameH <= 0) continue;

                        double u = (link.CadPosition.X - frameMinX) / frameW;
                        double v = (link.CadPosition.Y - frameMinY) / frameH;

                        // Giới hạn trong khoảng [0, 1]
                        u = Math.Max(0.0, Math.Min(1.0, u));
                        v = Math.Max(0.0, Math.Min(1.0, v));

                        // Tọa độ PDF: Gốc (0,0) ở góc dưới trái (Bottom-Left)
                        double pdfX = u * pageWidth;
                        double pdfY = v * pageHeight;

                        // Kích thước vùng bấm nhạy quanh mũi tên (rộng ~40pt, cao ~20pt)
                        double linkW = 42.0;
                        double linkH = 22.0;

                        double x1 = Math.Max(0, pdfX - linkW / 2.0);
                        double y1 = Math.Max(0, pdfY - linkH / 2.0);
                        double x2 = Math.Min(pageWidth, pdfX + linkW / 2.0);
                        double y2 = Math.Min(pageHeight, pdfY + linkH / 2.0);

                        var rect = new PdfRectangle(new XPoint(x1, y1), new XPoint(x2, y2));

                        // Tạo Link Annotation trỏ tới trang đích
                        var linkAnnot = srcPage.AddDocumentLink(rect, destPageNum);

                        // Gán tooltip chỉ dẫn thân thiện
                        string actionDesc = link.IsToArrow
                            ? $"Nhảy đến đầu dây '{link.WireName}' ở Trang {destPageNum}"
                            : $"Quay lại nguồn dây '{link.WireName}' ở Trang {destPageNum}";

                        linkAnnot.Elements["/Contents"] = new PdfString(actionDesc);

                        linksCreatedCount++;
                    }
                }

                // 3. Lưu file hoàn tất
                outputDoc.Save(finalPdfPath);
            }

            return linksCreatedCount;
        }
    }
}
