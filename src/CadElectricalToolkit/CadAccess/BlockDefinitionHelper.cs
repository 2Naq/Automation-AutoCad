using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using CadElectricalToolkit.Core;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Lớp tiện ích tự động khởi tạo định nghĩa Block (BlockTableRecord) và chèn BlockReference
    /// </summary>
    public static class BlockDefinitionHelper
    {
        public static string BlockWireNumber => ElectricalConfig.BlockWireNumber;
        public static string TagWireNo => ElectricalConfig.TagWireNumber;

        public static string BlockTerminal => ElectricalConfig.BlockTerminal;
        public static string TagTbName => ElectricalConfig.TagTerminalStrip;
        public static string TagPinNo => ElectricalConfig.TagTerminalPin;
        public static string TagWireFrom => ElectricalConfig.TagWireFrom;
        public static string TagWireTo => ElectricalConfig.TagWireTo;

        public static string BlockTitle => ElectricalConfig.BlockTitle;
        public static string TagSheetNo => ElectricalConfig.TagSheetNumber;
        public static string TagTotalSheets = "TOTAL_SHEET";
        public static string TagSheetName => ElectricalConfig.TagSheetName;
        public static string TagRev => ElectricalConfig.TagRevision;
        public static string TagDate => ElectricalConfig.TagDate;

        /// <summary>
        /// Chèn một BlockReference kèm theo các giá trị Attribute vào ModelSpace
        /// </summary>
        public static ObjectId InsertBlock(
            Database db, 
            Transaction tr, 
            string blockName, 
            Point3d position, 
            Dictionary<string, string> attributeValues,
            double rotation = 0.0,
            double scale = 1.0)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(blockName))
            {
                throw new ArgumentException($"Block '{blockName}' chua ton tai trong ban ve.");
            }

            var btrDefinition = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            var blkRef = new BlockReference(position, bt[blockName])
            {
                Rotation = rotation,
                ScaleFactors = new Scale3d(scale)
            };

            var blkRefId = ms.AppendEntity(blkRef);
            tr.AddNewlyCreatedDBObject(blkRef, true);

            // Duyet qua cac AttributeDefinition trong Block Definition va tao AttributeReference
            foreach (ObjectId id in btrDefinition)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition attDef)
                {
                    var attRef = new AttributeReference();
                    attRef.SetAttributeFromBlock(attDef, blkRef.BlockTransform);
                    
                    if (attributeValues != null && attributeValues.TryGetValue(attDef.Tag, out string val))
                    {
                        attRef.TextString = val;
                    }
                    else
                    {
                        attRef.TextString = attDef.TextString;
                    }

                    blkRef.AttributeCollection.AppendAttribute(attRef);
                    tr.AddNewlyCreatedDBObject(attRef, true);
                }
            }

            return blkRefId;
        }

        /// <summary>
        /// Đảm bảo Block định nghĩa 'GEN_SO' tồn tại, nếu chưa có sẽ tự động tạo
        /// </summary>
        public static ObjectId EnsureWireNumberBlock(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            if (bt.Has(BlockWireNumber))
            {
                return bt[BlockWireNumber];
            }

            var btr = new BlockTableRecord { Name = BlockWireNumber };

            // Khung chu nhat bao quanh: Rong 14mm, Cao 6mm, goc goc tai (-7, -3)
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(-7, -3), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(7, -3), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(7, 3), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(-7, 3), 0, 0, 0);
            pline.Closed = true;
            pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 4); // Cyan

            btr.AppendEntity(pline);

            // Attribute WIRE_NO nam o giua
            var attDef = new AttributeDefinition
            {
                Tag = TagWireNo,
                Prompt = "Nhap so day: ",
                TextString = "101",
                Height = 2.5,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = new Point3d(0, 0, 0),
                Position = new Point3d(0, 0, 0),
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2) // Yellow
            };
            btr.AppendEntity(attDef);

            var btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            return btrId;
        }

        /// <summary>
        /// Đảm bảo Block định nghĩa 'CAU_DAU' tồn tại, nếu chưa có sẽ tự động tạo
        /// </summary>
        public static ObjectId EnsureTerminalBlock(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            if (bt.Has(BlockTerminal))
            {
                return bt[BlockTerminal];
            }

            var btr = new BlockTableRecord { Name = BlockTerminal };

            // Hinh tron ky hieu chan cau dau (Ban kinh 4mm)
            var circle = new Circle(Point3d.Origin, Vector3d.ZAxis, 4.0)
            {
                Color = Color.FromColorIndex(ColorMethod.ByAci, 3) // Green
            };
            btr.AppendEntity(circle);

            // Duong ke ngang chia doi
            var line = new Line(new Point3d(-4, 0, 0), new Point3d(4, 0, 0))
            {
                Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
            };
            btr.AppendEntity(line);

            // Attribute TB_TAG (Vi du: TB1) o nua tren
            var attTb = new AttributeDefinition
            {
                Tag = TagTbName,
                Prompt = "Ten cau dau (TB): ",
                TextString = "TB1",
                Height = 2.0,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = new Point3d(0, 1.8, 0),
                Position = new Point3d(0, 1.8, 0),
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2)
            };
            btr.AppendEntity(attTb);

            // Attribute PIN_NO (Vi du: 1, 2, 3...) o nua duoi
            var attPin = new AttributeDefinition
            {
                Tag = TagPinNo,
                Prompt = "So chan cau dau: ",
                TextString = "1",
                Height = 2.0,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = new Point3d(0, -1.8, 0),
                Position = new Point3d(0, -1.8, 0),
                Color = Color.FromColorIndex(ColorMethod.ByAci, 1) // Red
            };
            btr.AppendEntity(attPin);

            // Attribute an: WIRE_FROM va WIRE_TO
            var attFrom = new AttributeDefinition
            {
                Tag = TagWireFrom,
                Prompt = "Day noi den (From): ",
                TextString = "",
                Height = 1.5,
                Invisible = true,
                Position = new Point3d(-6, 0, 0)
            };
            btr.AppendEntity(attFrom);

            var attTo = new AttributeDefinition
            {
                Tag = TagWireTo,
                Prompt = "Day noi di (To): ",
                TextString = "",
                Height = 1.5,
                Invisible = true,
                Position = new Point3d(6, 0, 0)
            };
            btr.AppendEntity(attTo);

            var btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            return btrId;
        }

        /// <summary>
        /// Tạo nhanh tất cả Block mẫu chuẩn điện công nghiệp trong bản vẽ hiện hành
        /// </summary>
        public static void CreateAllSampleBlocks(Database db, Transaction tr)
        {
            EnsureWireNumberBlock(db, tr);
            EnsureTerminalBlock(db, tr);
            EnsureTitleBlock(db, tr);
            EnsureRelayBlocks(db, tr);
            EnsurePlcBlocks(db, tr);
        }

        /// <summary>
        /// Khung tên mẫu chuẩn (KHUNG_TEN) với các Attributes quản lý trang
        /// </summary>
        public static ObjectId EnsureTitleBlock(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            if (bt.Has(BlockTitle)) return bt[BlockTitle];

            var btr = new BlockTableRecord { Name = BlockTitle };

            // Khung A3 ti le 1:1 (420 x 297 mm)
            var border = new Polyline();
            border.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
            border.AddVertexAt(1, new Point2d(420, 0), 0, 0, 0);
            border.AddVertexAt(2, new Point2d(420, 297), 0, 0, 0);
            border.AddVertexAt(3, new Point2d(0, 297), 0, 0, 0);
            border.Closed = true;
            border.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
            btr.AppendEntity(border);

            // Attribute So trang: SHEET_NO
            var attSheet = new AttributeDefinition
            {
                Tag = TagSheetNo,
                Prompt = "So trang: ",
                TextString = "01",
                Height = 3.5,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = new Point3d(380, 20, 0),
                Position = new Point3d(380, 20, 0),
                Color = Color.FromColorIndex(ColorMethod.ByAci, 1)
            };
            btr.AppendEntity(attSheet);

            // Attribute Ten ban ve: SHEET_NAME
            var attName = new AttributeDefinition
            {
                Tag = TagSheetName,
                Prompt = "Ten ban ve: ",
                TextString = "SO DO NGUYEN LY",
                Height = 3.5,
                Justify = AttachmentPoint.MiddleLeft,
                AlignmentPoint = new Point3d(250, 30, 0),
                Position = new Point3d(250, 30, 0),
                Color = Color.FromColorIndex(ColorMethod.ByAci, 2)
            };
            btr.AppendEntity(attName);

            // Attribute Revision: REV
            var attRev = new AttributeDefinition
            {
                Tag = TagRev,
                Prompt = "Lan sua doi: ",
                TextString = "00",
                Height = 2.5,
                Justify = AttachmentPoint.MiddleCenter,
                AlignmentPoint = new Point3d(405, 20, 0),
                Position = new Point3d(405, 20, 0)
            };
            btr.AppendEntity(attRev);

            var btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            return btrId;
        }

        private static void EnsureRelayBlocks(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);

            // 1. RL_COIL (Cuộn hút rơ-le)
            if (!bt.Has("RL_COIL"))
            {
                var btr = new BlockTableRecord { Name = "RL_COIL" };
                var rect = new Polyline();
                rect.AddVertexAt(0, new Point2d(-6, -4), 0, 0, 0);
                rect.AddVertexAt(1, new Point2d(6, -4), 0, 0, 0);
                rect.AddVertexAt(2, new Point2d(6, 4), 0, 0, 0);
                rect.AddVertexAt(3, new Point2d(-6, 4), 0, 0, 0);
                rect.Closed = true;
                btr.AppendEntity(rect);

                var attTag = new AttributeDefinition { Tag = "TAG", TextString = "KA1", Height = 2.5, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = new Point3d(0, 0, 0), Position = new Point3d(0, 0, 0) };
                var attPins = new AttributeDefinition { Tag = "PINS", TextString = "13-14", Height = 1.8, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = new Point3d(0, -6, 0), Position = new Point3d(0, -6, 0) };
                btr.AppendEntity(attTag);
                btr.AppendEntity(attPins);

                bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
            }

            // 2. RL_NO (Tiếp điểm thường mở)
            if (!bt.Has("RL_NO"))
            {
                var btr = new BlockTableRecord { Name = "RL_NO" };
                var l1 = new Line(new Point3d(0, 6, 0), new Point3d(0, 2, 0));
                var l2 = new Line(new Point3d(0, -6, 0), new Point3d(0, -2, 0));
                var c1 = new Line(new Point3d(-2, 2, 0), new Point3d(2, 2, 0));
                var c2 = new Line(new Point3d(-2, -2, 0), new Point3d(2, -2, 0));
                btr.AppendEntity(l1); btr.AppendEntity(l2); btr.AppendEntity(c1); btr.AppendEntity(c2);

                var attTag = new AttributeDefinition { Tag = "TAG", TextString = "KA1", Height = 2.0, Position = new Point3d(3, 1, 0) };
                var attPin = new AttributeDefinition { Tag = "PIN_NO", TextString = "9-5", Height = 1.8, Position = new Point3d(3, -2, 0) };
                btr.AppendEntity(attTag); btr.AppendEntity(attPin);

                bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
            }
        }

        private static void EnsurePlcBlocks(Database db, Transaction tr)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);

            // Block PLC_DI
            if (!bt.Has("PLC_DI"))
            {
                var btr = new BlockTableRecord { Name = "PLC_DI" };
                var box = new Polyline();
                box.AddVertexAt(0, new Point2d(-10, -5), 0, 0, 0);
                box.AddVertexAt(1, new Point2d(10, -5), 0, 0, 0);
                box.AddVertexAt(2, new Point2d(10, 5), 0, 0, 0);
                box.AddVertexAt(3, new Point2d(-10, 5), 0, 0, 0);
                box.Closed = true;
                btr.AppendEntity(box);

                var attAddr = new AttributeDefinition { Tag = "PLC_ADDR", TextString = "X0", Height = 2.5, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = new Point3d(0, 2, 0), Position = new Point3d(0, 2, 0) };
                var attDesc = new AttributeDefinition { Tag = "DESC", TextString = "START BTN", Height = 2.0, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = new Point3d(0, -2, 0), Position = new Point3d(0, -2, 0) };
                btr.AppendEntity(attAddr); btr.AppendEntity(attDesc);

                bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
            }
        }
    }
}
