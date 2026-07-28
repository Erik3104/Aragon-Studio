using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AragonStudio.UI.AutoAcotado
{
    public partial class AutoAcotadoWindow : Window
    {
        private readonly Document _doc;

        public AutoAcotadoWindow(ExternalCommandData commandData)
        {
            InitializeComponent();

            _doc = commandData.Application
                              .ActiveUIDocument
                              .Document;

            CargarVistas();
            CargarTiposDeCota();
        }

        // =========================================
        // VISTAS EN ÁRBOL
        // =========================================
        private void CargarVistas()
        {
            treeVistas.Items.Clear();

            CrearGrupoVistas("Plantas", ViewType.FloorPlan);
            CrearGrupoVistas("Alzados", ViewType.Elevation);
            CrearGrupoVistas("Secciones", ViewType.Section);
        }

        private void CrearGrupoVistas(string nombre, ViewType tipo)
        {
            IList<View> vistas = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .WhereElementIsNotElementType()
                .Cast<View>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == tipo)
                .OrderBy(v => v.Name)
                .ToList();

            if (vistas.Count == 0) return;

            TreeViewItem grupo = new TreeViewItem
            {
                Header = nombre,
                IsExpanded = true
            };

            foreach (View vista in vistas)
            {
                CheckBox chk = new CheckBox
                {
                    Content = vista.Name,
                    Tag = vista
                };

                grupo.Items.Add(chk);
            }

            treeVistas.Items.Add(grupo);
        }

        // =========================================
        // TIPOS DE COTA DEL PROYECTO
        // =========================================
        private void CargarTiposDeCota()
        {
            IList<DimensionType> tipos = new FilteredElementCollector(_doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .OrderBy(t => t.Name)
                .ToList();

            cmbTiposCota.ItemsSource = tipos;
            cmbTiposCota.DisplayMemberPath = "Name";
        }

        // =========================================
        // OBTENER VISTAS SELECCIONADAS
        // =========================================
        public List<View> ObtenerVistasSeleccionadas()
        {
            List<View> resultado = new List<View>();

            foreach (TreeViewItem grupo in treeVistas.Items)
            {
                foreach (object item in grupo.Items)
                {
                    if (item is CheckBox chk &&
                        chk.IsChecked == true &&
                        chk.Tag is View vista)
                    {
                        resultado.Add(vista);
                    }
                }
            }

            return resultado;
        }
    }
}
