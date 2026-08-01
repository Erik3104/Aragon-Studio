using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.Documentacion.EtiquetadoArquitectonico;

namespace AragonStudio.Etiquetado.Commands.Documentación.EtiquetadoArquitectonico.cs
{
    [Transaction(TransactionMode.Manual)]
    public class EtiquetadoArquitectonico : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                new EtiquetadoArquitectonicoWindow(commandData.Application).ShowDialog();
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