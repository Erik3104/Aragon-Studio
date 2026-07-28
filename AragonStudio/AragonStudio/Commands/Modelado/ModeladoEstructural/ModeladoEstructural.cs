using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AragonStudio.UI;
using System.Windows.Interop;
using System.Diagnostics;

namespace AragonStudio.Commands.Modelado.ModeladoEstructural
{
    /// <summary>
    /// Comando externo de Revit para abrir la herramienta de modelado estructural.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ModeladoEstructural : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Crear la instancia de la ventana WPF
            var window = new ModeladoEstructuralWindow();

            // Establecer la ventana principal de Revit como owner
            var helper = new WindowInteropHelper(window)
            {
                Owner = Process.GetCurrentProcess().MainWindowHandle
            };

            // Mostrar la ventana como modal
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
