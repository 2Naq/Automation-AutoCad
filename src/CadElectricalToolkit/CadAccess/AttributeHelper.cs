using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Các phương thức tiện ích hỗ trợ đọc và ghi Attribute của BlockReference
    /// </summary>
    public static class AttributeHelper
    {
        /// <summary>
        /// Lấy toàn bộ cặp Tag - Value của một BlockReference
        /// </summary>
        public static Dictionary<string, string> GetAttributes(this BlockReference blkRef, Transaction tr)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (blkRef == null || tr == null) return result;

            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                {
                    result[attRef.Tag] = attRef.TextString;
                }
            }
            return result;
        }

        /// <summary>
        /// Lấy giá trị của một Tag cụ thể trong BlockReference
        /// </summary>
        public static string? GetAttributeValue(this BlockReference blkRef, string tag, Transaction tr)
        {
            if (blkRef == null || tr == null) return null;

            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                {
                    if (string.Equals(attRef.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return attRef.TextString;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Lấy giá trị của một Tag từ danh sách các tên Tag dự phòng (không phân biệt hoa thường)
        /// Ví dụ: blk.GetAttributeValue(tr, "NAME", "TAG", "TAGNAME")
        /// </summary>
        public static string? GetAttributeValue(this BlockReference blkRef, Transaction tr, params string[] candidateTags)
        {
            if (blkRef == null || tr == null || candidateTags == null || candidateTags.Length == 0) return null;

            var attrs = blkRef.GetAttributes(tr);
            foreach (var tag in candidateTags)
            {
                if (attrs.TryGetValue(tag, out var val) && !string.IsNullOrWhiteSpace(val))
                {
                    return val;
                }
            }
            return null;
        }

        /// <summary>
        /// Gán giá trị mới cho một Tag trong BlockReference
        /// </summary>
        public static bool SetAttributeValue(this BlockReference blkRef, string tag, string newValue, Transaction tr)
        {
            if (blkRef == null || tr == null) return false;

            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                var obj = tr.GetObject(attId, OpenMode.ForRead);
                if (obj is AttributeReference attRef)
                {
                    if (string.Equals(attRef.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!attRef.IsWriteEnabled)
                        {
                            attRef.UpgradeOpen();
                        }
                        attRef.TextString = newValue;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gán giá trị mới cho một Tag từ danh sách các tên Tag dự phòng (gán vào Tag đầu tiên tìm thấy)
        /// </summary>
        public static bool SetAttributeValue(this BlockReference blkRef, Transaction tr, string newValue, params string[] candidateTags)
        {
            if (blkRef == null || tr == null || candidateTags == null || candidateTags.Length == 0) return false;

            foreach (ObjectId attId in blkRef.AttributeCollection)
            {
                var obj = tr.GetObject(attId, OpenMode.ForRead);
                if (obj is AttributeReference attRef)
                {
                    foreach (var tag in candidateTags)
                    {
                        if (string.Equals(attRef.Tag, tag, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!attRef.IsWriteEnabled)
                            {
                                attRef.UpgradeOpen();
                            }
                            attRef.TextString = newValue;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Lấy tên thực tế của Block (xử lý cả Anonymous Block / Dynamic Block)
        /// </summary>
        public static string GetEffectiveBlockName(this BlockReference blkRef, Transaction tr)
        {
            if (blkRef == null || tr == null) return string.Empty;

            if (blkRef.IsDynamicBlock)
            {
                var dynamicBlockTableRecord = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                return dynamicBlockTableRecord.Name;
            }

            return blkRef.Name;
        }
    }
}
