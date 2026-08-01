using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AragonStudio.UI;
using System.Diagnostics;
using System.Windows.Interop;

namespace AragonStudio.Commands.Help
{
    [Transaction(TransactionMode.Manual)]
    public class HelpCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new HelpWindow();

            var helper = new WindowInteropHelper(window)
            {
                Owner = Process.GetCurrentProcess().MainWindowHandle
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}