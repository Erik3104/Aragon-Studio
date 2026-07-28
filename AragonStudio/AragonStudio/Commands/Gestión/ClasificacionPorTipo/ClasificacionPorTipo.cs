using AragonStudio.UI.Gestión.ClasificacionPorTipo;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace AragonStudio.Commands.Gestion.ClasificacionPorTipo
{
    [Transaction(TransactionMode.Manual)]
    public class ClasificacionPorTipo : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                var configWin = new ConfiguracionWindow(uiApp);
                if (configWin.ShowDialog() != true) return Result.Cancelled;
                var config = configWin.Config;

                TaskDialog.Show("Selección de elementos base",
                    "A continuación, selecciona exactamente 2 elementos (instancias) que servirán como base de la secuencia.\n\n" +
                    "Recibirán los números 1 y 2 respectivamente.\n\n" +
                    "Haz clic en Aceptar y luego selecciona los elementos en el modelo.");

                IList<Autodesk.Revit.DB.Reference> selectedRefs = null;
                try
                {
                    selectedRefs = uiApp.ActiveUIDocument.Selection.PickObjects(ObjectType.Element, "Selecciona exactamente 2 elementos base");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (selectedRefs.Count != 2)
                {
                    TaskDialog.Show("Error", "Debes seleccionar exactamente 2 elementos.");
                    return Result.Failed;
                }

                var doc = uiApp.ActiveUIDocument.Document;
                var elem1 = doc.GetElement(selectedRefs[0].ElementId);
                var elem2 = doc.GetElement(selectedRefs[1].ElementId);
                var type1Id = elem1.GetTypeId();
                var type2Id = elem2.GetTypeId();

                if (type1Id == null || type2Id == null)
                {
                    TaskDialog.Show("Error", "Los elementos seleccionados no tienen tipo de familia.");
                    return Result.Failed;
                }

                var execWin = new EjecucionWindow(uiApp, config, type1Id, type2Id);
                execWin.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}