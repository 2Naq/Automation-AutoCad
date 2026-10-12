using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.UI.ViewModels;

namespace CadElectricalToolkit.UI.Services
{
    /// <summary>
    /// Triển khai ICadInteractionService sử dụng AutoCAD .NET Managed API.
    /// Tất cả tương tác Editor (chọn đối tượng, ghi dữ liệu) được tập trung tại đây.
    /// ViewModel chỉ gọi interface, không bao giờ import AutoCAD namespace trực tiếp.
    /// </summary>
    public class CadInteractionService : ICadInteractionService
    {
        public bool IsAvailable => CadDatabaseHelper.IsInCad;

        #region Pick Block (Chọn TAG / Block mẫu)

        public PickBlockResult? PickBlock(System.Windows.Window ownerWindow, string prompt)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return null;

            PickBlockResult? result = null;

            using (ed.StartUserInteraction(ownerWindow))
            {
                var peo = new PromptEntityOptions($"\n{prompt}");
                peo.SetRejectMessage("\nVui lòng chọn một Block Reference hoặc Text!");
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return null;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (ent is BlockReference blk)
                    {
                        result = new PickBlockResult
                        {
                            BlockName = blk.GetEffectiveBlockName(tr)
                        };

                        double minDist = double.MaxValue;
                        // 1. Quét toàn bộ AttributeReference trên instance block
                        foreach (ObjectId attId in blk.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                            {
                                result.AllAttributes[att.Tag] = att.TextString;
                                double deg = Math.Round(att.Rotation * 180.0 / Math.PI, 1);
                                deg = (deg % 360.0 + 360.0) % 360.0;
                                result.AttributeRotations[att.Tag] = deg;

                                double dist = att.Position.DistanceTo(per.PickedPoint);
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    result.ClosestTag = att.Tag;
                                    result.RotationDegrees = deg;
                                }
                            }
                        }

                        // 2. Quét thêm BlockTableRecord và DynamicBlockTableRecord để lấy trọn vẹn mọi attribute (kể cả rỗng)
                        var btrIds = new List<ObjectId> { blk.BlockTableRecord };
                        if (blk.IsDynamicBlock && !blk.DynamicBlockTableRecord.IsNull && blk.DynamicBlockTableRecord != blk.BlockTableRecord)
                        {
                            btrIds.Add(blk.DynamicBlockTableRecord);
                        }

                        foreach (var btrId in btrIds)
                        {
                            if (btrId.IsNull) continue;
                            if (tr.GetObject(btrId, OpenMode.ForRead) is BlockTableRecord btr)
                            {
                                foreach (ObjectId id in btr)
                                {
                                    if (tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition attDef)
                                    {
                                        if (!result.AllAttributes.ContainsKey(attDef.Tag))
                                        {
                                            result.AllAttributes[attDef.Tag] = attDef.TextString;
                                            double deg = Math.Round(attDef.Rotation * 180.0 / Math.PI, 1);
                                            result.AttributeRotations[attDef.Tag] = (deg % 360.0 + 360.0) % 360.0;
                                        }
                                    }
                                }
                            }
                        }

                        ed.WriteMessage($"\n[ĐÃ NHẬN DIỆN] Block: '{result.BlockName}' | Attribute TAG: '{result.ClosestTag}' (Tổng {result.AllAttributes.Count} TAGs)");
                    }
                    else if (ent is DBText dt)
                    {
                        double deg = Math.Round(dt.Rotation * 180.0 / Math.PI, 1);
                        deg = (deg % 360.0 + 360.0) % 360.0;
                        result = new PickBlockResult
                        {
                            IsText = true,
                            TextContent = dt.TextString,
                            RotationDegrees = deg
                        };
                        ed.WriteMessage($"\n[ĐÃ NHẬN DIỆN] Text: '{dt.TextString}' (Góc: {deg:0.##}°)");
                    }
                    else if (ent is MText mt)
                    {
                        double deg = Math.Round(mt.Rotation * 180.0 / Math.PI, 1);
                        deg = (deg % 360.0 + 360.0) % 360.0;
                        result = new PickBlockResult
                        {
                            IsText = true,
                            TextContent = mt.Contents,
                            RotationDegrees = deg
                        };
                        ed.WriteMessage($"\n[ĐÃ NHẬN DIỆN] MText: '{mt.Contents}' (Góc: {deg:0.##}°)");
                    }
                });
            }

            return result;
        }

        public List<string> GetBlockAttributeTags(string blockName)
        {
            var tags = new List<string>();
            if (string.IsNullOrWhiteSpace(blockName)) return tags;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt != null && bt.Has(blockName))
                {
                    var btr = tr.GetObject(bt[blockName], OpenMode.ForRead) as BlockTableRecord;
                    if (btr != null)
                    {
                        foreach (ObjectId id in btr)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition attDef)
                            {
                                if (!tags.Contains(attDef.Tag))
                                {
                                    tags.Add(attDef.Tag);
                                }
                            }
                        }
                    }
                }

                if (tags.Count == 0)
                {
                    var ms = tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead) as BlockTableRecord;
                    if (ms != null)
                    {
                        foreach (ObjectId id in ms)
                        {
                            if (id.ObjectClass.DxfName.Equals("INSERT", StringComparison.OrdinalIgnoreCase))
                            {
                                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blk)
                                {
                                    if (string.Equals(blk.GetEffectiveBlockName(tr), blockName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foreach (ObjectId attId in blk.AttributeCollection)
                                        {
                                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                                            {
                                                if (!tags.Contains(att.Tag))
                                                {
                                                    tags.Add(att.Tag);
                                                }
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            });

            return tags;
        }

        #endregion

        #region Number Block Attributes (Đánh số Attribute hàng loạt)

        public NumberingResult NumberBlockAttributes(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum, int step,
            int styleIndex, bool twoDigits,
            string targetBlockName, string targetTag,
            int dirIndex, bool includeText)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new NumberingResult();

            int count = 0;
            int nextNumber = startNum;

            using (ed.StartUserInteraction(ownerWindow))
            {
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nQuét chọn các Block / Text cần đánh số thứ tự: "
                };

                var selRes = ed.GetSelection(pso);
                if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
                {
                    ed.WriteMessage("\nChưa chọn đối tượng nào.");
                    return new NumberingResult();
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

                    var sorted = AutoNumberingViewModel.SortEntitiesByDirection(items, dirIndex);
                    int currentNum = startNum;

                    foreach (var (ent, _) in sorted)
                    {
                        string formattedVal = $"{prefix}{AutoNumberingViewModel.FormatNumber(currentNum, styleIndex, twoDigits)}{suffix}";

                        if (ent is BlockReference blk)
                        {
                            bool updated = false;
                            if (!string.IsNullOrWhiteSpace(targetTag) && !targetTag.StartsWith("("))
                            {
                                updated = blk.SetAttributeValue(targetTag, formattedVal, tr);
                            }
                            else
                            {
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
                            if (updated) { count++; currentNum += step; }
                        }
                        else if (ent is DBText dbText)
                        {
                            dbText.TextString = formattedVal;
                            count++; currentNum += step;
                        }
                        else if (ent is MText mText)
                        {
                            mText.Contents = formattedVal;
                            count++; currentNum += step;
                        }
                    }

                    nextNumber = currentNum;
                });

                ed.WriteMessage($"\n[DSTT] HOÀN TẤT! Đã đánh số thành công {count} đối tượng.");
                ed.Regen();
            }

            return new NumberingResult { Count = count, NextNumber = nextNumber };
        }

        #endregion

        #region Number One By One (Click từng cái)

        public NumberingResult NumberOneByOne(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum, int step,
            int styleIndex, bool twoDigits, string targetTag)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new NumberingResult();

            int count = 0;
            int currentNum = startNum;

            using (ed.StartUserInteraction(ownerWindow))
            {
                ed.WriteMessage($"\n--- BẮT ĐẦU ĐÁNH SỐ TỪNG ĐỐI TƯỢNG (ESC ĐỂ DỪNG) ---");

                while (true)
                {
                    string targetVal = $"{prefix}{AutoNumberingViewModel.FormatNumber(currentNum, styleIndex, twoDigits)}{suffix}";
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
                                double minDist = double.MaxValue;
                                AttributeReference? targetAtt = null;
                                foreach (ObjectId attId in blk.AttributeCollection)
                                {
                                    if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference att)
                                    {
                                        double dist = att.Position.DistanceTo(per.PickedPoint);
                                        if (dist < minDist) { minDist = dist; targetAtt = att; }
                                    }
                                }
                                if (targetAtt != null) { targetAtt.TextString = targetVal; updated = true; }
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

            return new NumberingResult { Count = count, NextNumber = currentNum };
        }

        #endregion

        #region Number Frames (Đánh số Khung tên)

        public NumberingResult NumberFrames(
            System.Windows.Window ownerWindow,
            string prefix, string suffix, int startNum,
            int styleIndex, bool twoDigits,
            string targetBlockName, string targetAttrTag, int dirIndex,
            bool usePageMode, bool autoTotal, int totalPages,
            string totalPagesTag, string totalPrefix)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new NumberingResult();

            int count = 0;

            using (ed.StartUserInteraction(ownerWindow))
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
                    return new NumberingResult();
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

                    var sorted = AutoNumberingViewModel.SortEntitiesByDirection(blockList, dirIndex);

                    int actualTotal = autoTotal ? sorted.Count : Math.Max(totalPages, sorted.Count);
                    string totalStr = AutoNumberingViewModel.FormatNumber(actualTotal, styleIndex, twoDigits);
                    string totalValue = $"{totalPrefix}{totalStr}";

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        int currentNumber = startNum + i;
                        string numStr = AutoNumberingViewModel.FormatNumber(currentNumber, styleIndex, twoDigits);
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

            return new NumberingResult { Count = count, NextNumber = startNum + count };
        }

        #endregion

        #region Batch Edit Attributes (Đổi Attribute hàng loạt)

        public BatchEditResult BatchEditAttributes(
            System.Windows.Window ownerWindow,
            string targetBlockName, string targetTag,
            bool isDirect, string newValFixed,
            string findText, string replaceText, bool matchCase)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new BatchEditResult();

            int count = 0;

            using (ed.StartUserInteraction(ownerWindow))
            {
                string msgTarget = string.IsNullOrEmpty(targetBlockName)
                    ? $"tất cả các Block có chứa TAG '{targetTag}'"
                    : $"các Block '{targetBlockName}'";
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = $"\nQuét chọn {msgTarget} để đổi Attribute '{targetTag}': "
                };

                var filter = SelectionHelper.CreateTypeFilter("INSERT");
                var selRes = ed.GetSelection(pso, filter);
                if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
                {
                    ed.WriteMessage("\nChưa chọn Block nào.");
                    return new BatchEditResult();
                }

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (selObj == null) continue;
                        if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is BlockReference blk)
                        {
                            if (!string.IsNullOrWhiteSpace(targetBlockName))
                            {
                                string bName = blk.GetEffectiveBlockName(tr);
                                if (!string.Equals(bName, targetBlockName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }

                            string? curVal = blk.GetAttributeValue(targetTag, tr);
                            if (curVal == null) continue;

                            string calculatedNewVal = isDirect
                                ? newValFixed
                                : ReplaceSubstring(curVal, findText, replaceText, matchCase);

                            bool written = blk.SetAttributeValue(targetTag, calculatedNewVal, tr);
                            if (written) count++;
                        }
                    }
                });

                ed.WriteMessage($"\n[ĐỔI ATTRIBUTE] HOÀN TẤT! Đã cập nhật thành công {count} Block có TAG '{targetTag}'.");
                ed.Regen();
            }

            return new BatchEditResult { Count = count };
        }

        public BatchEditResult BatchEditOneByOne(
            System.Windows.Window ownerWindow,
            string targetTag,
            bool isDirect, string newValFixed,
            string findText, string replaceText, bool matchCase)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new BatchEditResult();

            int count = 0;

            using (ed.StartUserInteraction(ownerWindow))
            {
                ed.WriteMessage($"\n--- CLICK TỪNG BLOCK ĐỂ ĐỔI ATTRIBUTE '{targetTag}' (ESC ĐỂ DỪNG) ---");

                while (true)
                {
                    var peo = new PromptEntityOptions($"\nClick chọn Block tiếp theo để đổi '{targetTag}' (ESC để dừng): ");
                    peo.SetRejectMessage("\nVui lòng chọn một Block Reference!");
                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) break;

                    bool updated = false;
                    string resultVal = "";
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        if (tr.GetObject(per.ObjectId, OpenMode.ForWrite) is BlockReference blk)
                        {
                            string? curVal = blk.GetAttributeValue(targetTag, tr);
                            if (curVal != null)
                            {
                                resultVal = isDirect ? newValFixed : ReplaceSubstring(curVal, findText, replaceText, matchCase);
                                updated = blk.SetAttributeValue(targetTag, resultVal, tr);
                            }
                        }
                    });

                    if (updated)
                    {
                        count++;
                        ed.WriteMessage($" -> [{count}] Đổi '{targetTag}' thành: '{resultVal}'");
                    }
                    else
                    {
                        ed.WriteMessage($"\nBlock này không chứa Attribute TAG '{targetTag}'.");
                    }
                }

                ed.WriteMessage($"\n[ĐỔI ATTRIBUTE] Đã đổi {count} block.");
                ed.Regen();
            }

            return new BatchEditResult { Count = count };
        }

        #endregion

        #region Rotate Attributes (Xoay Attribute & Text hàng loạt)

        public double? PickAngle(System.Windows.Window ownerWindow)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return null;

            double? angleDeg = null;

            using (ed.StartUserInteraction(ownerWindow))
            {
                var peo = new PromptEntityOptions("\nChọn Attribute, Text hoặc đường Line trên CAD để lấy góc xoay: ");
                peo.SetRejectMessage("\nVui lòng chọn một đối tượng hợp lệ!");
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return null;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    if (ent is BlockReference blk)
                    {
                        double minDist = double.MaxValue;
                        foreach (ObjectId attId in blk.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                            {
                                double dist = att.Position.DistanceTo(per.PickedPoint);
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    double d = Math.Round(att.Rotation * 180.0 / Math.PI, 1);
                                    angleDeg = (d % 360.0 + 360.0) % 360.0;
                                }
                            }
                        }
                    }
                    else if (ent is DBText dt)
                    {
                        double d = Math.Round(dt.Rotation * 180.0 / Math.PI, 1);
                        angleDeg = (d % 360.0 + 360.0) % 360.0;
                    }
                    else if (ent is MText mt)
                    {
                        double d = Math.Round(mt.Rotation * 180.0 / Math.PI, 1);
                        angleDeg = (d % 360.0 + 360.0) % 360.0;
                    }
                    else if (ent is Line line)
                    {
                        double rad = Math.Atan2(line.Delta.Y, line.Delta.X);
                        double d = Math.Round(rad * 180.0 / Math.PI, 1);
                        angleDeg = (d % 360.0 + 360.0) % 360.0;
                    }

                    if (angleDeg.HasValue)
                    {
                        ed.WriteMessage($"\n[LẤY GÓC XOAY] Đã đo được góc: {angleDeg.Value:0.##}°");
                    }
                });
            }

            return angleDeg;
        }

        public RotateResult RotateAttributes(
            System.Windows.Window ownerWindow,
            string targetBlockName,
            string targetTag,
            double targetAngleDegrees,
            bool isRelative,
            bool includeText,
            bool keepCenter = true,
            double offsetDistance = 0.0)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new RotateResult();

            int count = 0;

            using (ed.StartUserInteraction(ownerWindow))
            {
                string msgTarget = string.IsNullOrEmpty(targetBlockName)
                    ? $"tất cả các Block có chứa TAG '{targetTag}'"
                    : $"các Block '{targetBlockName}'";

                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = $"\nQuét chọn {msgTarget} để xoay TAG '{targetTag}' sang góc {targetAngleDegrees:0.##}°: "
                };

                var typeFilterStr = includeText ? "INSERT,TEXT,MTEXT" : "INSERT";
                var filter = SelectionHelper.CreateTypeFilter(typeFilterStr);
                var selRes = ed.GetSelection(pso, filter);
                if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
                {
                    ed.WriteMessage("\nChưa chọn đối tượng nào.");
                    return new RotateResult();
                }

                double radDeltaOrAbsolute = targetAngleDegrees * Math.PI / 180.0;

                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (selObj == null) continue;
                        var obj = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);

                        if (obj is BlockReference blk)
                        {
                            if (!string.IsNullOrWhiteSpace(targetBlockName))
                            {
                                string bName = blk.GetEffectiveBlockName(tr);
                                if (!string.Equals(bName, targetBlockName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }

                            bool blockUpdated = false;
                            foreach (ObjectId attId in blk.AttributeCollection)
                            {
                                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                                {
                                    if (string.Equals(att.Tag, targetTag, StringComparison.OrdinalIgnoreCase))
                                    {
                                        double newRad = isRelative
                                            ? (att.Rotation + radDeltaOrAbsolute)
                                            : radDeltaOrAbsolute;

                                        RotateEntityWithCenter(att, newRad, keepCenter, offsetDistance, db);
                                        blockUpdated = true;
                                    }
                                }
                            }

                            if (blockUpdated) count++;
                        }
                        else if (includeText)
                        {
                            if (obj is DBText dt)
                            {
                                if (string.IsNullOrEmpty(targetTag) || dt.TextString.IndexOf(targetTag, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    double newRad = isRelative ? (dt.Rotation + radDeltaOrAbsolute) : radDeltaOrAbsolute;
                                    RotateEntityWithCenter(dt, newRad, keepCenter, offsetDistance, db);
                                    count++;
                                }
                            }
                            else if (obj is MText mt)
                            {
                                if (string.IsNullOrEmpty(targetTag) || mt.Contents.IndexOf(targetTag, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    double newRad = isRelative ? (mt.Rotation + radDeltaOrAbsolute) : radDeltaOrAbsolute;
                                    RotateEntityWithCenter(mt, newRad, keepCenter, offsetDistance, db);
                                    count++;
                                }
                            }
                        }
                    }
                });

                ed.WriteMessage($"\n[XOAY ATTRIBUTE] HOÀN TẤT! Đã xoay thành công {count} đối tượng sang góc {targetAngleDegrees:0.##}°.");
                ed.Regen();
            }

            return new RotateResult { Count = count };
        }

        public RotateResult RotateOneByOne(
            System.Windows.Window ownerWindow,
            string targetTag,
            double targetAngleDegrees,
            bool isRelative,
            bool keepCenter = true,
            double offsetDistance = 0.0)
        {
            var ed = CadDatabaseHelper.ActiveEd;
            if (ed == null) return new RotateResult();

            int count = 0;
            double radDeltaOrAbsolute = targetAngleDegrees * Math.PI / 180.0;

            using (ed.StartUserInteraction(ownerWindow))
            {
                ed.WriteMessage($"\n--- CLICK TỪNG BLOCK ĐỂ XOAY TAG '{targetTag}' SANG GÓC {targetAngleDegrees:0.##}° (ESC ĐỂ DỪNG) ---");

                while (true)
                {
                    var peo = new PromptEntityOptions($"\nClick chọn Block tiếp theo để xoay '{targetTag}' (ESC để dừng): ");
                    peo.SetRejectMessage("\nVui lòng chọn một Block Reference hoặc Text!");
                    var per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) break;

                    bool updated = false;
                    CadDatabaseHelper.RunTransaction((tr, db) =>
                    {
                        var obj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                        if (obj is BlockReference blk)
                        {
                            foreach (ObjectId attId in blk.AttributeCollection)
                            {
                                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                                {
                                    if (string.Equals(att.Tag, targetTag, StringComparison.OrdinalIgnoreCase))
                                    {
                                        double newRad = isRelative ? (att.Rotation + radDeltaOrAbsolute) : radDeltaOrAbsolute;
                                        RotateEntityWithCenter(att, newRad, keepCenter, offsetDistance, db);
                                        updated = true;
                                    }
                                }
                            }
                        }
                        else if (obj is DBText dt)
                        {
                            double newRad = isRelative ? (dt.Rotation + radDeltaOrAbsolute) : radDeltaOrAbsolute;
                            RotateEntityWithCenter(dt, newRad, keepCenter, offsetDistance, db);
                            updated = true;
                        }
                        else if (obj is MText mt)
                        {
                            double newRad = isRelative ? (mt.Rotation + radDeltaOrAbsolute) : radDeltaOrAbsolute;
                            RotateEntityWithCenter(mt, newRad, keepCenter, offsetDistance, db);
                            updated = true;
                        }
                    });

                    if (updated)
                    {
                        count++;
                        ed.WriteMessage($" -> [{count}] Đã xoay thành công sang góc {targetAngleDegrees:0.##}°.");
                    }
                    else
                    {
                        ed.WriteMessage($"\nKhông tìm thấy Attribute TAG '{targetTag}' trên đối tượng này.");
                    }
                }

                ed.WriteMessage($"\n[XOAY ATTRIBUTE] Đã hoàn thành xoay {count} đối tượng.");
                ed.Regen();
            }

            return new RotateResult { Count = count };
        }

        #endregion

        #region Helpers

        private static bool TryGetEntityCenter(Entity ent, out Point3d center)
        {
            center = Point3d.Origin;
            if (ent == null) return false;
            try
            {
                var ext = ent.GeometricExtents;
                center = new Point3d(
                    (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                    (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
                    (ext.MinPoint.Z + ext.MaxPoint.Z) / 2.0);
                return true;
            }
            catch
            {
                if (ent is DBText dt)
                {
                    center = (dt.Justify == AttachmentPoint.BaseLeft) ? dt.Position : dt.AlignmentPoint;
                    return true;
                }
                if (ent is MText mt)
                {
                    center = mt.Location;
                    return true;
                }
                return false;
            }
        }

        private static void RotateEntityWithCenter(Entity ent, double newRad, bool keepCenter, double offsetDistance, Database db)
        {
            if (ent is DBText dt)
            {
                if (!dt.IsWriteEnabled) dt.UpgradeOpen();

                Point3d oldCenter = Point3d.Origin;
                bool hasOld = keepCenter && !string.IsNullOrWhiteSpace(dt.TextString) && TryGetEntityCenter(dt, out oldCenter);

                dt.Rotation = NormalizeAngle(newRad);
                dt.AdjustAlignment(db);

                if (hasOld && TryGetEntityCenter(dt, out Point3d newCenter))
                {
                    Vector3d totalDiff = oldCenter - newCenter;
                    double targetRad = NormalizeAngle(newRad);
                    Vector3d textDir = new Vector3d(Math.Cos(targetRad), Math.Sin(targetRad), 0.0);
                    if (textDir.Length > 1e-6)
                    {
                        textDir = textDir.GetNormal();
                    }

                    Vector3d normalDir = new Vector3d(-Math.Sin(targetRad), Math.Cos(targetRad), 0.0);
                    if (normalDir.Length > 1e-6)
                    {
                        normalDir = normalDir.GetNormal();
                    }

                    // Căn giữa theo chiều đọc của chữ (textDir), giữ nguyên vị trí gốc theo phương vuông góc,
                    // và hỗ trợ đẩy hở ra khỏi linh kiện (offsetDistance)
                    double shiftAlongDir = totalDiff.DotProduct(textDir);
                    Vector3d shift = (textDir * shiftAlongDir) + (normalDir * offsetDistance);

                    if (shift.Length > 1e-4)
                    {
                        dt.TransformBy(Matrix3d.Displacement(shift));
                        dt.AdjustAlignment(db);
                    }
                }
            }
            else if (ent is MText mt)
            {
                if (!mt.IsWriteEnabled) mt.UpgradeOpen();

                Point3d oldCenter = Point3d.Origin;
                bool hasOld = keepCenter && !string.IsNullOrWhiteSpace(mt.Contents) && TryGetEntityCenter(mt, out oldCenter);

                mt.Rotation = NormalizeAngle(newRad);

                if (hasOld && TryGetEntityCenter(mt, out Point3d newCenter))
                {
                    Vector3d totalDiff = oldCenter - newCenter;
                    double targetRad = NormalizeAngle(newRad);
                    Vector3d textDir = new Vector3d(Math.Cos(targetRad), Math.Sin(targetRad), 0.0);
                    if (textDir.Length > 1e-6)
                    {
                        textDir = textDir.GetNormal();
                    }

                    Vector3d normalDir = new Vector3d(-Math.Sin(targetRad), Math.Cos(targetRad), 0.0);
                    if (normalDir.Length > 1e-6)
                    {
                        normalDir = normalDir.GetNormal();
                    }

                    double shiftAlongDir = totalDiff.DotProduct(textDir);
                    Vector3d shift = (textDir * shiftAlongDir) + (normalDir * offsetDistance);

                    if (shift.Length > 1e-4)
                    {
                        mt.TransformBy(Matrix3d.Displacement(shift));
                    }
                }
            }
        }

        private static double NormalizeAngle(double radians)
        {
            double twoPi = 2.0 * Math.PI;
            radians %= twoPi;
            if (radians < 0) radians += twoPi;
            return radians;
        }

        private static string ReplaceSubstring(string input, string find, string replaceWith, bool matchCase)
        {
            if (string.IsNullOrEmpty(find)) return input;
            if (matchCase) return input.Replace(find, replaceWith);
            return Regex.Replace(input, Regex.Escape(find), replaceWith, RegexOptions.IgnoreCase);
        }

        #endregion
    }
}
