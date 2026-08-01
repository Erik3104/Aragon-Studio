using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using AragonStudio.UI.Documentacion.EtiquetadoEstructural;

namespace AragonStudio.Etiquetado.Commands.Documentación.EtiquetadoEstructural
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class EtiquetadoEstructural : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("Error", "No hay ningún proyecto abierto en Revit.");
                return Result.Failed;
            }
            Document doc = uidoc.Document;
            if (doc.IsReadOnly)
            {
                TaskDialog.Show("Error", "El documento está en modo de solo lectura. No se pueden realizar cambios.");
                return Result.Failed;
            }
            EtiquetadoEstructuralWindow window = new EtiquetadoEstructuralWindow(uidoc, doc, uiapp);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}