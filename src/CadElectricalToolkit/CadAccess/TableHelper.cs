using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Tiện ích khởi tạo và định dạng bảng thống kê (AutoCAD Table) trực tiếp trên bản vẽ
    /// </summary>
    public static class TableHelper
    {
        public static ObjectId InsertSummaryTable(
            Database db,
            Transaction tr,
            Point3d position,
            string title,
            string[] headers,
            List<string[]> rows,
            double rowHeight = 7.0,
            double colWidth = 35.0)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            int totalRows = rows.Count + 2; // Row 0: Title, Row 1: Header, Row 2+: Data
            int totalCols = headers.Length;

            var table = new Table();
            table.SetDatabaseDefaults(db);
            table.TableStyle = db.Tablestyle;
            table.SetSize(totalRows, totalCols);
            table.Position = position;

            // Set kich thuoc cot va hang
            for (int r = 0; r < totalRows; r++)
            {
                table.Rows[r].Height = rowHeight;
            }
            for (int c = 0; c < totalCols; c++)
            {
                table.Columns[c].Width = colWidth;
            }

            // Row 0: Tieu de (Title)
            table.Cells[0, 0].TextString = title;
            table.Cells[0, 0].TextHeight = 4.0;
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.Cells[0, 0].ContentColor = Color.FromColorIndex(ColorMethod.ByAci, 2); // Yellow

            // Row 1: Tieu de cac cot (Headers)
            for (int c = 0; c < totalCols; c++)
            {
                table.Cells[1, c].TextString = headers[c];
                table.Cells[1, c].TextHeight = 2.8;
                table.Cells[1, c].Alignment = CellAlignment.MiddleCenter;
                table.Cells[1, c].ContentColor = Color.FromColorIndex(ColorMethod.ByAci, 4); // Cyan
            }

            // Rows 2+: Du lieu (Data)
            for (int r = 0; r < rows.Count; r++)
            {
                var rowData = rows[r];
                for (int c = 0; c < totalCols && c < rowData.Length; c++)
                {
                    table.Cells[r + 2, c].TextString = rowData[c];
                    table.Cells[r + 2, c].TextHeight = 2.5;
                    table.Cells[r + 2, c].Alignment = CellAlignment.MiddleCenter;
                }
            }

            table.GenerateLayout();

            var tableId = ms.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            return tableId;
        }
    }
}
