using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;
using CadElectricalToolkit.UI;
using CadElectricalToolkit.UI.Views;

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
        /// DSTT : Đánh số thứ tự tăng dần tự động (Giao diện WPF hỗ trợ Block Attribute & Text)
        /// </summary>
        [CommandMethod("DSTT")]
        public void AutoNumbering()
        {
            var window = new AutoNumberingWindow(openBlockAttrTab: true);
            window.ShowModal();
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
