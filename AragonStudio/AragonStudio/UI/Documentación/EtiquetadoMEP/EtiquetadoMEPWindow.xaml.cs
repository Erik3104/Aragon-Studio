using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AragonStudio.UI.Documentacion.EtiquetadoMep
{
    public partial class EtiquetadoMepWindow : Window
    {
        private UIApplication _uiApp;
        private Document _doc;
        private List<CategoriaMEP> _categorias;
        private List<FamilySymbol> _todosLosTiposEtiqueta;
        private List<FamilySymbol> _tiposEtiquetaFiltrados;
        private List<CheckBoxInfo> _checkBoxesVistas;
        private ElementId _tipoEtiquetaSeleccionadoId;
        private List<string> _parametrosDisponibles;
        private bool _cancelarProceso = false;

        public class CategoriaMEP
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

        public EtiquetadoMepWindow(UIApplication uiApp)
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
            CargarCategoriasMEP();
            CargarVistas();
            txtFiltroEtiquetas.TextChanged += (s, e) => FiltrarEtiquetas();
            cmbTiposEtiqueta.SelectionChanged += (s, e) => VerificarHabilitarBoton();
            cmbModo.SelectedIndex = 0;
            ActualizarImpactoEstimado();
        }

        private void CargarCategoriasMEP()
        {
            _categorias = new List<CategoriaMEP>
            {
                new CategoriaMEP { Nombre = "Accesorios de conductos", BuiltInCat = BuiltInCategory.OST_DuctAccessory, TagBuiltInCat = BuiltInCategory.OST_DuctAccessoryTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Accesorios de tuberías", BuiltInCat = BuiltInCategory.OST_PipeAccessory, TagBuiltInCat = BuiltInCategory.OST_PipeAccessoryTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Aparatos eléctricos", BuiltInCat = BuiltInCategory.OST_ElectricalFixtures, TagBuiltInCat = BuiltInCategory.OST_ElectricalFixtureTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Aparatos sanitarios", BuiltInCat = BuiltInCategory.OST_PlumbingFixtures, TagBuiltInCat = BuiltInCategory.OST_PlumbingFixtureTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Áreas", BuiltInCat = BuiltInCategory.OST_Areas, TagBuiltInCat = BuiltInCategory.OST_AreaTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Bandejas de cables", BuiltInCat = BuiltInCategory.OST_CableTray, TagBuiltInCat = BuiltInCategory.OST_CableTrayTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Conductos", BuiltInCat = BuiltInCategory.OST_DuctCurves, TagBuiltInCat = BuiltInCategory.OST_DuctTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Dispositivos de comunicación", BuiltInCat = BuiltInCategory.OST_CommunicationDevices, TagBuiltInCat = BuiltInCategory.OST_CommunicationDeviceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Dispositivos de datos", BuiltInCat = BuiltInCategory.OST_DataDevices, TagBuiltInCat = BuiltInCategory.OST_DataDeviceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Dispositivos de iluminación", BuiltInCat = BuiltInCategory.OST_LightingDevices, TagBuiltInCat = BuiltInCategory.OST_LightingDeviceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Dispositivos de seguridad", BuiltInCat = BuiltInCategory.OST_SecurityDevices, TagBuiltInCat = BuiltInCategory.OST_SecurityDeviceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Dispositivos telefónicos", BuiltInCat = BuiltInCategory.OST_TelephoneDevices, TagBuiltInCat = BuiltInCategory.OST_TelephoneDeviceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Elementos de detalle", BuiltInCat = BuiltInCategory.OST_DetailComponents, TagBuiltInCat = BuiltInCategory.OST_DetailComponentTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Equipo de fontanería", BuiltInCat = BuiltInCategory.OST_PlumbingFixtures, TagBuiltInCat = BuiltInCategory.OST_PlumbingFixtureTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Equipo médico", BuiltInCat = BuiltInCategory.OST_MedicalEquipment, TagBuiltInCat = BuiltInCategory.OST_MedicalEquipmentTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Equipos eléctricos", BuiltInCat = BuiltInCategory.OST_ElectricalEquipment, TagBuiltInCat = BuiltInCategory.OST_ElectricalEquipmentTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Equipos mecánicos", BuiltInCat = BuiltInCategory.OST_MechanicalEquipment, TagBuiltInCat = BuiltInCategory.OST_MechanicalEquipmentTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Espacios", BuiltInCat = BuiltInCategory.OST_MEPSpaces, TagBuiltInCat = BuiltInCategory.OST_MEPSpaceTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Habitaciones", BuiltInCat = BuiltInCategory.OST_Rooms, TagBuiltInCat = BuiltInCategory.OST_RoomTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Luminarias", BuiltInCat = BuiltInCategory.OST_LightingFixtures, TagBuiltInCat = BuiltInCategory.OST_LightingFixtureTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Masas", BuiltInCat = BuiltInCategory.OST_Mass, TagBuiltInCat = BuiltInCategory.OST_MassTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Modelos genéricos", BuiltInCat = BuiltInCategory.OST_GenericModel, TagBuiltInCat = BuiltInCategory.OST_GenericModelTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Piezas", BuiltInCat = BuiltInCategory.OST_Parts, TagBuiltInCat = BuiltInCategory.OST_PartTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Rociadores", BuiltInCat = BuiltInCategory.OST_Sprinklers, TagBuiltInCat = BuiltInCategory.OST_SprinklerTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Tuberías", BuiltInCat = BuiltInCategory.OST_PipeCurves, TagBuiltInCat = BuiltInCategory.OST_PipeTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Tubos (Conduits)", BuiltInCat = BuiltInCategory.OST_Conduit, TagBuiltInCat = BuiltInCategory.OST_ConduitTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Uniones de bandeja de cables", BuiltInCat = BuiltInCategory.OST_CableTrayFitting, TagBuiltInCat = BuiltInCategory.OST_CableTrayFittingTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Uniones de conducto", BuiltInCat = BuiltInCategory.OST_DuctFitting, TagBuiltInCat = BuiltInCategory.OST_DuctFittingTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Uniones de tubería", BuiltInCat = BuiltInCategory.OST_PipeFitting, TagBuiltInCat = BuiltInCategory.OST_PipeFittingTags, HasValidTag = true },
                new CategoriaMEP { Nombre = "Uniones de tubos", BuiltInCat = BuiltInCategory.OST_ConduitFitting, TagBuiltInCat = BuiltInCategory.OST_ConduitFittingTags, HasValidTag = true }
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
                CargarParametros();
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
            VerificarHabilitarBoton();
        }

        private void CargarParametros()
        {
            var cat = _categorias[cmbCategorias.SelectedIndex];
            var elementos = new FilteredElementCollector(_doc).OfCategory(cat.BuiltInCat).WhereElementIsNotElementType().Cast<Element>().Take(100).ToList();
            _parametrosDisponibles = new List<string>();

            foreach (var elem in elementos)
            {
                if (elem.GetTypeId() != ElementId.InvalidElementId)
                {
                    var tipo = _doc.GetElement(elem.GetTypeId()) as ElementType;
                    if (tipo != null)
                    {
                        foreach (Parameter p in tipo.Parameters)
                        {
                            if (p.StorageType == StorageType.String && !_parametrosDisponibles.Contains(p.Definition.Name))
                                _parametrosDisponibles.Add(p.Definition.Name);
                        }
                    }
                }
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.IsShared && p.Definition != null && !_parametrosDisponibles.Contains(p.Definition.Name))
                        _parametrosDisponibles.Add(p.Definition.Name);
                }
            }

            _parametrosDisponibles.Sort();
            cmbParametros.ItemsSource = _parametrosDisponibles;
            if (_parametrosDisponibles.Any()) cmbParametros.SelectedIndex = 0;
        }

        private void cmbModo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool modoPermiteAvanzadas = (cmbModo.SelectedIndex == 0 || cmbModo.SelectedIndex == 1 || cmbModo.SelectedIndex == 2 || cmbModo.SelectedIndex == 3);
            cmbParametros.IsEnabled = (cmbModo.SelectedIndex == 2);
            chkOpcionesAvanzadas.IsEnabled = modoPermiteAvanzadas;
            if (!chkOpcionesAvanzadas.IsEnabled)
                chkOpcionesAvanzadas.IsChecked = false;
            ActualizarImpactoEstimado();
        }

        private void ChkOpcionesAvanzadas_Checked(object sender, RoutedEventArgs e)
        {
            borderOpcionesAvanzadas.Visibility = System.Windows.Visibility.Visible;
            ActualizarImpactoEstimado();
        }

        private void ChkOpcionesAvanzadas_Unchecked(object sender, RoutedEventArgs e)
        {
            borderOpcionesAvanzadas.Visibility = System.Windows.Visibility.Collapsed;
            ActualizarImpactoEstimado();
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

            if (modo == 0) txtEtiquetasPorElemento.Text = "Todos los elementos";
            else if (modo == 1) txtEtiquetasPorElemento.Text = "Un elemento por tipo";
            else if (modo == 2) txtEtiquetasPorElemento.Text = "Por grupo (parámetro)";
            else txtEtiquetasPorElemento.Text = "Inteligente (tipo + tamaño)";
        }

        private double ObtenerLongitudPies(Element elem)
        {
            if (elem.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Length;
            return 0;
        }

        private int ContarEtiquetas()
        {
            var vistasSeleccionadas = _checkBoxesVistas.Where(cb => cb.CheckBox.IsChecked == true).Select(cb => cb.View).ToList();
            if (!vistasSeleccionadas.Any()) return 0;

            var cat = _categorias[cmbCategorias.SelectedIndex];
            int modo = cmbModo.SelectedIndex;
            string parametroSeleccionado = (modo == 2 && cmbParametros.SelectedItem != null) ? cmbParametros.SelectedItem.ToString() : null;
            bool usarAvanzadas = chkOpcionesAvanzadas.IsChecked == true;
            int valorLimite = usarAvanzadas ? int.TryParse(txtMaxEtiquetas.Text, out int max) ? max : 1 : 0;

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
                {
                    int limite = usarAvanzadas ? valorLimite : int.MaxValue;
                    var ordenados = elementos.OrderByDescending(e => ObtenerLongitudPies(e)).ToList();
                    total += Math.Min(ordenados.Count, limite);
                }
                else if (modo == 1)
                {
                    var grupos = elementos.GroupBy(e => e.GetTypeId());
                    int porGrupo = usarAvanzadas ? valorLimite : 1;
                    foreach (var grupo in grupos)
                    {
                        total += Math.Min(grupo.Count(), porGrupo);
                    }
                }
                else if (modo == 2 && !string.IsNullOrEmpty(parametroSeleccionado))
                {
                    var grupos = new Dictionary<string, List<Element>>();
                    foreach (var elem in elementos)
                    {
                        string valor = ObtenerValorParametro(elem, parametroSeleccionado);
                        if (string.IsNullOrEmpty(valor)) continue;
                        if (!grupos.ContainsKey(valor)) grupos[valor] = new List<Element>();
                        grupos[valor].Add(elem);
                    }
                    int porGrupo = usarAvanzadas ? valorLimite : 1;
                    foreach (var grupo in grupos.Values)
                    {
                        total += Math.Min(grupo.Count, porGrupo);
                    }
                }
                else if (modo == 3)
                {
                    var grupos = AgruparPorTipoYTamano(elementos);
                    int porGrupo = usarAvanzadas ? valorLimite : 1;
                    foreach (var grupo in grupos)
                    {
                        total += Math.Min(grupo.Count, porGrupo);
                    }
                }
            }
            return total;
        }

        private void BtnAplicar_Click(object sender, RoutedEventArgs e)
        {
            var vistasSeleccionadas = _checkBoxesVistas.Where(cb => cb.CheckBox.IsChecked == true).Select(cb => cb.View).ToList();
            if (!vistasSeleccionadas.Any())
            {
                MessageBox.Show("Selecciona al menos una vista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!VerificarVistas3D(vistasSeleccionadas))
                return;

            int totalEstimado = ContarEtiquetas();
            if (totalEstimado > 100)
            {
                var result = MessageBox.Show($"Se van a generar aproximadamente {totalEstimado} etiquetas. ¿Desea continuar?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                    return;
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
            string parametroSeleccionado = (modo == 2 && cmbParametros.SelectedItem != null) ? cmbParametros.SelectedItem.ToString() : null;
            bool usarAvanzadas = chkOpcionesAvanzadas.IsChecked == true;
            int valorLimite = usarAvanzadas ? int.TryParse(txtMaxEtiquetas.Text, out int max) ? max : 1 : 0;
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
                using (var t = new Transaction(_doc, "Etiquetado MEP"))
                {
                    t.Start();
                    for (int idx = 0; idx < vistasSeleccionadas.Count; idx++)
                    {
                        if (_cancelarProceso) break;

                        var view = vistasSeleccionadas[idx];
                        progressBar.Value = idx + 1;
                        txtResultado.Text = $"Procesando vista {idx + 1} de {vistasSeleccionadas.Count}: {view.Name}";

                        var elementos = new FilteredElementCollector(_doc, view.Id)
                            .OfCategory(cat.BuiltInCat)
                            .WhereElementIsNotElementType()
                            .Cast<Element>()
                            .ToList();

                        if (!elementos.Any()) continue;

                        List<Element> elementosAEtiquetar = new List<Element>();

                        if (modo == 0)
                        {
                            int limite = usarAvanzadas ? valorLimite : int.MaxValue;
                            var ordenados = elementos.OrderByDescending(e => ObtenerLongitudPies(e)).ToList();
                            elementosAEtiquetar.AddRange(ordenados.Take(limite));
                        }
                        else if (modo == 1)
                        {
                            int porGrupo = usarAvanzadas ? valorLimite : 1;
                            var grupos = elementos.GroupBy(e => e.GetTypeId());
                            foreach (var grupo in grupos)
                            {
                                var ordenados = grupo.OrderByDescending(e => ObtenerLongitudPies(e)).ToList();
                                elementosAEtiquetar.AddRange(ordenados.Take(porGrupo));
                            }
                        }
                        else if (modo == 2 && !string.IsNullOrEmpty(parametroSeleccionado))
                        {
                            int porGrupo = usarAvanzadas ? valorLimite : 1;
                            var grupos = new Dictionary<string, List<Element>>();
                            foreach (var elem in elementos)
                            {
                                string valor = ObtenerValorParametro(elem, parametroSeleccionado);
                                if (string.IsNullOrEmpty(valor)) continue;
                                if (!grupos.ContainsKey(valor)) grupos[valor] = new List<Element>();
                                grupos[valor].Add(elem);
                            }
                            foreach (var grupo in grupos.Values)
                            {
                                var ordenados = grupo.OrderByDescending(e => ObtenerLongitudPies(e)).ToList();
                                elementosAEtiquetar.AddRange(ordenados.Take(porGrupo));
                            }
                        }
                        else if (modo == 3)
                        {
                            int porGrupo = usarAvanzadas ? valorLimite : 1;
                            var grupos = AgruparPorTipoYTamano(elementos);
                            foreach (var grupo in grupos)
                            {
                                var ordenados = grupo.OrderByDescending(e => ObtenerLongitudPies(e)).ToList();
                                elementosAEtiquetar.AddRange(ordenados.Take(porGrupo));
                            }
                        }

                        var existingTags = new FilteredElementCollector(_doc, view.Id).OfClass(typeof(IndependentTag)).Cast<IndependentTag>().ToList();
                        var placedBBs = existingTags.Select(t => t.get_BoundingBox(view)).Where(bb => bb != null).ToList();

                        foreach (var elem in elementosAEtiquetar)
                        {
                            if (_cancelarProceso) break;

                            bool yaExiste = false;
                            foreach (var tag in existingTags)
                            {
                                if (tag.GetTypeId() != _tipoEtiquetaSeleccionadoId) continue;
                                if (tag.GetTaggedLocalElementIds().Contains(elem.Id)) { yaExiste = true; break; }
                            }
                            if (yaExiste) continue;

                            XYZ punto = ObtenerPuntoElemento(elem, view);
                            if (punto == null) continue;

                            var loc = EncontrarUbicacionLibreOptimizado(punto, placedBBs, view, elem, usarLider);
                            if (loc == null) loc = punto;

                            try
                            {
                                var refElem = new Reference(elem);
                                var tag = IndependentTag.Create(_doc, view.Id, refElem, usarLider, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, loc);
                                tag.ChangeTypeId(_tipoEtiquetaSeleccionadoId);
                                placedBBs.Add(tag.get_BoundingBox(view));
                                totalEtiquetas++;
                            }
                            catch { }
                        }
                    }
                    if (!_cancelarProceso)
                        t.Commit();
                    else
                        t.RollBack();
                }
                if (_cancelarProceso)
                {
                    txtResultado.Text = "Proceso cancelado por el usuario.";
                    MessageBox.Show("Proceso cancelado.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    txtResultado.Text = $"Etiquetado completado. Se colocaron {totalEtiquetas} etiquetas.";
                    MessageBox.Show($"Etiquetado completado. Se colocaron {totalEtiquetas} etiquetas.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
            txtResultado.Text = "Cancelando... (puede tardar unos segundos)";
        }

        private XYZ ObtenerPuntoElemento(Element elem, View view)
        {
            if (elem.Location is LocationCurve lc && lc.Curve != null)
                return lc.Curve.Evaluate(0.5, true);
            if (elem.Location is LocationPoint lp)
                return lp.Point;
            var bb = elem.get_BoundingBox(view);
            if (bb != null)
                return (bb.Min + bb.Max) / 2;
            return null;
        }

        private XYZ EncontrarUbicacionLibreOptimizado(XYZ basePoint, List<BoundingBoxXYZ> existingBBs, View view, Element element, bool hasLeader)
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
                        var tempTag = IndependentTag.Create(_doc, view.Id, refElem, hasLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, newPoint);
                        var bb = tempTag.get_BoundingBox(view);
                        _doc.Delete(tempTag.Id);
                        if (!existingBBs.Any(existing => existing != null && bb != null &&
                            existing.Min.X < bb.Max.X && existing.Max.X > bb.Min.X &&
                            existing.Min.Y < bb.Max.Y && existing.Max.Y > bb.Min.Y))
                            return newPoint;
                    }
                    catch { }
                }
            }
            return null;
        }

        private string ObtenerValorParametro(Element elem, string nombreParam)
        {
            var param = elem.LookupParameter(nombreParam);
            if (param == null && elem.GetTypeId() != ElementId.InvalidElementId)
            {
                var tipo = _doc.GetElement(elem.GetTypeId()) as ElementType;
                if (tipo != null) param = tipo.LookupParameter(nombreParam);
            }
            if (param != null && param.HasValue && !string.IsNullOrEmpty(param.AsString()))
                return param.AsString();
            return null;
        }

        private List<List<Element>> AgruparPorTipoYTamano(List<Element> elementos)
        {
            var grupos = new Dictionary<string, List<Element>>();
            foreach (var elem in elementos)
            {
                string tamano = ObtenerParametroTamano(elem);
                if (string.IsNullOrEmpty(tamano)) continue;
                string typeId = elem.GetTypeId().ToString();
                string clave = $"{typeId}_{tamano}";
                if (!grupos.ContainsKey(clave))
                    grupos[clave] = new List<Element>();
                grupos[clave].Add(elem);
            }
            return grupos.Values.ToList();
        }

        private string ObtenerParametroTamano(Element elem)
        {
            Parameter p = elem.LookupParameter("Tamaño");
            if (p == null) p = elem.LookupParameter("Size");
            if (p == null && elem.GetTypeId() != ElementId.InvalidElementId)
            {
                var tipo = _doc.GetElement(elem.GetTypeId()) as ElementType;
                if (tipo != null)
                {
                    p = tipo.LookupParameter("Tamaño");
                    if (p == null) p = tipo.LookupParameter("Size");
                }
            }
            if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsString()))
                return p.AsString();
            return null;
        }
    }
}