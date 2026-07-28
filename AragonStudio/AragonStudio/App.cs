using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Reflection;
using AragonStudio.Resources.Icons;

namespace AragonStudio
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            string tabName = "Aragón Studio";
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            try { app.CreateRibbonTab(tabName); } catch { }

            RibbonPanel convertidorPanel = app.CreateRibbonPanel(tabName, "Convertidor");
            convertidorPanel.AddStackedItems(
                NewBtn("DwgARvt", "DWG a Revit", "AragonStudio.Commands.Convertidor.DwgARvt.DwgARvt", "DwgARvt.svg", assemblyPath),
                NewBtn("SkpARvt", "SketchUp a Revit", "AragonStudio.Commands.Convertidor.SkpARvt.SkpARvt", "SkpARvt.svg", assemblyPath)
            );

            RibbonPanel docPanel = app.CreateRibbonPanel(tabName, "Documentación");

            PulldownButtonData etiquetadoData = new PulldownButtonData("Etiquetado", "Etiquetado");
            PulldownButton etiquetadoGroup = docPanel.AddItem(etiquetadoData) as PulldownButton;
            etiquetadoGroup.LargeImage = SvgIconLoader.LoadSvg("Resources/Icons/SvgIcons/Etiquetado.svg", 32);
            etiquetadoGroup.AddPushButton(NewSmallBtn("EtiquetadoArq", "Arquitectónico", "AragonStudio.Commands.Documentacion.EtiquetadoArquitectonico.EtiquetadoArquitectonico", "EtiquetadoArquitectonico.svg", assemblyPath));
            etiquetadoGroup.AddPushButton(NewSmallBtn("EtiquetadoEst", "Estructural", "AragonStudio.Commands.Documentacion.EtiquetadoEstructural.EtiquetadoEstructural", "EtiquetadoEstructural.svg", assemblyPath));
            etiquetadoGroup.AddPushButton(NewSmallBtn("EtiquetadoMep", "MEP", "AragonStudio.Commands.Documentacion.EtiquetadoMep.EtiquetadoMep", "Mep.svg", assemblyPath));

            docPanel.AddStackedItems(
                NewBtn("AutoAcotado", "Auto Acotado", "AragonStudio.Commands.Documentacion.AutoAcotado", "Acotado.svg", assemblyPath),
                NewBtn("CorreccionOrtografica", "Ortografía", "AragonStudio.Commands.Documentacion.CorreccionOrtografica", "Ortografia.svg", assemblyPath),
                NewBtn("InformeGeneral", "Informe General", "AragonStudio.Commands.Documentacion.InformeGeneral", "Informe.svg", assemblyPath)
            );
            docPanel.AddItem(NewBtn("Traductor", "Traductor", "AragonStudio.Commands.Documentacion.Traductor", "Traductor.svg", assemblyPath));

            RibbonPanel gestionPanel = app.CreateRibbonPanel(tabName, "Gestión");
            gestionPanel.AddItem(NewBtn("CatalogoDeModelos", "Catálogo", "AragonStudio.Commands.Gestion.CatalogoDeModelos", "Catalogo.svg", assemblyPath));
            gestionPanel.AddItem(NewBtn("ClasificacionPorTipo", "Clasificación", "AragonStudio.Commands.Gestion.ClasificacionPorTipo.ClasificacionPorTipo", "Clasificacion.svg", assemblyPath));

            PulldownButtonData guardadoData = new PulldownButtonData("Guardado", "Guardado");
            PulldownButton guardadoGroup = gestionPanel.AddItem(guardadoData) as PulldownButton;
            guardadoGroup.LargeImage = SvgIconLoader.LoadSvg("Resources/Icons/SvgIcons/Guardado.svg", 32);
            guardadoGroup.AddPushButton(NewSmallBtn("GuardConfig", "Configurar", "AragonStudio.Commands.Gestion.Guardado.Guardado", "Guardado.svg", assemblyPath));

            RibbonPanel modeladoPanel = app.CreateRibbonPanel(tabName, "Modelado");
            modeladoPanel.AddStackedItems(
                NewSmallBtn("GeneradorDeAcabados", "Acabados", "AragonStudio.Commands.Modelado.GeneradorDeAcabados", "Acabados.svg", assemblyPath),
                NewSmallBtn("GeneradorDeAceros", "Aceros", "AragonStudio.Commands.Modelado.GeneradorDeAceros", "Aceros.svg", assemblyPath),
                NewSmallBtn("GeneradorDeVegetacion", "Vegetación", "AragonStudio.Commands.Modelado.GeneradorDeVegetacion", "Vegetacion.svg", assemblyPath)
            );
            modeladoPanel.AddStackedItems(
                NewBtn("ModeladoArquitectonico", "Modelado Arquitectónico", "AragonStudio.Commands.Modelado.ModeladoArquitectonico", "Arquitectonico.svg", assemblyPath),
                NewBtn("ModeladoEstructural", "Modelado Estructural", "AragonStudio.Commands.Modelado.ModeladoEstructural", "Estructural.svg", assemblyPath)
            );

            RibbonPanel vistasPanel = app.CreateRibbonPanel(tabName, "Vistas");
            vistasPanel.AddStackedItems(
                NewBtn("FamiliaDeTipoAVista3D", "Detalle de Familias", "AragonStudio.Commands.Vistas.FamiliaDeTipoAVista3D", "Vista3D.svg", assemblyPath),
                NewBtn("GeneradorDeHojasPorVista", "Generador de Planos", "AragonStudio.Commands.Vistas.GeneradorDeHojasPorVista", "Hojas.svg", assemblyPath)
            );
            vistasPanel.AddStackedItems(
                NewBtn("PlanosPorHabitacion", "Planos por Espacio", "AragonStudio.Commands.Vistas.PlanosPorHabitacion", "Planos.svg", assemblyPath),
                NewBtn("SeccionesPorHabitacion", "Secciones por Espacio", "AragonStudio.Commands.Vistas.SeccionesPorHabitacion", "Secciones.svg", assemblyPath)
            );

            RibbonPanel helpPanel = app.CreateRibbonPanel(tabName, "Ayuda");
            helpPanel.AddItem(NewBtn("Help", "Aragón Studio", "AragonStudio.Commands.Help.Help", "Help.svg", assemblyPath));

            AragonStudio.Services.Guardado.RecordatorioService.Inicializar(app);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            AragonStudio.Services.Guardado.RecordatorioService.Detener();
            return Result.Succeeded;
        }

        private PushButtonData NewBtn(string name, string text, string className, string iconFile, string assemblyPath)
        {
            return new PushButtonData(name, text, assemblyPath, className)
            {
                LargeImage = SvgIconLoader.LoadSvg($"Resources/Icons/SvgIcons/{iconFile}", 32),
                Image = SvgIconLoader.LoadSvg($"Resources/Icons/SvgIcons/{iconFile}", 16)
            };
        }

        private PushButtonData NewSmallBtn(string name, string text, string className, string iconFile, string assemblyPath)
        {
            return new PushButtonData(name, text, assemblyPath, className)
            {
                LargeImage = SvgIconLoader.LoadSvg($"Resources/Icons/SvgIcons/{iconFile}", 24),
                Image = SvgIconLoader.LoadSvg($"Resources/Icons/SvgIcons/{iconFile}", 12)
            };
        }
    }
}