using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.UI.Views
{
    /// <summary>
    /// Giao diện WPF hiện đại cho tính năng Đánh số thứ tự (DSTT & DSTTKBT)
    /// Hỗ trợ cả STT Khung tên và STT Block Attribute / Text
    /// </summary>
    public partial class AutoNumberingWindow : Window
    {
        public AutoNumberingWindow(bool openBlockAttrTab = true)
        {
            InitializeComponent();

            if (openBlockAttrTab)
            {
                TabItemBlockAttr.IsSelected = true;
            }

            UpdateFramePreview();
            UpdateFramePagePreview();
            UpdateAttrPreview();
        }

        #region Live Preview Logic

        private void FrameRule_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFramePreview();
            UpdateFramePagePreview();
        }

        private void FramePage_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFramePagePreview();
        }

        private void AttrRule_Changed(object sender, RoutedEventArgs e)
        {
            UpdateAttrPreview();
        }

        private void UpdateFramePreview()
        {
            if (LblFramePreview == null) return;

            string prefix = TxtFramePrefix?.Text ?? "";
            string suffix = TxtFrameSuffix?.Text ?? "";
            int from = ParseInt(TxtFrameFrom?.Text, 1);
            int styleIndex = CboFrameNumberStyle?.SelectedIndex ?? 0;
            bool twoDigits = ChkFrameTwoDigits?.IsChecked == true;

            string numStr = FormatNumber(from, styleIndex, twoDigits);
            LblFramePreview.Text = $"{prefix}{numStr}{suffix}";
        }

        private void UpdateFramePagePreview()
        {
            if (LblFramePagePreview == null) return;

            bool isPageMode = ChkFramePageMode?.IsChecked == true;
            if (!isPageMode)
            {
                LblFramePagePreview.Text = "Chế độ ghi tổng số trang đang tắt.";
                return;
            }

            string prefix = TxtFramePrefix?.Text ?? "";
            string suffix = TxtFrameSuffix?.Text ?? "";
            int from = ParseInt(TxtFrameFrom?.Text, 1);
            int styleIndex = CboFrameNumberStyle?.SelectedIndex ?? 0;
            bool twoDigits = ChkFrameTwoDigits?.IsChecked == true;

            string pageVal = $"{prefix}{FormatNumber(from, styleIndex, twoDigits)}{suffix}";

            bool autoTotal = ChkFrameAutoTotal?.IsChecked == true;
            string totalPrefix = TxtFrameTotalPagesPrefix?.Text ?? "/";
            int totalNum = autoTotal ? 24 : ParseInt(TxtFrameTotalPages?.Text, 24);
            string totalVal = $"{totalPrefix}{FormatNumber(totalNum, styleIndex, twoDigits)}";

            string tag1 = !string.IsNullOrWhiteSpace(TxtFrameAttrTag?.Text) ? TxtFrameAttrTag!.Text : "A00";
            string tag2 = !string.IsNullOrWhiteSpace(TxtFrameTotalPagesTag?.Text) ? TxtFrameTotalPagesTag!.Text : "TSHEET";

            LblFramePagePreview.Text = $"VD: {tag1}='{pageVal}', {tag2}='{totalVal}' (Hiển thị: {pageVal} {totalVal})";
        }

        private void UpdateAttrPreview()
        {
            if (LblAttrPreview == null) return;

            string prefix = TxtAttrPrefix?.Text ?? "";
            string suffix = TxtAttrSuffix?.Text ?? "";
            int from = ParseInt(TxtAttrFrom?.Text, 1);
            int styleIndex = CboAttrNumberStyle?.SelectedIndex ?? 0;
            bool twoDigits = ChkAttrTwoDigits?.IsChecked == true;

            string numStr = FormatNumber(from, styleIndex, twoDigits);
            LblAttrPreview.Text = $"{prefix}{numStr}{suffix}";
        }

        #endregion

        #region CAD Picking (Chọn TAG)

        private void BtnChooseTag_Click(object sender, RoutedEventArgs e)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            bool isFrameTab = TabControlMain.SelectedIndex == 0;

            using (ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Block hoặc Attribute trên bản vẽ để lấy Block name và Tag: ");
                peo.SetRejectMessage("\nVui lòng chọn một Block Reference hoặc Text!");
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (ent is BlockReference blk)
                    {
                        string blkName = blk.GetEffectiveBlockName(tr);
                        string pickedTag = "";

                        // Tìm attribute gần nhất với tọa độ click chuột
                        double minDist = double.MaxValue;
                        foreach (ObjectId attId in blk.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                            {
                                double dist = att.Position.DistanceTo(per.PickedPoint);
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    pickedTag = att.Tag;
                                }
                            }
                        }

                        if (isFrameTab)
                        {
                            TxtFrameBlockName.Text = blkName;
                            if (!string.IsNullOrEmpty(pickedTag)) TxtFrameAttrTag.Text = pickedTag;
                            TxtFrameStatus.Text = $"Đã nhận diện: {blkName} (TAG: {pickedTag})";
                        }
                        else
                        {
                            TxtAttrBlockName.Text = blkName;
                            if (!string.IsNullOrEmpty(pickedTag)) TxtAttrTagName.Text = pickedTag;
                            TxtAttrStatus.Text = $"Đã nhận diện: {blkName} (TAG: {pickedTag})";
                        }

                        ed.WriteMessage($"\n[ĐÃ NHẬN DIỆN] Block: '{blkName}' | Attribute TAG: '{pickedTag}'");
                    }
                    else if (ent is DBText or MText)
                    {
                        if (!isFrameTab)
                        {
                            TxtAttrBlockName.Text = "";
                            TxtAttrTagName.Text = "(TEXT/MTEXT)";
                            TxtAttrStatus.Text = "Đã nhận diện đối tượng Text/MText.";
                        }
                        ed.WriteMessage("\n[ĐÃ NHẬN DIỆN] Đối tượng là Text/MText tự do.");
                    }
                });
            }
        }

        #endregion

        #region TAB 2: Đánh số Block Attribute & Text

        private void BtnAttrSelectBlocks_Click(object sender, RoutedEventArgs e)
        {
            var ed = CadDatabaseHelper.ActiveEd;

            string prefix = TxtAttrPrefix.Text ?? "";
            string suffix = TxtAttrSuffix.Text ?? "";
            int startNum = ParseInt(TxtAttrFrom.Text, 1);
            int step = Math.Max(1, ParseInt(TxtAttrStep.Text, 1));
            int styleIndex = CboAttrNumberStyle.SelectedIndex;
            bool twoDigits = ChkAttrTwoDigits.IsChecked == true;
            string targetBlockName = TxtAttrBlockName.Text.Trim();
            string targetTag = TxtAttrTagName.Text.Trim();
            int dirIndex = CboAttrDirection.SelectedIndex;
            bool includeText = ChkAttrIncludeText.IsChecked == true;

            int count = 0;
            int nextNumber = startNum;

            using (ed.StartUserInteraction(this))
            {
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nQuét chọn các Block / Text cần đánh số thứ tự: "
                };

                var selRes = ed.GetSelection(pso);
                if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
                {
                    ed.WriteMessage("\nChưa chọn đối tượng nào.");
                    return;
                }

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var items = new List<(Entity ent, Point3d pos)>();

                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (selObj == null) continue;
                        var entity = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                        if (entity == null) continue;

                        if (entity is BlockReference blk)
                        {
                            if (!string.IsNullOrEmpty(targetBlockName))
                            {
                                string blkName = blk.GetEffectiveBlockName(tr);
                                if (!string.Equals(blkName, targetBlockName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }
                            items.Add((blk, blk.Position));
                        }
                        else if (includeText && (entity is DBText || entity is MText))
                        {
                            Point3d pos = entity is DBText t ? t.Position : ((MText)entity).Location;
                            items.Add((entity, pos));
                        }
                    }

                    if (items.Count == 0)
                    {
                        ed.WriteMessage("\nKhông tìm thấy đối tượng phù hợp theo tiêu chí lọc.");
                        return;
                    }

                    // Sắp xếp đối tượng theo hướng với dung sai (Tolerance)
                    var sorted = SortEntitiesByDirection(items, dirIndex);

                    int currentNum = startNum;
                    foreach (var (ent, _) in sorted)
                    {
                        string formattedVal = $"{prefix}{FormatNumber(currentNum, styleIndex, twoDigits)}{suffix}";

                        if (ent is BlockReference blk)
                        {
                            bool updated = false;
                            if (!string.IsNullOrWhiteSpace(targetTag) && !targetTag.StartsWith("("))
                            {
                                updated = blk.SetAttributeValue(targetTag, formattedVal, tr);
                            }
                            else
                            {
                                // Gán vào attribute đầu tiên có sẵn
                                foreach (ObjectId attId in blk.AttributeCollection)
                                {
                                    if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference att)
                                    {
                                        att.TextString = formattedVal;
                                        updated = true;
                                        break;
                                    }
                                }
                            }

                            if (updated)
                            {
                                count++;
                                currentNum += step;
                            }
                        }
                        else if (ent is DBText dbText)
                        {
                            dbText.TextString = formattedVal;
                            count++;
                            currentNum += step;
                        }
                        else if (ent is MText mText)
                        {
                            mText.Contents = formattedVal;
                            count++;
                            currentNum += step;
                        }
                    }

                    nextNumber = currentNum;
                });

                ed.WriteMessage($"\n[DSTT] HOÀN TẤT! Đã đánh số thành công {count} đối tượng.");
                ed.Regen();
            }

            if (count > 0)
            {
                TxtAttrStatus.Text = $"✅ Đã áp dụng thành công {count} đối tượng vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
                TxtAttrFrom.Text = nextNumber.ToString();
            }
        }

        private void BtnAttrPickOneByOne_Click(object sender, RoutedEventArgs e)
        {
            var ed = CadDatabaseHelper.ActiveEd;

            string prefix = TxtAttrPrefix.Text ?? "";
            string suffix = TxtAttrSuffix.Text ?? "";
            int currentNum = ParseInt(TxtAttrFrom.Text, 1);
            int step = Math.Max(1, ParseInt(TxtAttrStep.Text, 1));
            int styleIndex = CboAttrNumberStyle.SelectedIndex;
            bool twoDigits = ChkAttrTwoDigits.IsChecked == true;
            string targetTag = TxtAttrTagName.Text.Trim();

            int count = 0;

            using (ed.StartUserInteraction(this))
            {
                ed.WriteMessage($"\n--- BẮT ĐẦU ĐÁNH SỐ TỪNG ĐỐI TƯỢNG (ESC ĐỂ DỪNG) ---");

                while (true)
                {
                    string targetVal = $"{prefix}{FormatNumber(currentNum, styleIndex, twoDigits)}{suffix}";
                    var peo = new PromptEntityOptions($"\nClick chọn đối tượng tiếp theo để gán '{targetVal}' (ESC/Enter để dừng): ");
                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) break;

                    bool updated = false;
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        var ent = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                        if (ent is BlockReference blk)
                        {
                            if (!string.IsNullOrWhiteSpace(targetTag) && !targetTag.StartsWith("("))
                            {
                                updated = blk.SetAttributeValue(targetTag, targetVal, tr);
                            }
                            else
                            {
                                // Tìm attribute gần điểm click nhất
                                double minDist = double.MaxValue;
                                AttributeReference? targetAtt = null;
                                foreach (ObjectId attId in blk.AttributeCollection)
                                {
                                    if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference att)
                                    {
                                        double dist = att.Position.DistanceTo(per.PickedPoint);
                                        if (dist < minDist)
                                        {
                                            minDist = dist;
                                            targetAtt = att;
                                        }
                                    }
                                }

                                if (targetAtt != null)
                                {
                                    targetAtt.TextString = targetVal;
                                    updated = true;
                                }
                            }
                        }
                        else if (ent is DBText dbText)
                        {
                            dbText.TextString = targetVal;
                            updated = true;
                        }
                        else if (ent is MText mText)
                        {
                            mText.Contents = targetVal;
                            updated = true;
                        }
                    });

                    if (updated)
                    {
                        ed.WriteMessage($" -> Gán: '{targetVal}'");
                        currentNum += step;
                        count++;
                    }
                    else
                    {
                        ed.WriteMessage("\nKhông thể cập nhật đối tượng này.");
                    }
                }

                ed.WriteMessage($"\n[DSTT] Đã đánh số {count} đối tượng.");
                ed.Regen();
            }

            if (count > 0)
            {
                TxtAttrStatus.Text = $"✅ Đã áp dụng {count} đối tượng vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
                TxtAttrFrom.Text = currentNum.ToString();
            }
        }

        #endregion

        #region TAB 1: Đánh số Khung tên bản vẽ

        private void BtnFrameSelectBlocks_Click(object sender, RoutedEventArgs e)
        {
            var ed = CadDatabaseHelper.ActiveEd;

            string prefix = TxtFramePrefix.Text ?? "";
            string suffix = TxtFrameSuffix.Text ?? "";
            int startNum = ParseInt(TxtFrameFrom.Text, 1);
            int styleIndex = CboFrameNumberStyle.SelectedIndex;
            bool twoDigits = ChkFrameTwoDigits.IsChecked == true;
            string targetBlockName = TxtFrameBlockName.Text.Trim();
            string targetAttrTag = TxtFrameAttrTag.Text.Trim();
            int dirIndex = CboFrameDirection.SelectedIndex;

            bool usePageMode = ChkFramePageMode.IsChecked == true;
            bool autoTotal = ChkFrameAutoTotal.IsChecked == true;
            int totalPages = ParseInt(TxtFrameTotalPages.Text, 24);
            string totalPagesTag = TxtFrameTotalPagesTag.Text.Trim();
            string totalPrefix = TxtFrameTotalPagesPrefix.Text ?? "/";

            if (string.IsNullOrWhiteSpace(targetAttrTag))
            {
                MessageBox.Show("Vui lòng nhập Attribute TAG cho số trang (ví dụ: A00).", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = 0;

            using (ed.StartUserInteraction(this))
            {
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = $"\nQuét chọn các khung tên '{targetBlockName}' cần đánh số trang: "
                };

                var filter = SelectionHelper.CreateTypeFilter("INSERT");
                var selRes = ed.GetSelection(pso, filter);

                if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
                {
                    ed.WriteMessage("\nChưa chọn khung tên nào.");
                    return;
                }

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var blockList = new List<(Entity ent, Point3d pos)>();

                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (selObj == null) continue;
                        if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blk)
                        {
                            if (!string.IsNullOrWhiteSpace(targetBlockName))
                            {
                                string blkName = blk.GetEffectiveBlockName(tr);
                                if (!string.Equals(blkName, targetBlockName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }
                            blockList.Add((blk, blk.Position));
                        }
                    }

                    if (blockList.Count == 0)
                    {
                        ed.WriteMessage($"\nKhông tìm thấy khung tên '{targetBlockName}' trong vùng chọn.");
                        return;
                    }

                    // Sắp xếp khung tên theo hướng
                    var sorted = SortEntitiesByDirection(blockList, dirIndex);

                    int actualTotal = autoTotal ? sorted.Count : Math.Max(totalPages, sorted.Count);
                    string totalStr = FormatNumber(actualTotal, styleIndex, twoDigits);
                    string totalValue = $"{totalPrefix}{totalStr}";

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        int currentNumber = startNum + i;
                        string numStr = FormatNumber(currentNumber, styleIndex, twoDigits);
                        string pageValue = $"{prefix}{numStr}{suffix}";

                        if (sorted[i].ent is BlockReference blkRef)
                        {
                            bool written = blkRef.SetAttributeValue(targetAttrTag, pageValue, tr);
                            if (written) count++;

                            if (usePageMode && !string.IsNullOrWhiteSpace(totalPagesTag))
                            {
                                blkRef.SetAttributeValue(totalPagesTag, totalValue, tr);
                            }

                            ed.WriteMessage($"\n   [{i + 1}] -> {targetAttrTag}='{pageValue}'" + (usePageMode ? $", {totalPagesTag}='{totalValue}'" : ""));
                        }
                    }
                });

                ed.WriteMessage($"\n\n[DSTTKBT] HOÀN TẤT! Đã đánh số {count} khung tên bản vẽ.");
                ed.Regen();
            }

            if (count > 0)
            {
                TxtFrameStatus.Text = $"✅ Đã áp dụng {count} khung tên vào CAD! (Bấm 'Đóng' nếu hoàn tất)";
            }
        }

        #endregion

        #region Spatial Sorting Logic (Sắp xếp không gian với dung sai)

        /// <summary>
        /// Sắp xếp danh sách đối tượng theo hướng và dung sai hàng/cột
        /// 0: Trái => Phải | Trên => Dưới (Xếp theo dòng từ trên xuống, mỗi dòng từ trái qua phải)
        /// 1: Trái => Phải | Dưới => Trên (Xếp theo dòng từ dưới lên, mỗi dòng từ trái qua phải)
        /// 2: Trên => Dưới | Trái => Phải (Xếp theo cột từ trái qua phải, mỗi cột từ trên xuống dưới - Domino)
        /// 3: Dưới => Trên | Trái => Phải (Xếp theo cột từ trái qua phải, mỗi cột từ dưới lên trên)
        /// </summary>
        public static List<(Entity ent, Point3d pos)> SortEntitiesByDirection(List<(Entity ent, Point3d pos)> items, int dirIndex)
        {
            if (items.Count <= 1) return items;

            const double tolerance = 20.0; // Dung sai tọa độ 20 unit

            switch (dirIndex)
            {
                case 1: // Trái => Phải | Dưới => Trên
                    return items
                        .GroupBy(item => Math.Round(item.pos.Y / tolerance))
                        .OrderBy(grp => grp.Key) // Dưới lên
                        .SelectMany(grp => grp.OrderBy(item => item.pos.X)) // Trái qua phải
                        .ToList();

                case 2: // Trên => Dưới | Trái => Phải (Cột domino chuẩn)
                    return items
                        .GroupBy(item => Math.Round(item.pos.X / tolerance))
                        .OrderBy(grp => grp.Key) // Trái qua phải
                        .SelectMany(grp => grp.OrderByDescending(item => item.pos.Y)) // Trên xuống
                        .ToList();

                case 3: // Dưới => Trên | Trái => Phải
                    return items
                        .GroupBy(item => Math.Round(item.pos.X / tolerance))
                        .OrderBy(grp => grp.Key) // Trái qua phải
                        .SelectMany(grp => grp.OrderBy(item => item.pos.Y)) // Dưới lên
                        .ToList();

                default: // 0: Trái => Phải | Trên => Dưới (Mặc định)
                    return items
                        .GroupBy(item => Math.Round(item.pos.Y / tolerance))
                        .OrderByDescending(grp => grp.Key) // Trên xuống
                        .SelectMany(grp => grp.OrderBy(item => item.pos.X)) // Trái qua phải
                        .ToList();
            }
        }

        #endregion

        #region Format Helper Methods

        public static string FormatNumber(int number, int styleIndex, bool twoDigits)
        {
            switch (styleIndex)
            {
                case 1: // A,B,C,...
                    return number <= 26 ? ((char)('A' + number - 1)).ToString() : number.ToString();
                case 2: // I,II,III,...
                    return ToRoman(number);
                default: // 1,2,3,...
                    return twoDigits ? number.ToString("D2") : number.ToString();
            }
        }

        public static string ToRoman(int number)
        {
            if (number <= 0 || number > 3999) return number.ToString();

            string[] thousands = { "", "M", "MM", "MMM" };
            string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
            string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

            return thousands[number / 1000]
                 + hundreds[(number % 1000) / 100]
                 + tens[(number % 100) / 10]
                 + ones[number % 10];
        }

        private static int ParseInt(string? str, int defaultVal)
        {
            return int.TryParse(str, out int val) ? val : defaultVal;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
