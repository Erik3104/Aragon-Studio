using AragonStudio.Services.EtiquetadoEstructural;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AragonStudio.UI.Documentacion.EtiquetadoEstructural
{
    public partial class EtiquetadoEstructuralWindow : Window
    {
        private UIDocument _uidoc;
        private Document _doc;
        private StructuralIntentEngine _engine;

        private List<BuiltInCategory> categoriasEstructurales = new List<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Rebar,
            BuiltInCategory.OST_StructConnections,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_Ramps,
            BuiltInCategory.OST_GenericModel
        };

        private List<FamilySymbol> _todosLosTiposEtiqueta;

        public EtiquetadoEstructuralWindow(UIDocument uidoc, Document doc, UIApplication uiApp)
        {
            try
            {
                InitializeComponent();
                try
                {
                    Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                    this.Icon = new BitmapImage(iconUri);
                }
                catch { }

                _uidoc = uidoc;
                _doc = doc;
                _engine = new StructuralIntentEngine(uiApp, uidoc, doc);
                this.Loaded += Window_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en constructor: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CargarCategoriasEstructurales();
                CargarVistas();
                ActualizarModoInteligente();

                rbPorTipo.Checked += Modo_Changed;
                rbPorElemento.Checked += Modo_Changed;
                rbInteligente.Checked += Modo_Changed;
                cmbTiposEtiqueta.SelectionChanged += TipoEtiqueta_Changed;

                if (lblMaxGrupos != null)
                    lblMaxGrupos.Text = "Máx. etiquetas por grupo:";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en Window_Loaded: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CargarCategoriasEstructurales()
        {
            if (_doc == null) return;
            cmbCategorias.Items.Clear();
            foreach (var bic in categoriasEstructurales)
            {
                Category cat = Category.GetCategory(_doc, bic);
                if (cat != null)
                {
                    cmbCategorias.Items.Add(new ComboBoxItem { Content = cat.Name, Tag = bic });
                }
            }
            if (cmbCategorias.Items.Count > 0)
                cmbCategorias.SelectedIndex = 0;
        }

        private void CargarVistas()
        {
            if (_doc == null) return;
            var vistas = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.Internal && v.ViewType != ViewType.Legend)
                .OrderBy(v => v.ViewType).ThenBy(v => v.Name)
                .ToList();
            treeVistas.Items.Clear();
            foreach (var grupo in vistas.GroupBy(v => v.ViewType))
            {
                TreeViewItem tipoItem = new TreeViewItem { Header = grupo.Key.ToString(), IsExpanded = false };
                foreach (var vista in grupo)
                {
                    CheckBox cb = new CheckBox { Content = vista.Name, Tag = vista, Foreground = System.Windows.Media.Brushes.White };
                    TreeViewItem item = new TreeViewItem { Header = cb };
                    tipoItem.Items.Add(item);
                }
                treeVistas.Items.Add(tipoItem);
            }
        }

        private void ActualizarModoInteligente()
        {
            if (!IsLoaded) return;
            bool isRebar = (GetSelectedBuiltInCategory() == BuiltInCategory.OST_Rebar);
            rbInteligente.IsEnabled = isRebar;
            if (!isRebar && rbInteligente.IsChecked == true)
                rbPorTipo.IsChecked = true;
            pnlGrupos.Visibility = (rbInteligente.IsChecked == true && isRebar) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void Categoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            BuiltInCategory selectedCat = GetSelectedBuiltInCategory();
            CargarTiposEtiqueta(selectedCat);
            ActualizarModoInteligente();
        }

        private void Modo_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            ActualizarModoInteligente();
        }

        private void TipoEtiqueta_Changed(object sender, SelectionChangedEventArgs e) { }

        private void Numero_Validacion(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void CargarTiposEtiqueta(BuiltInCategory category)
        {
            cmbTiposEtiqueta.Items.Clear();
            cmbTiposEtiqueta.IsEnabled = false;
            cmbTiposEtiqueta.Items.Add(new ComboBoxItem { Content = "Cargando etiquetas...", IsEnabled = false });

            BuiltInCategory tagCategory;
            switch (category)
            {
                case BuiltInCategory.OST_StructuralFraming: tagCategory = BuiltInCategory.OST_StructuralFramingTags; break;
                case BuiltInCategory.OST_StructuralColumns: tagCategory = BuiltInCategory.OST_StructuralColumnTags; break;
                case BuiltInCategory.OST_Floors: tagCategory = BuiltInCategory.OST_FloorTags; break;
                case BuiltInCategory.OST_StructuralFoundation: tagCategory = BuiltInCategory.OST_StructuralFoundationTags; break;
                case BuiltInCategory.OST_Walls: tagCategory = BuiltInCategory.OST_WallTags; break;
                case BuiltInCategory.OST_Rebar: tagCategory = BuiltInCategory.OST_RebarTags; break;
                case BuiltInCategory.OST_StructConnections: tagCategory = BuiltInCategory.OST_StructConnectionSymbols; break;
                case BuiltInCategory.OST_Stairs: tagCategory = BuiltInCategory.OST_StairsTags; break;
                case BuiltInCategory.OST_Ramps: tagCategory = BuiltInCategory.OST_RampTags; break;
                case BuiltInCategory.OST_GenericModel: tagCategory = BuiltInCategory.OST_GenericModelTags; break;
                default: tagCategory = BuiltInCategory.OST_Tags; break;
            }

            Category tagCat = null;
            try { tagCat = Category.GetCategory(_doc, tagCategory); } catch { }

            List<FamilySymbol> tags = new List<FamilySymbol>();
            if (tagCat != null)
            {
                tags = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(ts => ts.Family != null && ts.Family.FamilyCategory != null &&
                                 ts.Family.FamilyCategory.Id == tagCat.Id && ts.IsActive)
                    .OrderBy(ts => ts.Family.Name)
                    .ToList();
            }

            cmbTiposEtiqueta.Items.Clear();
            if (tags.Count == 0)
            {
                cmbTiposEtiqueta.Items.Add(new ComboBoxItem { Content = "No hay etiquetas para esta categoría", IsEnabled = false });
                cmbTiposEtiqueta.IsEnabled = false;
                return;
            }

            _todosLosTiposEtiqueta = tags;
            foreach (var ts in tags)
                cmbTiposEtiqueta.Items.Add(new ComboBoxItem { Content = $"{ts.Family.Name} : {ts.Name}", Tag = ts });
            cmbTiposEtiqueta.IsEnabled = true;
            if (cmbTiposEtiqueta.Items.Count > 0) cmbTiposEtiqueta.SelectedIndex = 0;
        }

        private void BtnPrevisualizar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BuiltInCategory cat = GetSelectedBuiltInCategory();
                View activeView = _uidoc.ActiveView;
                if (activeView == null)
                {
                    MessageBox.Show("No hay una vista activa.", "ARAGÓN STUDIO", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var elementos = new FilteredElementCollector(_doc, activeView.Id)
                    .OfCategory(cat)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                if (elementos.Count == 0)
                {
                    MessageBox.Show($"No hay elementos de la categoría seleccionada en la vista actual.", "ARAGÓN STUDIO", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _uidoc.Selection.SetElementIds(elementos.Select(e => e.Id).ToList());
                MessageBox.Show($"Se han seleccionado {elementos.Count} elementos en la vista actual.", "Previsualización", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al previsualizar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLimpiarSeleccionRevit_Click(object sender, RoutedEventArgs e)
        {
            _uidoc.Selection.SetElementIds(new List<ElementId>());
            MessageBox.Show("Selección limpiada.", "ARAGÓN STUDIO", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private BuiltInCategory GetSelectedBuiltInCategory()
        {
            if (cmbCategorias.SelectedItem is ComboBoxItem item && item.Tag is BuiltInCategory bic)
                return bic;
            return BuiltInCategory.OST_StructuralFraming;
        }

        private TaggingRequest ObtenerRequest()
        {
            BuiltInCategory cat = GetSelectedBuiltInCategory();
            TaggingMode mode;
            if (rbPorTipo.IsChecked == true)
                mode = TaggingMode.ByType;
            else if (rbInteligente.IsChecked == true && rbInteligente.IsEnabled)
                mode = TaggingMode.Intelligent;
            else
                mode = TaggingMode.ByElement;

            ElementId tagSymbolId = null;
            if (cmbTiposEtiqueta.SelectedItem is ComboBoxItem item && item.Tag is FamilySymbol fs)
                tagSymbolId = fs.Id;

            List<View> selectedViews = new List<View>();
            foreach (TreeViewItem tipo in treeVistas.Items)
                foreach (TreeViewItem itemT in tipo.Items)
                    if ((itemT.Header as CheckBox).IsChecked == true)
                        selectedViews.Add((View)(itemT.Header as CheckBox).Tag);

            selectedViews = selectedViews.GroupBy(v => v.Id).Select(g => g.First()).ToList();

            int maxGrupos = 2;
            if (int.TryParse(txtMaxGrupos.Text, out int val) && val > 0)
                maxGrupos = val;

            return new TaggingRequest
            {
                SelectedViews = selectedViews,
                StructuralCategory = cat,
                Mode = mode,
                TagSymbolId = tagSymbolId,
                HasLeader = rbConLider.IsChecked == true,
                MaxTagGroups = maxGrupos
            };
        }

        private void SeleccionarTodas_Click(object sender, RoutedEventArgs e) => MarcarTodos(true);
        private void LimpiarSeleccion_Click(object sender, RoutedEventArgs e) => MarcarTodos(false);
        private void MarcarTodos(bool seleccionar)
        {
            foreach (TreeViewItem tipo in treeVistas.Items)
                foreach (TreeViewItem item in tipo.Items)
                {
                    if (item.Header is CheckBox cb) cb.IsChecked = seleccionar;
                }
        }

        private async void BtnAplicar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TaggingRequest request = ObtenerRequest();
                if (request.SelectedViews.Count == 0)
                {
                    MessageBox.Show("Seleccione al menos una vista.", "ARAGÓN STUDIO", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnAplicar.IsEnabled = false;

                AnalysisResult analysis = _engine.EstimateImpact(request);
                if (analysis == null) return;

                txtTotalEtiquetas.Text = analysis.EstimatedTagCount.ToString();
                txtZonas.Text = analysis.ZonesCount.ToString();
                string sat = analysis.Saturation == SaturationLevel.Low ? "BAJO" : (analysis.Saturation == SaturationLevel.Medium ? "MEDIO" : "ALTO");
                txtSaturacion.Text = sat;
                progressSaturacion.Value = analysis.SaturationPercent;

                txtResultadoEjecucion.Text = $"Etiquetas estimadas: {analysis.EstimatedTagCount}";

                if (analysis.EstimatedTagCount > 100)
                {
                    var result = MessageBox.Show($"Se generarán {analysis.EstimatedTagCount} etiquetas. ¿Desea continuar?", "Advertencia", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No) return;
                }

                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                _engine.ExecuteTagging(request);
                MessageBox.Show("Etiquetado estructural completado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error detallado", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnAplicar.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }
    }
}