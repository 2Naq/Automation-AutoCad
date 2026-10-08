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
        public static Document? ActiveDoc
        {
            get
            {
                try
                {
                    return Application.DocumentManager?.MdiActiveDocument;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static Database ActiveDb => ActiveDoc?.Database!;
        public static Editor ActiveEd => ActiveDoc?.Editor!;

        /// <summary>
        /// Kiểm tra plugin có đang chạy trong môi trường AutoCAD thực tế hay không
        /// </summary>
        public static bool IsInCad => ActiveDoc != null;

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
