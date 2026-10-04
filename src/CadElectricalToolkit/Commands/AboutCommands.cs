using Autodesk.AutoCAD.Runtime;
using CadElectricalToolkit.UI.Views;
using cadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadElectricalToolkit.Commands
{
    public class AboutCommands
    {
        [CommandMethod("ELECINFO")]
        [CommandMethod("TT")]
        public void ShowToolInfo()
        {
            using (var form = new ToolInfoForm())
            {
                cadApp.ShowModalDialog(form);
            }
        }
    }
}
