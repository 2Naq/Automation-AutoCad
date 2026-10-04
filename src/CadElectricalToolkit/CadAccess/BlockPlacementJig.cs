using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CadElectricalToolkit.CadAccess
{
    /// <summary>
    /// Jig kéo thả định vị BlockReference trực quan theo con trỏ chuột
    /// Cho phép người dùng rê chuột xem trước vị trí block và nhấp chuột để chọn điểm đặt
    /// </summary>
    public class BlockPlacementJig : EntityJig
    {
        private Point3d _position;
        private readonly string _promptMsg;

        /// <summary>
        /// Tọa độ điểm đặt người dùng đã chọn
        /// </summary>
        public Point3d Position => _position;

        public BlockPlacementJig(BlockReference blkRef, Point3d initialPosition, string promptMsg) 
            : base(blkRef)
        {
            _position = initialPosition;
            blkRef.Position = initialPosition;
            _promptMsg = promptMsg;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var jpo = new JigPromptPointOptions(_promptMsg)
            {
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                   UserInputControls.NoNegativeResponseAccepted
            };

            var res = prompts.AcquirePoint(jpo);
            if (res.Status == PromptStatus.OK)
            {
                if (_position.DistanceTo(res.Value) < 0.0001)
                    return SamplerStatus.NoChange;

                _position = res.Value;
                return SamplerStatus.OK;
            }

            return SamplerStatus.Cancel;
        }

        protected override bool Update()
        {
            ((BlockReference)Entity).Position = _position;
            return true;
        }
    }
}
