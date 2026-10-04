using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.CadAccess;

namespace CadElectricalToolkit.Commands
{
    /// <summary>
    /// Các lệnh thống kê thiết bị & xuất dữ liệu Excel (TKDMTB, CAD2EXCEL)
    /// </summary>
    public class BomCommands
    {
        /// <summary>
        /// TKDMTB : Thống kê danh mục thiết bị (BOM - Bill of Materials)
        /// </summary>
        [CommandMethod("TKDMTB")]
        public void SummaryDeviceList()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            ed.WriteMessage("\n[TKDMTB] Dang quet danh muc thiet bi tren ban ve de tao bang BOM...");
        }

        /// <summary>
        /// CAD2EXCEL : Xuất thông tin Block Attribute và Text ra file CSV/Excel
        /// </summary>
        [CommandMethod("CAD2EXCEL")]
        public void ExportCadToExcel()
        {
            var ed = CadDatabaseHelper.ActiveEd;
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChon cac Text hoac Block Attribute muon xuat Excel: " });
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nChua chon doi tuong nao.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("EntityType,BlockName,Handle,Tag/Property,Value,PosX,PosY");

            int exportedCount = 0;
            CadDatabaseHelper.RunTransaction((tr, db) =>
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);

                    if (ent is DBText dbText)
                    {
                        sb.AppendLine($"Text,,{dbText.Handle},TextString,\"{dbText.TextString.Replace("\"", "\"\"")}\",{dbText.Position.X:F2},{dbText.Position.Y:F2}");
                        exportedCount++;
                    }
                    else if (ent is MText mText)
                    {
                        sb.AppendLine($"MText,,{mText.Handle},Contents,\"{mText.Text.Replace("\"", "\"\"")}\",{mText.Location.X:F2},{mText.Location.Y:F2}");
                        exportedCount++;
                    }
                    else if (ent is BlockReference blkRef)
                    {
                        string blkName = blkRef.GetEffectiveBlockName(tr);
                        foreach (ObjectId attId in blkRef.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                            {
                                sb.AppendLine($"BlockAttribute,{blkName},{blkRef.Handle},{attRef.Tag},\"{attRef.TextString.Replace("\"", "\"\"")}\",{attRef.Position.X:F2},{attRef.Position.Y:F2}");
                                exportedCount++;
                            }
                        }
                    }
                }
            });

            if (exportedCount == 0)
            {
                ed.WriteMessage("\nKhong tim thay du lieu Text hoac Attribute de xuat.");
                return;
            }

            // Mo hop thoai Save File CSV
            var sfd = new SaveFileDialog
            {
                Title = "Chon noi luu file Excel / CSV",
                Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"CadExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    ed.WriteMessage($"\n[CAD2EXCEL] Da xuat thanh cong {exportedCount} dong du lieu ra file: {sfd.FileName}");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[CAD2EXCEL] Loi khi ghi file: {ex.Message}");
                }
            }
        }
    }
}
