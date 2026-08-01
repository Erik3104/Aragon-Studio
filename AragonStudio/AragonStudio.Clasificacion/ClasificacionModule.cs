using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Linq;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class ClasificacionModule : IAragonModule
    {
        public string Name => "Clasificación";

        public void Register(UIControlledApplication app)
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tabName = "Aragón Studio";

                try { app.CreateRibbonTab(tabName); } catch { }

                RibbonPanel panel = null;
                try
                {
                    panel = app.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name == "Gestión");
                }
                catch { }

                if (panel == null)
                {
                    panel = app.CreateRibbonPanel(tabName, "Gestión");
                }

                PushButtonData btnClasificacion = new PushButtonData(
                    "ClasificacionPorTipo",
                    "Clasificación",
                    assemblyPath,
                    "AragonStudio.Commands.Gestion.ClasificacionPorTipo.ClasificacionPorTipo"
                );

                btnClasificacion.LargeImage = SvgIconLoader.LoadSvg("Clasificacion.svg", 32);
                btnClasificacion.Image = SvgIconLoader.LoadSvg("Clasificacion.svg", 16);

                panel.AddItem(btnClasificacion);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error registrando módulo Clasificación:\n{ex.Message}");
            }
        }
    }
}