using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Linq;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class GuardadoModule : IAragonModule
    {
        public string Name => "Guardado";

        public void Register(UIControlledApplication app)
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string tabName = "Aragón Studio";

                try { app.CreateRibbonTab(tabName); } catch { }

                // Buscar el panel de Gestión o crearlo
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

                // Crear el botón de Guardado (Pulldown)
                PulldownButtonData guardadoData = new PulldownButtonData("Guardado", "Guardado");
                guardadoData.LargeImage = SvgIconLoader.LoadSvg("Guardado.svg", 32);
                guardadoData.Image = SvgIconLoader.LoadSvg("Guardado.svg", 16);

                var guardadoGroup = panel.AddItem(guardadoData) as PulldownButton;
                if (guardadoGroup != null)
                {
                    PushButtonData btnConfig = new PushButtonData(
                        "GuardConfig",
                        "Configurar",
                        assemblyPath,
                        "AragonStudio.Commands.Gestion.Guardado.Guardado"
                    );
                    btnConfig.LargeImage = SvgIconLoader.LoadSvg("Guardado.svg", 24);
                    btnConfig.Image = SvgIconLoader.LoadSvg("Guardado.svg", 12);
                    guardadoGroup.AddPushButton(btnConfig);
                }

                // Inicializar el servicio de recordatorio
                try
                {
                    Services.Guardado.RecordatorioService.Inicializar(app);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error inicializando RecordatorioService: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error registrando módulo Guardado:\n{ex.Message}");
            }
        }
    }
}