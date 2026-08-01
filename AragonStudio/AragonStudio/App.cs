using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class App : IExternalApplication
    {
        private static List<IAragonModule> _loadedModules = new List<IAragonModule>();
        private static string _tabName = "Aragón Studio";

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                try { app.CreateRibbonTab(_tabName); } catch { }

                // Cargar módulos dinámicamente
                LoadModules(app);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error en OnStartup: {ex.Message}");
                return Result.Failed;
            }
        }

        private void LoadModules(UIControlledApplication app)
        {
            string modulesPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(modulesPath)) return;

            var dllFiles = Directory.GetFiles(modulesPath, "AragonStudio.*.dll")
                .Where(f => !f.EndsWith("AragonStudio.dll"))
                .ToList();

            if (dllFiles.Count == 0)
            {
                LoadBuiltInModules(app);
                return;
            }

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var moduleTypes = assembly.GetTypes()
                        .Where(t => typeof(IAragonModule).IsAssignableFrom(t) &&
                                    t.IsClass && !t.IsAbstract);

                    foreach (var moduleType in moduleTypes)
                    {
                        try
                        {
                            var module = (IAragonModule)Activator.CreateInstance(moduleType);
                            module.Register(app);
                            _loadedModules.Add(module);
                            System.Diagnostics.Debug.WriteLine($"Módulo cargado: {module.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error registrando módulo {moduleType.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cargando módulo {Path.GetFileName(dllPath)}: {ex.Message}");
                }
            }
        }

        private void LoadBuiltInModules(UIControlledApplication app)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // =========================================================================
            // PANEL: DOCUMENTACIÓN - Etiquetados
            // =========================================================================
            RibbonPanel docPanel = app.CreateRibbonPanel(_tabName, "Documentación");

            PulldownButtonData etiquetadoData = new PulldownButtonData("Etiquetado", "Etiquetado");
            PulldownButton etiquetadoGroup = docPanel.AddItem(etiquetadoData) as PulldownButton;
            if (etiquetadoGroup != null)
            {
                etiquetadoGroup.LargeImage = SvgIconLoader.LoadSvg("Etiquetado.svg", 32);
                etiquetadoGroup.AddPushButton(CreateSmallButton("EtiquetadoArq", "Arquitectónico",
                    "AragonStudio.Commands.Documentacion.EtiquetadoArquitectonico",
                    "EtiquetadoArquitectonico.svg", assemblyPath));
                etiquetadoGroup.AddPushButton(CreateSmallButton("EtiquetadoEst", "Estructural",
                    "AragonStudio.Commands.Documentacion.EtiquetadoEstructural",
                    "EtiquetadoEstructural.svg", assemblyPath));
                etiquetadoGroup.AddPushButton(CreateSmallButton("EtiquetadoMep", "MEP",
                    "AragonStudio.Commands.Documentacion.EtiquetadoMep",
                    "Mep.svg", assemblyPath));
            }

            // =========================================================================
            // PANEL: GESTIÓN - Clasificación y Guardado
            // =========================================================================
            RibbonPanel gestionPanel = app.CreateRibbonPanel(_tabName, "Gestión");

            gestionPanel.AddItem(CreateButton("ClasificacionPorTipo", "Clasificación",
                "AragonStudio.Commands.Gestion.ClasificacionPorTipo.ClasificacionPorTipo",
                "Clasificacion.svg", assemblyPath));

            PulldownButtonData guardadoData = new PulldownButtonData("Guardado", "Guardado");
            PulldownButton guardadoGroup = gestionPanel.AddItem(guardadoData) as PulldownButton;
            if (guardadoGroup != null)
            {
                guardadoGroup.LargeImage = SvgIconLoader.LoadSvg("Guardado.svg", 32);
                guardadoGroup.AddPushButton(CreateSmallButton("GuardConfig", "Configurar",
                    "AragonStudio.Commands.Gestion.Guardado.Guardado",
                    "Guardado.svg", assemblyPath));
            }

            // =========================================================================
            // PANEL: MODELADO - Acabados
            // =========================================================================
            RibbonPanel modeladoPanel = app.CreateRibbonPanel(_tabName, "Modelado");
            modeladoPanel.AddItem(CreateSmallButton("GeneradorDeAcabados", "Acabados",
                "AragonStudio.Commands.Modelado.GeneradorDeAcabados",
                "Acabados.svg", assemblyPath));

            // =========================================================================
            // PANEL: CONVERTIDOR - DWG a Revit
            // =========================================================================
            RibbonPanel convertidorPanel = app.CreateRibbonPanel(_tabName, "Convertidor");
            convertidorPanel.AddItem(CreateButton("DwgARvt", "DWG a Revit",
                "AragonStudio.Commands.Convertidor.DwgARvt.DwgARvt",
                "DwgARvt.svg", assemblyPath));

            // =========================================================================
            // PANEL: VISTAS - Detalle de Familias y Visibilidad
            // =========================================================================
            RibbonPanel vistasPanel = app.CreateRibbonPanel(_tabName, "Vistas");

            vistasPanel.AddItem(CreateButton("FamiliaDeTipoAVista3D", "Detalle de Familias",
                "AragonStudio.Commands.Vistas.FamiliaDeTipoAVista3D",
                "Vista3D.svg", assemblyPath));

            vistasPanel.AddItem(CreateButton("VisibilityManager", "Visibilidad",
                "AragonStudio.Commands.Vistas.VisibilityManager.VisibilityManagerCommand",
                "VisibilityManager.svg", assemblyPath));

            // =========================================================================
            // PANEL: AYUDA - WhatsApp
            // =========================================================================
            RibbonPanel helpPanel = app.CreateRibbonPanel(_tabName, "Ayuda");
            helpPanel.AddItem(CreateButton("Help", "Aragón Studio",
                "AragonStudio.Commands.Help.HelpCommand",
                "Help.svg", assemblyPath));
        }

        private PushButtonData CreateButton(string name, string text, string className, string iconFile, string assemblyPath)
        {
            return new PushButtonData(name, text, assemblyPath, className)
            {
                LargeImage = SvgIconLoader.LoadSvg(iconFile, 32),
                Image = SvgIconLoader.LoadSvg(iconFile, 16)
            };
        }

        private PushButtonData CreateSmallButton(string name, string text, string className, string iconFile, string assemblyPath)
        {
            return new PushButtonData(name, text, assemblyPath, className)
            {
                LargeImage = SvgIconLoader.LoadSvg(iconFile, 24),
                Image = SvgIconLoader.LoadSvg(iconFile, 12)
            };
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            foreach (var module in _loadedModules)
            {
                try { /* Limpieza */ } catch { }
            }
            _loadedModules.Clear();
            return Result.Succeeded;
        }
    }
}