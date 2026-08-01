using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using AragonStudio.UI.VisibilityManager;
using System;

namespace AragonStudio.Commands.VisibilityManager
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class VisibilityManagerCommand : IExternalCommand
    {
        private static VisibilityManagerWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (commandData.Application.ActiveUIDocument == null)
            {
                TaskDialog.Show("Aviso", "Debe abrir un proyecto de Revit.");
                return Result.Cancelled;
            }

            try
            {
                if (_window != null && _window.IsVisible)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                _window = new VisibilityManagerWindow(commandData.Application);
                _window.Closed += (s, e) => _window = null;
                _window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"❌ Error en VisibilityManager:\n{ex.Message}");
                _window = null;
                return Result.Failed;
            }
        }
    }
}