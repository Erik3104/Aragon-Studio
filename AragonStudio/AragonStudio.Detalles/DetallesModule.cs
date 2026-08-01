using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Linq;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class DetallesModule : IAragonModule
    {
        public string Name => "Detalles";

        public void Register(UIControlledApplication app)
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tabName = "Aragón Studio";

                try { app.CreateRibbonTab(tabName); } catch { }

                // Buscar el panel de Vistas o crearlo
                RibbonPanel panel = null;
                try
                {
                    panel = app.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name == "Vistas");
                }
                catch { }

                if (panel == null)
                {
                    panel = app.CreateRibbonPanel(tabName, "Vistas");
                }

                // Crear el botón de Detalle de Familias
                PushButtonData btnDetalle = new PushButtonData(
                    "FamiliaDeTipoAVista3D",
                    "Detalle de Familias",
                    assemblyPath,
                    "AragonStudio.Commands.Vistas.FamiliaDeTipoAVista3D"
                );

                btnDetalle.LargeImage = SvgIconLoader.LoadSvg("Vista3D.svg", 32);
                btnDetalle.Image = SvgIconLoader.LoadSvg("Vista3D.svg", 16);

                panel.AddItem(btnDetalle);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error registrando módulo Detalles:\n{ex.Message}");
            }
        }
    }
}