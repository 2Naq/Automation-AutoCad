using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Các phương thức hỗ trợ quét chọn và lọc đối tượng trong bản vẽ
    /// </summary>
    public static class SelectionHelper
    {
        /// <summary>
        /// Yêu cầu người dùng quét chọn đối tượng với bộ lọc kiểu đối tượng
        /// </summary>
        public static PromptSelectionResult SelectEntities(string promptMessage, SelectionFilter? filter = null)
        {
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = promptMessage
            };

            return filter != null 
                ? CadDatabaseHelper.ActiveEd.GetSelection(pso, filter) 
                : CadDatabaseHelper.ActiveEd.GetSelection(pso);
        }

        /// <summary>
        /// Tạo bộ lọc theo loại đối tượng (ví dụ: "DIMENSION", "INSERT", "TEXT,MTEXT")
        /// </summary>
        public static SelectionFilter CreateTypeFilter(string dxfType)
        {
            var typedValues = new[]
            {
                new TypedValue((int)DxfCode.Start, dxfType)
            };
            return new SelectionFilter(typedValues);
        }

        /// <summary>
        /// Quét tất cả các đối tượng kiểu T trong ModelSpace
        /// </summary>
        public static List<ObjectId> GetAllEntitiesOfType<T>(Database db, Transaction tr) where T : Entity
        {
            var result = new List<ObjectId>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var rxClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(T));
            foreach (ObjectId id in ms)
            {
                if (id.ObjectClass.IsDerivedFrom(rxClass))
                {
                    result.Add(id);
                }
            }
            return result;
        }
    }
}
