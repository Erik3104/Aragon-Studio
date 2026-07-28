using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.Vistas.FamiliaDeTipoAVista3D;

namespace AragonStudio.Commands.Vistas
{
    [Transaction(TransactionMode.Manual)]
    public class FamiliaDeTipoAVista3D : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                new FamiliaDeTipoAVista3DWindow(commandData.Application).ShowDialog();
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