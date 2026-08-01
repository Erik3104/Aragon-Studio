using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AragonStudio.UI.Documentacion.EtiquetadoArquitectonico
{
    public partial class EtiquetadoArquitectonicoWindow : Window
    {
        private UIApplication _uiApp;
        private Document _doc;
        private List<CategoriaArquitectonica> _categorias;
        private List<FamilySymbol> _todosLosTiposEtiqueta;
        private List<FamilySymbol> _tiposEtiquetaFiltrados;
        private List<CheckBoxInfo> _checkBoxesVistas;
        private ElementId _tipoEtiquetaSeleccionadoId;
        private bool _cancelarProceso = false;

        public class CategoriaArquitectonica
        {
            public string Nombre { get; set; }
            public BuiltInCategory BuiltInCat { get; set; }
            public BuiltInCategory TagBuiltInCat { get; set; }
            public bool HasValidTag { get; set; }
        }

        public class CheckBoxInfo
        {
            public System.Windows.Controls.CheckBox CheckBox { get; set; }
            public View View { get; set; }
        }

        public EtiquetadoArquitectonicoWindow(UIApplication uiApp)
        {
            InitializeComponent();
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
            }
            catch { }

            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;
            _checkBoxesVistas = new List<CheckBoxInfo>();
            CargarCategoriasArquitectonicas();
            CargarVistas();
            txtFiltroEtiquetas.TextChanged += (s, e) => FiltrarEtiquetas();
            cmbTiposEtiqueta.SelectionChanged += (s, e) => VerificarHabilitarBoton();
            ActualizarImpactoEstimado();
        }

        private void CargarCategoriasArquitectonicas()
        {
            _categorias = new List<CategoriaArquitectonica>
            {
                new CategoriaArquitectonica { Nombre = "Muros", BuiltInCat = BuiltInCategory.OST_Walls, TagBuiltInCat = BuiltInCategory.OST_WallTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Puertas", BuiltInCat = BuiltInCategory.OST_Doors, TagBuiltInCat = BuiltInCategory.OST_DoorTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Ventanas", BuiltInCat = BuiltInCategory.OST_Windows, TagBuiltInCat = BuiltInCategory.OST_WindowTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Suelos", BuiltInCat = BuiltInCategory.OST_Floors, TagBuiltInCat = BuiltInCategory.OST_FloorTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Techos", BuiltInCat = BuiltInCategory.OST_Roofs, TagBuiltInCat = BuiltInCategory.OST_RoofTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Cielos rasos", BuiltInCat = BuiltInCategory.OST_Ceilings, TagBuiltInCat = BuiltInCategory.OST_CeilingTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Mobiliario", BuiltInCat = BuiltInCategory.OST_Furniture, TagBuiltInCat = BuiltInCategory.OST_FurnitureTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Muebles fijos", BuiltInCat = BuiltInCategory.OST_Casework, TagBuiltInCat = BuiltInCategory.OST_CaseworkTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Equipamiento", BuiltInCat = BuiltInCategory.OST_SpecialityEquipment, TagBuiltInCat = BuiltInCategory.OST_SpecialityEquipmentTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Modelos genéricos", BuiltInCat = BuiltInCategory.OST_GenericModel, TagBuiltInCat = BuiltInCategory.OST_GenericModelTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Escaleras", BuiltInCat = BuiltInCategory.OST_Stairs, TagBuiltInCat = BuiltInCategory.OST_StairsTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Rampas", BuiltInCat = BuiltInCategory.OST_Ramps, TagBuiltInCat = BuiltInCategory.OST_RampTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Habitaciones", BuiltInCat = BuiltInCategory.OST_Rooms, TagBuiltInCat = BuiltInCategory.OST_RoomTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Áreas", BuiltInCat = BuiltInCategory.OST_Areas, TagBuiltInCat = BuiltInCategory.OST_AreaTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Columnas arquitectónicas", BuiltInCat = BuiltInCategory.OST_Columns, TagBuiltInCat = BuiltInCategory.OST_ColumnTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Paneles de muro cortina", BuiltInCat = BuiltInCategory.OST_CurtainWallPanels, TagBuiltInCat = BuiltInCategory.OST_CurtainWallPanelTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Montantes de muro cortina", BuiltInCat = BuiltInCategory.OST_CurtainWallMullions, TagBuiltInCat = BuiltInCategory.OST_CurtainWallMullionTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Masas", BuiltInCat = BuiltInCategory.OST_Mass, TagBuiltInCat = BuiltInCategory.OST_MassTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Aparcamiento", BuiltInCat = BuiltInCategory.OST_Parking, TagBuiltInCat = BuiltInCategory.OST_ParkingTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Vegetación", BuiltInCat = BuiltInCategory.OST_Planting, TagBuiltInCat = BuiltInCategory.OST_PlantingTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Piezas", BuiltInCat = BuiltInCategory.OST_Parts, TagBuiltInCat = BuiltInCategory.OST_PartTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Elementos de detalle", BuiltInCat = BuiltInCategory.OST_DetailComponents, TagBuiltInCat = BuiltInCategory.OST_DetailComponentTags, HasValidTag = true },
                new CategoriaArquitectonica { Nombre = "Señalización", BuiltInCat = BuiltInCategory.OST_TitleBlocks, TagBuiltInCat = BuiltInCategory.OST_TitleBlocks, HasValidTag = true }
            };
            _categorias = _categorias.Where(c => c.HasValidTag).OrderBy(c => c.Nombre).ToList();
            cmbCategorias.ItemsSource = _categorias.Select(c => c.Nombre).ToList();
            if (_categorias.Any()) cmbCategorias.SelectedIndex = 0;
        }

        private void cmbCategorias_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbCategorias.SelectedIndex >= 0)
            {
                CargarTiposEtiqueta();
                btnAplicar.IsEnabled = false;
                txtFiltroEtiquetas.Text = "Filtrar por nombre...";
                ActualizarImpactoEstimado();
                VerificarHabilitarBoton();
            }
        }

        private void CargarTiposEtiqueta()
        {
            var cat = _categorias[cmbCategorias.SelectedIndex];
            var tagCat = Category.GetCategory(_doc, cat.TagBuiltInCat);
            if (tagCat == null)
            {
                cmbTiposEtiqueta.ItemsSource = null;
                cmbTiposEtiqueta.IsEnabled = false;
                txtResultado.Text = $"No hay categoría de etiqueta para '{cat.Nombre}'.";
                return;
            }

            _todosLosTiposEtiqueta = new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                .Where(ts => ts.Family != null && ts.Family.FamilyCategory != null && ts.Family.FamilyCategory.Id == tagCat.Id && ts.IsActive)
                .OrderBy(ts => ts.Family.Name)
                .ToList();

            ActualizarListaEtiquetas(_todosLosTiposEtiqueta);
        }

        private void FiltrarEtiquetas()
        {
            if (_todosLosTiposEtiqueta == null) return;
            string filtro = txtFiltroEtiquetas.Text.ToLower();
            if (string.IsNullOrEmpty(filtro) || filtro == "filtrar por nombre...")
            {
                ActualizarListaEtiquetas(_todosLosTiposEtiqueta);
                return;
            }
            var filtrados = _todosLosTiposEtiqueta.Where(ts =>
                ts.Family.Name.ToLower().Contains(filtro) ||
                ts.Name.ToLower().Contains(filtro)
            ).ToList();
            ActualizarListaEtiquetas(filtrados);
        }

        private void ActualizarListaEtiquetas(List<FamilySymbol> lista)
        {
            _tiposEtiquetaFiltrados = lista;
            cmbTiposEtiqueta.ItemsSource = lista.Select(ts => $"{ts.Family.Name} : {ts.Name}").ToList();
            cmbTiposEtiqueta.IsEnabled = lista.Any();
            if (lista.Any()) cmbTiposEtiqueta.SelectedIndex = 0;
            btnAplicar.IsEnabled = cmbTiposEtiqueta.SelectedIndex >= 0 && _checkBoxesVistas.Any(cb => cb.CheckBox.IsChecked == true);
        }

        private void CargarVistas()
        {
            var views = new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.DrawingSheet)
                .OrderBy(v => v.Name)
                .ToList();

            treeVistas.Items.Clear();
            _checkBoxesVistas.Clear();

            var grouped = views.GroupBy(v => v.ViewType).OrderBy(g => g.Key.ToString());
            foreach (var group in grouped)
            {
                var treeItem = new System.Windows.Controls.TreeViewItem();
                treeItem.Header = group.Key.ToString();
                treeItem.IsExpanded = false;
                foreach (var view in group.OrderBy(v => v.Name))
                {
                    var cb = new System.Windows.Controls.CheckBox();
                    cb.Content = view.Name;
                    cb.Tag = view.Id;
                    cb.Checked += (s, ev) => { VerificarHabilitarBoton(); ActualizarImpactoEstimado(); };
                    cb.Unchecked += (s, ev) => { VerificarHabilitarBoton(); ActualizarImpactoEstimado(); };
                    treeItem.Items.Add(cb);
                    _checkBoxesVistas.Add(new CheckBoxInfo { CheckBox = cb, View = view });
                }
                treeVistas.Items.Add(treeItem);
            }
            ActualizarImpactoEstimado();
        }

        private void VerificarHabilitarBoton()
        {
            btnAplicar.IsEnabled = _checkBoxesVistas.Any(cb => cb.CheckBox.IsChecked == true) && cmbTiposEtiqueta.SelectedIndex >= 0;
        }

        private void SeleccionarTodas_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cbInfo in _checkBoxesVistas) cbInfo.CheckBox.IsChecked = true;
            ActualizarImpactoEstimado();
        }

        private void LimpiarSeleccion_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cbInfo in _checkBoxesVistas) cbInfo.CheckBox.IsChecked = false;
            ActualizarImpactoEstimado();
        }

        private void txtFiltroEtiquetas_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtFiltroEtiquetas.Text == "Filtrar por nombre...") txtFiltroEtiquetas.Text = "";
        }

        private void txtFiltroEtiquetas_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFiltroEtiquetas.Text)) txtFiltroEtiquetas.Text = "Filtrar por nombre...";
        }

        private bool VerificarVistas3D(List<View> vistas)
        {
            foreach (var view in vistas)
            {
                if (view.ViewType == ViewType.ThreeD)
                {
                    View3D view3D = view as View3D;
                    if (view3D != null && !view3D.IsLocked)
                    {
                        MessageBox.Show($"La vista 3D '{view3D.Name}' no está bloqueada. Por favor, bloquee la vista antes de ejecutar la herramienta.", "Vista 3D no bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
            return true;
        }

        private void ActualizarImpactoEstimado()
        {
            var vistasSeleccionadas = _checkBoxesVistas.Where(cb => cb.CheckBox.IsChecked == true).Select(cb => cb.View).ToList();
            int totalEtiquetas = ContarEtiquetas();
            int modo = cmbModo.SelectedIndex;

            txtTotalElementos.Text = totalEtiquetas.ToString();
            txtVistasSeleccionadas.Text = vistasSeleccionadas.Count.ToString();
            txtEtiquetasPorElemento.Text = modo == 0 ? "Todos los elementos" : "Un elemento por tipo";
        }

        private int ContarEtiquetas()
        {
            var vistasSeleccionadas = _checkBoxesVistas.Where(cb => cb.CheckBox.IsChecked == true).Select(cb => cb.View).ToList();
            if (!vistasSeleccionadas.Any()) return 0;

            var cat = _categorias[cmbCategorias.SelectedIndex];
            int modo = cmbModo.SelectedIndex;
            int total = 0;

            foreach (var view in vistasSeleccionadas)
            {
                var elementos = new FilteredElementCollector(_doc, view.Id)
                    .OfCategory(cat.BuiltInCat)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
                if (!elementos.Any()) continue;

                if (modo == 0)
                    total += elementos.Count;
                else
                {
                    var tiposProcesados = new HashSet<ElementId>();
                    foreach (var elem in elementos)
                    {
                        var typeId = elem.GetTypeId();
                        if (typeId != null && !tiposProcesados.Contains(typeId))
                        {
                            tiposProcesados.Add(typeId);
                            total++;
                        }
                    }
                }
            }
            return total;
        }

        private XYZ ObtenerPuntoElemento(Element elem, View v)
        {
            if (elem.Category != null && elem.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Rooms)
            {
                if (elem.Location is LocationPoint lp) return lp.Point;
                var bb = elem.get_BoundingBox(v);
                if (bb != null) return (bb.Min + bb.Max) / 2;
                return null;
            }
            if (elem.Location is LocationCurve lc && lc.Curve != null) return lc.Curve.Evaluate(0.5, true);
            if (elem.Location is LocationPoint lp2) return lp2.Point;
            var bb2 = elem.get_BoundingBox(v);
            if (bb2 != null) return (bb2.Min + bb2.Max) / 2;
            return null;
        }

        private void BtnAplicar_Click(object sender, RoutedEventArgs e)
        {
            var vistasSeleccionadas = _checkBoxesVistas
                .Where(cb => cb.CheckBox.IsChecked == true)
                .Select(cb => cb.View).ToList();
            if (!vistasSeleccionadas.Any())
            {
                MessageBox.Show("Selecciona al menos una vista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!VerificarVistas3D(vistasSeleccionadas)) return;

            int totalEstimado = ContarEtiquetas();
            if (totalEstimado > 100)
            {
                var result = MessageBox.Show(
                    $"Se van a generar aproximadamente {totalEstimado} etiquetas. Esto podría ralentizar Revit. ¿Desea continuar?",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No) return;
            }

            if (cmbTiposEtiqueta.SelectedIndex < 0 || _tiposEtiquetaFiltrados == null)
            {
                MessageBox.Show("Selecciona un tipo de etiqueta.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _tipoEtiquetaSeleccionadoId = _tiposEtiquetaFiltrados[cmbTiposEtiqueta.SelectedIndex].Id;
            var tagSymbol = _doc.GetElement(_tipoEtiquetaSeleccionadoId) as FamilySymbol;
            if (tagSymbol == null) return;
            if (!tagSymbol.IsActive) tagSymbol.Activate();

            var cat = _categorias[cmbCategorias.SelectedIndex];
            int modo = cmbModo.SelectedIndex;
            bool usarLider = rbConLider.IsChecked == true;

            btnAplicar.IsEnabled = false;
            btnCancelar.IsEnabled = true;
            btnCancelar.Visibility = System.Windows.Visibility.Visible;
            progressBar.Visibility = System.Windows.Visibility.Visible;
            progressBar.Maximum = vistasSeleccionadas.Count;
            progressBar.Value = 0;
            txtResultado.Text = "Aplicando etiquetas...";
            _cancelarProceso = false;

            int totalEtiquetas = 0;
            try
            {
                using (var t = new Transaction(_doc, "Etiquetado Arquitectónico"))
                {
                    t.Start();
                    for (int idx = 0; idx < vistasSeleccionadas.Count; idx++)
                    {
                        if (_cancelarProceso) break;

                        var view = vistasSeleccionadas[idx];
                        progressBar.Value = idx + 1;
                        txtResultado.Text = $"Procesando vista {idx + 1} de {vistasSeleccionadas.Count}: {view.Name}";

                        if (cat.BuiltInCat == BuiltInCategory.OST_Rooms && view.ViewType != ViewType.FloorPlan) continue;

                        var elementos = new FilteredElementCollector(_doc, view.Id)
                            .OfCategory(cat.BuiltInCat)
                            .WhereElementIsNotElementType()
                            .Cast<Element>().ToList();
                        if (!elementos.Any()) continue;

                        var existingTags = new FilteredElementCollector(_doc, view.Id)
                            .OfClass(typeof(IndependentTag))
                            .Cast<IndependentTag>().ToList();

                        List<(Element elem, XYZ punto)> elementosAEtiquetar = new List<(Element, XYZ)>();
                        if (modo == 0)
                        {
                            foreach (var elem in elementos)
                            {
                                var punto = ObtenerPuntoElemento(elem, view);
                                if (punto != null) elementosAEtiquetar.Add((elem, punto));
                            }
                        }
                        else
                        {
                            var tiposProcesados = new HashSet<ElementId>();
                            foreach (var elem in elementos)
                            {
                                var typeId = elem.GetTypeId();
                                if (typeId == null || tiposProcesados.Contains(typeId)) continue;
                                tiposProcesados.Add(typeId);
                                var punto = ObtenerPuntoElemento(elem, view);
                                if (punto != null) elementosAEtiquetar.Add((elem, punto));
                            }
                        }

                        var placedBBs = existingTags
                            .Select(t => t.get_BoundingBox(view))
                            .Where(bb => bb != null).ToList();

                        foreach (var (elem, puntoBase) in elementosAEtiquetar)
                        {
                            if (_cancelarProceso) break;

                            bool yaExiste = false;
                            foreach (var tag in existingTags)
                            {
                                if (tag.GetTypeId() != _tipoEtiquetaSeleccionadoId) continue;
                                if (tag.GetTaggedLocalElementIds().Contains(elem.Id)) { yaExiste = true; break; }
                            }
                            if (yaExiste) continue;

                            var loc = EncontrarUbicacionLibreOptimizado(puntoBase, placedBBs, view, elem, usarLider);
                            if (loc == null) loc = puntoBase;

                            try
                            {
                                var refElem = new Reference(elem);
                                var tag = IndependentTag.Create(_doc, view.Id, refElem, usarLider,
                                    TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, loc);
                                tag.ChangeTypeId(_tipoEtiquetaSeleccionadoId);
                                placedBBs.Add(tag.get_BoundingBox(view));
                                totalEtiquetas++;
                            }
                            catch { }
                        }
                    }
                    if (!_cancelarProceso) t.Commit();
                    else t.RollBack();
                }

                txtResultado.Text = _cancelarProceso
                    ? "Proceso cancelado por el usuario."
                    : $"Etiquetado completado. Se colocaron {totalEtiquetas} etiquetas.";
            }
            catch (Exception ex)
            {
                txtResultado.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnAplicar.IsEnabled = true;
                btnCancelar.IsEnabled = false;
                btnCancelar.Visibility = System.Windows.Visibility.Collapsed;
                progressBar.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            _cancelarProceso = true;
            btnCancelar.IsEnabled = false;
            txtResultado.Text = "Cancelando...";
        }

        private XYZ EncontrarUbicacionLibreOptimizado(XYZ basePoint, List<BoundingBoxXYZ> existingBBs,
            View view, Element element, bool hasLeader)
        {
            double[] offsets = { 0, 0.5, -0.5, 1, -1 };
            foreach (var dx in offsets)
            {
                foreach (var dy in offsets)
                {
                    var newPoint = new XYZ(basePoint.X + dx, basePoint.Y + dy, basePoint.Z);
                    try
                    {
                        var refElem = new Reference(element);
                        var tempTag = IndependentTag.Create(_doc, view.Id, refElem, hasLeader,
                            TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, newPoint);
                        var bb = tempTag.get_BoundingBox(view);
                        _doc.Delete(tempTag.Id);

                        if (!existingBBs.Any(existing =>
                            existing != null && bb != null &&
                            existing.Min.X < bb.Max.X && existing.Max.X > bb.Min.X &&
                            existing.Min.Y < bb.Max.Y && existing.Max.Y > bb.Min.Y))
                            return newPoint;
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}