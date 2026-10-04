using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Các phương thức tiện ích hỗ trợ truy cập AutoCAD Database, Transaction và Editor
    /// </summary>
    public static class CadDatabaseHelper
    {
        public static Document ActiveDoc => Application.DocumentManager.MdiActiveDocument;
        public static Database ActiveDb => ActiveDoc.Database;
        public static Editor ActiveEd => ActiveDoc.Editor;

        /// <summary>
        /// Ghi thông báo ra dòng lệnh AutoCAD (Command Line)
        /// </summary>
        public static void WriteMessage(string message)
        {
            if (ActiveDoc != null)
            {
                ActiveEd.WriteMessage(message);
            }
        }

        /// <summary>
        /// Mở Transaction an toàn trong Document hiện hành
        /// </summary>
        public static void RunTransaction(System.Action<Transaction, Database> action)
        {
            var doc = ActiveDoc;
            if (doc == null) return;

            using (var docLock = doc.LockDocument())
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    action(tr, doc.Database);
                    tr.Commit();
                }
            }
        }
    }
}
