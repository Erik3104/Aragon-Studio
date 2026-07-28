using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AragonStudio.UI.Vistas.FamiliaDeTipoAVista3D
{
    public partial class FamiliaDeTipoAVista3DWindow : Window
    {
        private UIApplication _uiApp;
        private Document _doc;
        private List<Category> _categorias;
        private List<TipoConSeleccion> _tiposDisponibles;
        private ElementId _plantillaIdSeleccionada = ElementId.InvalidElementId;
        private ElementId _viewFamilyTypeId;

        public class TipoConSeleccion
        {
            public ElementType Tipo { get; set; }
            public string ValorParametro { get; set; }
            public string DisplayName { get; set; }
            public bool IsSelected { get; set; }
            public ElementId ElementoId { get; set; }
        }

        public FamiliaDeTipoAVista3DWindow(UIApplication uiApp)
        {
            InitializeComponent();
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
            }
            catch { this.Icon = null; }

            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;
            CargarCategorias();
            CargarPlantillas();
            ObtenerViewFamilyType3D();
        }

        private void ObtenerViewFamilyType3D()
        {
            var viewFamilyType = new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);
            if (viewFamilyType == null)
            {
                MessageBox.Show("No se encontró un tipo de vista 3D en el proyecto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
            _viewFamilyTypeId = viewFamilyType.Id;
        }

        private void CargarCategorias()
        {
            _categorias = _doc.Settings.Categories.Cast<Category>()
                .Where(c => c.CategoryType == CategoryType.Model && !c.IsTagCategory && c.AllowsBoundParameters)
                .OrderBy(c => c.Name).ToList();
            cmbCategorias.ItemsSource = _categorias.Select(c => c.Name).ToList();
        }

        private void CargarPlantillas()
        {
            var plantillas = new FilteredElementCollector(_doc).OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate && v.ViewType == ViewType.ThreeD)
                .OrderBy(v => v.Name)
                .ToList();
            plantillas.Insert(0, null);
            cmbPlantillas.ItemsSource = plantillas;
            cmbPlantillas.DisplayMemberPath = "Name";
            cmbPlantillas.SelectedIndex = 0;
        }

        private void cmbCategorias_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbCategorias.SelectedIndex >= 0)
            {
                CargarParametros();
                CargarTipos();
            }
        }

        private void CargarParametros()
        {
            var catName = cmbCategorias.SelectedItem.ToString();
            var category = _categorias.First(c => c.Name == catName);
            var collector = new FilteredElementCollector(_doc).OfCategoryId(category.Id).WhereElementIsElementType();
            var firstType = collector.FirstOrDefault() as ElementType;
            if (firstType != null)
            {
                var parameters = firstType.Parameters.Cast<Parameter>()
                    .Where(p => p.StorageType == StorageType.String && !p.IsReadOnly)
                    .Select(p => p.Definition.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                parameters.Insert(0, "Ninguno");
                cmbParametro.ItemsSource = parameters;
                cmbParametro.SelectedIndex = 0;
            }
            else
            {
                cmbParametro.ItemsSource = null;
                cmbParametro.SelectedIndex = -1;
            }
        }

        private void cmbParametro_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbCategorias.SelectedIndex >= 0)
            {
                CargarTipos();
            }
        }

        private void CargarTipos()
        {
            if (cmbCategorias.SelectedIndex < 0)
            {
                lstTipos.ItemsSource = null;
                btnGenerar.IsEnabled = false;
                return;
            }

            var catName = cmbCategorias.SelectedItem.ToString();
            var category = _categorias.First(c => c.Name == catName);
            string paramName = (cmbParametro.SelectedIndex > 0) ? cmbParametro.SelectedItem.ToString() : null;
            bool usarParametro = !string.IsNullOrEmpty(paramName) && paramName != "Ninguno";

            var elementos = new FilteredElementCollector(_doc)
                .OfCategoryId(category.Id)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .ToList();

            if (usarParametro)
            {
                var dict = new Dictionary<string, Element>();
                foreach (var elem in elementos)
                {
                    var tipo = _doc.GetElement(elem.GetTypeId()) as ElementType;
                    if (tipo == null) continue;
                    var param = tipo.LookupParameter(paramName);
                    if (param != null && param.HasValue && !string.IsNullOrEmpty(param.AsString()))
                    {
                        string valor = param.AsString();
                        if (!dict.ContainsKey(valor))
                            dict[valor] = elem;
                    }
                }
                _tiposDisponibles = dict.Select(kvp => new TipoConSeleccion
                {
                    ValorParametro = kvp.Key,
                    DisplayName = $"{kvp.Key} - {_doc.GetElement(kvp.Value.GetTypeId())?.Name}",
                    IsSelected = false,
                    ElementoId = kvp.Value.Id
                }).OrderBy(t => t.DisplayName).ToList();
            }
            else
            {
                var tipoPorElemento = new Dictionary<ElementId, Element>();
                foreach (var elem in elementos)
                {
                    var typeId = elem.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId && !tipoPorElemento.ContainsKey(typeId))
                    {
                        tipoPorElemento[typeId] = elem;
                    }
                }
                _tiposDisponibles = tipoPorElemento.Select(kvp => new TipoConSeleccion
                {
                    ValorParametro = null,
                    DisplayName = _doc.GetElement(kvp.Key)?.Name,
                    IsSelected = false,
                    ElementoId = kvp.Value.Id
                }).OrderBy(t => t.DisplayName).ToList();
            }

            lstTipos.ItemsSource = _tiposDisponibles;
            btnGenerar.IsEnabled = _tiposDisponibles.Any();
            if (!_tiposDisponibles.Any())
            {
                txtResultado.Text = "No hay tipos de familia con instancias en el modelo para esta categoría.";
            }
        }

        private void txtFiltro_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtFiltro.Text == "Filtrar tipos...")
                txtFiltro.Text = "";
        }

        private void txtFiltro_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFiltro.Text))
                txtFiltro.Text = "Filtrar tipos...";
            else
                FiltrarLista();
        }

        private void FiltrarLista()
        {
            if (_tiposDisponibles == null) return;
            string filtro = txtFiltro.Text.ToLower();
            if (string.IsNullOrEmpty(filtro) || filtro == "filtrar tipos...")
            {
                lstTipos.ItemsSource = _tiposDisponibles;
                return;
            }
            var filtrados = _tiposDisponibles.Where(t => t.DisplayName.ToLower().Contains(filtro)).ToList();
            lstTipos.ItemsSource = filtrados;
        }

        private void SeleccionarTodos_Click(object sender, RoutedEventArgs e)
        {
            if (lstTipos.ItemsSource is IEnumerable<TipoConSeleccion> items)
            {
                foreach (var item in items)
                    item.IsSelected = true;
                lstTipos.Items.Refresh();
            }
        }

        private void LimpiarSeleccion_Click(object sender, RoutedEventArgs e)
        {
            if (lstTipos.ItemsSource is IEnumerable<TipoConSeleccion> items)
            {
                foreach (var item in items)
                    item.IsSelected = false;
                lstTipos.Items.Refresh();
            }
        }

        private void btnGenerar_Click(object sender, RoutedEventArgs e)
        {
            if (_tiposDisponibles == null) return;
            var seleccionados = _tiposDisponibles.Where(t => t.IsSelected).ToList();
            if (seleccionados.Count == 0)
            {
                MessageBox.Show("Selecciona al menos un tipo de familia.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _plantillaIdSeleccionada = (cmbPlantillas.SelectedItem as View)?.Id ?? ElementId.InvalidElementId;

            btnGenerar.IsEnabled = false;
            progressBar.Visibility = System.Windows.Visibility.Visible;
            progressBar.Maximum = seleccionados.Count;
            progressBar.Value = 0;
            txtResultado.Text = "Generando vistas...";

            int creadas = 0;
            try
            {
                using (var t = new Transaction(_doc, "Generar vistas 3D por tipo"))
                {
                    t.Start();
                    int current = 0;
                    var level = new FilteredElementCollector(_doc).OfClass(typeof(Level)).FirstOrDefault() as Level;
                    if (level == null) throw new Exception("No hay niveles en el proyecto.");

                    foreach (var item in seleccionados)
                    {
                        current++;
                        progressBar.Value = current;
                        txtResultado.Text = $"Procesando {current} de {seleccionados.Count}: {item.DisplayName}";

                        Element elemento = _doc.GetElement(item.ElementoId);
                        if (elemento == null) continue;

                        string baseName = !string.IsNullOrEmpty(item.ValorParametro) ? item.ValorParametro : item.DisplayName;
                        string viewName = ObtenerNombreUnico(baseName + " - 3D");

                        var view3D = View3D.CreateIsometric(_doc, _viewFamilyTypeId);
                        view3D.Name = viewName;
                        if (_plantillaIdSeleccionada != ElementId.InvalidElementId)
                            view3D.ViewTemplateId = _plantillaIdSeleccionada;
                        view3D.SetOrientation(new ViewOrientation3D(new XYZ(1, -1, 1), new XYZ(0, 0, 1), new XYZ(1, 1, 0)));

                        if (!view3D.IsLocked)
                            view3D.SaveOrientationAndLock();

                        view3D.IsolateElementsTemporary(new List<ElementId> { elemento.Id });
                        view3D.ConvertTemporaryHideIsolateToPermanent();

                        view3D.Scale = 100;
                        creadas++;
                    }
                    t.Commit();
                }

                progressBar.Visibility = System.Windows.Visibility.Collapsed;
                btnGenerar.Visibility = System.Windows.Visibility.Collapsed;
                btnCerrar.Visibility = System.Windows.Visibility.Visible;
                txtResultado.Text = $"Operación completada. Se generaron {creadas} vistas.";
                MessageBox.Show("Vistas generadas correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                progressBar.Visibility = System.Windows.Visibility.Collapsed;
                btnGenerar.IsEnabled = true;
                txtResultado.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnGenerar.IsEnabled = true;
            }
        }

        private string ObtenerNombreUnico(string nombreBase)
        {
            string nombre = SanitizarNombre(nombreBase);
            int contador = 1;
            while (new FilteredElementCollector(_doc).OfClass(typeof(View3D)).Cast<View3D>().Any(v => v.Name == nombre))
            {
                nombre = SanitizarNombre($"{nombreBase}_{contador}");
                contador++;
            }
            return nombre;
        }

        private string SanitizarNombre(string nombre)
        {
            char[] invalidos = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalidos)
                nombre = nombre.Replace(c, '_');
            return nombre;
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}