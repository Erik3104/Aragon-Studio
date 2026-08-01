using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.Documentacion.EtiquetadoMep;

namespace AragonStudio.Etiquetado.Commands.Documentación.EtiquetadoMep
{
    [Transaction(TransactionMode.Manual)]
    public class EtiquetadoMep : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                new EtiquetadoMepWindow(commandData.Application).ShowDialog();
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