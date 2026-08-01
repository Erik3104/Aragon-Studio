using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Linq;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class HelpModule : IAragonModule
    {
        public string Name => "Ayuda";

        public void Register(UIControlledApplication app)
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tabName = "Aragón Studio";

                try { app.CreateRibbonTab(tabName); } catch { }

                // Buscar el panel de Ayuda o crearlo
                RibbonPanel panel = null;
                try
                {
                    panel = app.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name == "Ayuda");
                }
                catch { }

                if (panel == null)
                {
                    panel = app.CreateRibbonPanel(tabName, "Ayuda");
                }

                // Crear el botón de Ayuda (Gmail)
                PushButtonData btnHelp = new PushButtonData(
                    "Help",
                    "Aragón Studio",
                    assemblyPath,
                    "AragonStudio.Commands.Help.HelpCommand"
                );

                btnHelp.LargeImage = SvgIconLoader.LoadSvg("Help.svg", 32);
                btnHelp.Image = SvgIconLoader.LoadSvg("Help.svg", 16);

                panel.AddItem(btnHelp);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error registrando módulo Ayuda:\n{ex.Message}");
            }
        }
    }
}