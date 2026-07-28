using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.AutoAcotado;

namespace AragonStudio.Commands.Documentacion
{
    [Transaction(TransactionMode.Manual)]
    public class AutoAcotado : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            AutoAcotadoWindow window =
                new AutoAcotadoWindow(commandData);

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
