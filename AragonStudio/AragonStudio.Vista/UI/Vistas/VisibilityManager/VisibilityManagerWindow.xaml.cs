using AragonStudio.Enums.VisibilityManager;
using AragonStudio.Models.VisibilityManager;
using AragonStudio.RevitAPI.VisibilityManager;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VisibilityDataService = AragonStudio.Services.VisibilityManager.VisibilityDataService;
using VisibilityExecutionService = AragonStudio.Services.VisibilityManager.VisibilityExecutionService;

namespace AragonStudio.UI.VisibilityManager
{
    public partial class VisibilityManagerWindow : Window
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly VisibilityDataService _dataService;
        private readonly VisibilityExecutionService _executionService;
        private readonly VisibilityExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private IList<ElementId> _selectedIds = new List<ElementId>();
        private List<ViewItem> _viewItems;
        private bool _isProcessing = false;

        public VisibilityManagerWindow(UIApplication uiApp)
        {
            try
            {
                InitializeComponent();

                if (uiApp?.ActiveUIDocument == null)
                {
                    TaskDialog.Show("Error", "No hay documento activo.");
                    Close();
                    return;
                }

                _uiApp = uiApp;
                _uiDoc = uiApp.ActiveUIDocument;
                _dataService = new VisibilityDataService(uiApp);
                _executionService = new VisibilityExecutionService();
                _handler = new VisibilityExternalEventHandler();
                _externalEvent = ExternalEvent.Create(_handler);

                _handler.OnProgress = (msg) => Dispatcher.Invoke(() =>
                {
                    if (msg.StartsWith("Progreso:"))
                    {
                        var val = msg.Replace("Progreso:", "").Replace("%", "").Trim();
                        if (int.TryParse(val, out int p))
                            progressBar.Value = p;
                    }
                });
                _handler.OnStatus = (msg) => Dispatcher.Invoke(() => { lblStatus.Text = msg; });

                rbOcultar.Checked += RadioButton_Checked;
                rbMostrar.Checked += RadioButton_Checked;
                rbSoloElementos.Checked += RadioButton_Checked;
                rbMismoTipo.Checked += RadioButton_Checked;
                rbCategoria.Checked += RadioButton_Checked;

                SetInitialSummary();
                LoadViews();
                UpdateSummary();
                SetWindowIcon();
                SetupTreeView();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al inicializar: {ex.Message}");
                Close();
            }
        }

        private void SetupTreeView()
        {
            // Crear el DataTemplate programáticamente para evitar errores de XAML
            var template = new HierarchicalDataTemplate();
            template.DataType = typeof(ViewItem);
            template.ItemsSource = new System.Windows.Data.Binding("Children");

            var factory = new System.Windows.FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("Name"));
            factory.SetValue(CheckBox.ForegroundProperty, System.Windows.Media.Brushes.White);
            factory.SetValue(CheckBox.FontSizeProperty, 11.0);
            factory.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(CheckBox_Checked));
            factory.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(CheckBox_Unchecked));

            template.VisualTree = factory;
            tvViews.ItemTemplate = template;
        }

        private void SetInitialSummary()
        {
            resModo.Text = "Ocultar";
            resElementos.Text = "0";
            resCategorias.Text = "Ninguna";
            resTipo.Text = "Ninguno";
            resVistas.Text = "0";
            resAccion.Text = "Solo elementos";
        }

        private void SetWindowIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                Icon = new BitmapImage(iconUri);
            }
            catch { }
        }

        private void LoadViews()
        {
            try
            {
                _viewItems = _dataService.GetViewGroups() ?? new List<ViewItem>();
                tvViews.ItemsSource = _viewItems;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al cargar vistas: {ex.Message}");
                _viewItems = new List<ViewItem>();
                tvViews.ItemsSource = _viewItems;
            }
        }

        private void SelectElements_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hide();
                var sel = _uiDoc.Selection;
                var references = sel.PickObjects(ObjectType.Element,
                    "Seleccione elementos. Use ventana de selección o clic. Presione Enter o clic derecho para finalizar.");

                _selectedIds = references?.Select(r => r.ElementId).ToList() ?? new List<ElementId>();
                lblSelectedCount.Text = $"Elementos seleccionados: {_selectedIds.Count}";
                UpdateSummary();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                _selectedIds = new List<ElementId>();
                lblSelectedCount.Text = "Elementos seleccionados: 0";
                UpdateSummary();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al seleccionar: {ex.Message}");
            }
            finally
            {
                Show();
            }
        }

        private void SelectAllViews_Click(object sender, RoutedEventArgs e)
        {
            SetAllViewsSelected(true);
            UpdateSummary();
        }

        private void ClearAllViews_Click(object sender, RoutedEventArgs e)
        {
            SetAllViewsSelected(false);
            UpdateSummary();
        }

        private void SetAllViewsSelected(bool selected)
        {
            if (_viewItems == null) return;
            foreach (var group in _viewItems)
            {
                group.IsSelected = selected;
                foreach (var child in group.Children)
                    child.IsSelected = selected;
            }
            tvViews.ItemsSource = null;
            tvViews.ItemsSource = _viewItems;
        }

        private void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            if (_selectedIds == null || _selectedIds.Count == 0)
            {
                TaskDialog.Show("Aviso", "Seleccione al menos un elemento.");
                return;
            }

            var selectedViews = GetSelectedViews();
            if (selectedViews.Count == 0)
            {
                TaskDialog.Show("Aviso", "Seleccione al menos una vista de destino.");
                return;
            }

            var isHide = rbOcultar.IsChecked == true;
            var action = isHide ? VisibilityActionType.Hide : VisibilityActionType.Unhide;
            ScopeType scope;
            if (rbSoloElementos.IsChecked == true)
                scope = ScopeType.SelectedOnly;
            else if (rbMismoTipo.IsChecked == true)
                scope = ScopeType.SameType;
            else if (rbCategoria.IsChecked == true)
                scope = ScopeType.Category;
            else
                scope = ScopeType.SelectedOnly;

            _isProcessing = true;
            btnApply.IsEnabled = false;
            progressBar.Value = 0;
            lblStatus.Text = "Procesando...";

            try
            {
                var request = _executionService.BuildRequest(_selectedIds, selectedViews, action, scope);
                _handler.Request = request;
                _externalEvent.Raise();

                System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    btnApply.IsEnabled = true;
                    _isProcessing = false;
                    if (lblStatus.Text == "Procesando...")
                        lblStatus.Text = "Listo";
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al aplicar los cambios:\n{ex.Message}");
                lblStatus.Text = "Error.";
                btnApply.IsEnabled = true;
                _isProcessing = false;
            }
        }

        private List<View> GetSelectedViews()
        {
            var views = new List<View>();
            if (_viewItems == null) return views;
            foreach (var group in _viewItems)
                foreach (var child in group.Children)
                    if (child.IsSelected && child.View != null)
                        views.Add(child.View);
            return views;
        }

        private void UpdateSummary()
        {
            try
            {
                if (resModo == null || resElementos == null || resCategorias == null ||
                    resTipo == null || resVistas == null || resAccion == null)
                    return;

                resModo.Text = rbOcultar.IsChecked == true ? "Ocultar" : "Mostrar";

                if (_selectedIds != null && _selectedIds.Count > 0)
                {
                    var info = _dataService.GetSelectionInfo(_selectedIds);
                    resElementos.Text = info.count.ToString();
                    resCategorias.Text = info.categoryName;
                    resTipo.Text = info.typeName;
                }
                else
                {
                    resElementos.Text = "0";
                    resCategorias.Text = "Ninguna";
                    resTipo.Text = "Ninguno";
                }

                var selectedViews = GetSelectedViews();
                resVistas.Text = selectedViews.Count.ToString();

                if (rbSoloElementos.IsChecked == true)
                    resAccion.Text = "Solo elementos";
                else if (rbMismoTipo.IsChecked == true)
                    resAccion.Text = "Todos los del mismo tipo";
                else if (rbCategoria.IsChecked == true)
                    resAccion.Text = "Categoría completa";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSummary error: {ex.Message}");
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSummary();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !_isProcessing)
                Close();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext is ViewItem item)
            {
                if (item.Children.Any())
                {
                    foreach (var child in item.Children)
                        child.IsSelected = true;
                }
                UpdateSummary();
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb?.DataContext is ViewItem item)
            {
                if (item.Children.Any())
                {
                    foreach (var child in item.Children)
                        child.IsSelected = false;
                }
                UpdateSummary();
            }
        }
    }
}