using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh tiện ích soạn thảo và thao tác nhanh (CDN, DSTT, GTD, GTN)
    /// </summary>
    public class DraftingCommands
    {
        /// <summary>
        /// CDN : Cộng Dim nhanh - Quét chọn các đường kích thước Dimension và tính tổng chiều dài
        /// </summary>
        [CommandMethod("CDN")]
        public void QuickSumDimensions()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var filter = SelectionHelper.CreateTypeFilter("DIMENSION");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChon cac doi tuong Dimension de tinh tong: " }, filter);

            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon doi tuong Dimension nao.");
                return;
            }

            double totalLength = 0;
            int count = 0;

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is Dimension dim)
                    {
                        totalLength += dim.Measurement;
                        count++;
                    }
                }
            });

            string resultStr = totalLength.ToString("F2");
            ed.WriteMessage($"\n[CDN] Tong chieu dai {count} Dimension da chon: {resultStr} mm");

            // Sao chep gia tri vao Clipboard de paste ngay vao Excel hoac CAD
            try
            {
                Clipboard.SetText(resultStr);
                ed.WriteMessage(" (Da sao chep vao Clipboard!)");
            }
            catch
            {
                // Bo qua neu loi clipboard
            }
        }

        /// <summary>
        /// DSTT : Đánh số thứ tự tăng dần tự động khi click vào Text / MText / Block Attribute
        /// </summary>
        [CommandMethod("DSTT")]
        public void AutoNumbering()
        {
            var ed = CadDatabaseHelper.ActiveEd;

            // Nhap tien to
            var psoPrefix = new PromptStringOptions("\nNhap tien to (Prefix) [Enter neu khong co]: ")
            {
                AllowSpaces = true
            };
            var pprPrefix = ed.GetString(psoPrefix);
            if (pprPrefix.Status != PromptStatus.OK) return;
            string prefix = pprPrefix.StringResult ?? string.Empty;

            // Nhap so bat dau
            var pioStart = new PromptIntegerOptions("\nNhap so bat dau <1>: ")
            {
                DefaultValue = 1,
                UseDefaultValue = true
            };
            var pirStart = ed.GetInteger(pioStart);
            if (pirStart.Status != PromptStatus.OK) return;
            int currentNum = pirStart.Value;

            // Nhap buoc nhay
            var pioStep = new PromptIntegerOptions("\nNhap buoc nhay <1>: ")
            {
                DefaultValue = 1,
                UseDefaultValue = true
            };
            var pirStep = ed.GetInteger(pioStep);
            if (pirStep.Status != PromptStatus.OK) return;
            int step = pirStep.Value;

            ed.WriteMessage($"\nBat dau danh so tu: {prefix}{currentNum}, buoc nhay {step}. Click chon Text/MText/Block de danh so (ESC de dung):");

            while (true)
            {
                var peo = new PromptEntityOptions($"\nChon Text/Block de gan gia tri '{prefix}{currentNum}': ");
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) break;

                bool updated = false;
                CadDatabaseHelper.RunTransaction((tr, db) =>
                {
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                    string targetValue = $"{prefix}{currentNum}";

                    if (ent is DBText dbText)
                    {
                        dbText.TextString = targetValue;
                        updated = true;
                    }
                    else if (ent is MText mText)
                    {
                        mText.Contents = targetValue;
                        updated = true;
                    }
                    else if (ent is BlockReference blkRef)
                    {
                        // Neu la block, tim attribute dau tien hoac attribute co tag TT/NO
                        foreach (ObjectId attId in blkRef.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForWrite) is AttributeReference attRef)
                            {
                                attRef.TextString = targetValue;
                                updated = true;
                                break;
                            }
                        }
                    }
                });

                if (updated)
                {
                    ed.WriteMessage($" -> Da gan: {prefix}{currentNum}");
                    currentNum += step;
                }
                else
                {
                    ed.WriteMessage("\nDoi tuong chon khong phai la Text, MText hoac Block co Attribute.");
                }
            }
        }

        /// <summary>
        /// GTD : Gom text dọc - Căn gióng các đối tượng Text theo phương thẳng đứng
        /// </summary>
        [CommandMethod("GTD")]
        public void AlignTextVertical()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var filter = SelectionHelper.CreateTypeFilter("TEXT,MTEXT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChon cac Text/MText can gom doc: " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count <= 1)
            {
                ed.WriteMessage("\nCan chon it nhat 2 doi tuong Text.");
                return;
            }

            var ppo = new PromptPointOptions("\nChon diem gióng chuan (hoac Enter lay vi tri Text tren cung): ");
            var ppr = ed.GetPoint(ppo);

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var textList = new List<(Entity ent, Point3d pos)>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForWrite);
                    if (ent is DBText t) textList.Add((ent, t.Position));
                    else if (ent is MText m) textList.Add((ent, m.Location));
                }

                if (textList.Count == 0) return;

                // Sap xep tu tren xuong duoi (Y giam dan)
                textList = textList.OrderByDescending(x => x.pos.Y).ToList();
                double alignX = ppr.Status == PromptStatus.OK ? ppr.Value.X : textList.First().pos.X;

                foreach (var item in textList)
                {
                    if (item.ent is DBText dt)
                    {
                        dt.Position = new Point3d(alignX, dt.Position.Y, dt.Position.Z);
                    }
                    else if (item.ent is MText mt)
                    {
                        mt.Location = new Point3d(alignX, mt.Location.Y, mt.Location.Z);
                    }
                }
                ed.WriteMessage($"\n[GTD] Da can giong doc {textList.Count} Text theo toa do X = {alignX:F2}");
            });
        }

        /// <summary>
        /// GTN : Gom text ngang - Căn gióng các đối tượng Text theo phương ngang
        /// </summary>
        [CommandMethod("GTN")]
        public void AlignTextHorizontal()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var filter = SelectionHelper.CreateTypeFilter("TEXT,MTEXT");
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChon cac Text/MText can gom ngang: " }, filter);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count <= 1)
            {
                ed.WriteMessage("\nCan chon it nhat 2 doi tuong Text.");
                return;
            }

            var ppo = new PromptPointOptions("\nChon diem gióng chuan (hoac Enter lay vi tri Text dau tien): ");
            var ppr = ed.GetPoint(ppo);

            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                var textList = new List<(Entity ent, Point3d pos)>();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForWrite);
                    if (ent is DBText t) textList.Add((ent, t.Position));
                    else if (ent is MText m) textList.Add((ent, m.Location));
                }

                if (textList.Count == 0) return;

                // Sap xep tu trai sang phai (X tang dan)
                textList = textList.OrderBy(x => x.pos.X).ToList();
                double alignY = ppr.Status == PromptStatus.OK ? ppr.Value.Y : textList.First().pos.Y;

                foreach (var item in textList)
                {
                    if (item.ent is DBText dt)
                    {
                        dt.Position = new Point3d(dt.Position.X, alignY, dt.Position.Z);
                    }
                    else if (item.ent is MText mt)
                    {
                        mt.Location = new Point3d(mt.Location.X, alignY, mt.Location.Z);
                    }
                }
                ed.WriteMessage($"\n[GTN] Da can giong ngang {textList.Count} Text theo toa do Y = {alignY:F2}");
            });
        }
    }
}
