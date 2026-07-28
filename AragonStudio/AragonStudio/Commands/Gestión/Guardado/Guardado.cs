using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.Gestión.Guardado;

namespace AragonStudio.Commands.Gestion.Guardado
{
    [Transaction(TransactionMode.ReadOnly)]
    public class Guardado : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            new GuardadoWindow(commandData.Application).ShowDialog();
            return Result.Succeeded;
        }
    }
}