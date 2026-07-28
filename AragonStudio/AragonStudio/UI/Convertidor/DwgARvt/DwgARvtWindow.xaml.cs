using AragonStudio.Services.dwgArevit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawingLine = System.Windows.Shapes.Line;
using MediaColor = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace AragonStudio.UI.Convertidor.DwgARvt
{
    public partial class DwgARvtWindow : Window
    {
        private ScanResult _scan;
        private Document _doc;
        private ObservableCollection<LayerMappingItem> _layerItems;

        private bool _isPanning;
        private Point _lastMousePosition;

        public Level LevelBase => cmbLevelBase.SelectedItem as Level;
        public Level LevelTop => cmbLevelTop.SelectedItem as Level;
        public double DoorHeight => ParseDouble(txtDoorHeight.Text, 2.10);
        public double WindowHeight => ParseDouble(txtWindowHeight.Text, 1.20);
        public double SillHeight => ParseDouble(txtSillHeight.Text, 0.90);
        public double BeamHeight => ParseDouble(txtBeamHeight.Text, 0.30);
        public double BeamOffset => ParseDouble(txtBeamOffset.Text, 0.00);
        public double FootingThickness => ParseDouble(txtFootingThick.Text, 0.30);
        public double FloorThickness => ParseDouble(txtFloorThick.Text, 0.20);

        private double ParseDouble(string text, double defaultValue)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return value;
            return defaultValue;
        }

        public DwgARvtWindow(ScanResult scan, Document doc)
        {
            InitializeComponent();
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
            }
            catch { }

            _scan = scan;
            _doc = doc;

            int closedTotal = scan.PolylinesByLayer.Values.Sum(list => list.Count(p => p.IsClosed));
            int openTotal = scan.PolylinesByLayer.Values.Sum(list => list.Count(p => !p.IsClosed));
            int circlesTotal = scan.CirclesByLayer.Values.Sum(list => list.Count);
            int blocksTotal = scan.BlocksByLayer.Values.Sum(list => list.Count);
            txtStats.Text = $"Cerradas: {closedTotal} | Abiertas: {openTotal} | Círculos: {circlesTotal} | Bloques: {blocksTotal}";

            List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            cmbLevelBase.ItemsSource = levels;
            cmbLevelTop.ItemsSource = levels;
            if (levels.Count > 0) cmbLevelBase.SelectedIndex = 0;
            if (levels.Count > 1) cmbLevelTop.SelectedIndex = 1; else cmbLevelTop.SelectedIndex = 0;

            _layerItems = new ObservableCollection<LayerMappingItem>();
            foreach (string layer in scan.Layers)
            {
                int closed = scan.PolylinesByLayer.ContainsKey(layer) ? scan.PolylinesByLayer[layer].Count(p => p.IsClosed) : 0;
                int open = scan.PolylinesByLayer.ContainsKey(layer) ? scan.PolylinesByLayer[layer].Count(p => !p.IsClosed) : 0;
                int circles = scan.CirclesByLayer.ContainsKey(layer) ? scan.CirclesByLayer[layer].Count : 0;
                int blocks = scan.BlocksByLayer.ContainsKey(layer) ? scan.BlocksByLayer[layer].Count : 0;
                MediaColor color = GetLayerColor(layer);
                _layerItems.Add(new LayerMappingItem(layer, closed, open, circles, blocks, doc, color));
            }
            lvLayers.ItemsSource = _layerItems;

            foreach (var item in _layerItems)
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LayerMappingItem.SelectedCategory))
                        RefreshPreview();
                };
            RefreshPreview();
        }

        private MediaColor GetLayerColor(string layerName)
        {
            if (_scan.PolylinesByLayer.ContainsKey(layerName) && _scan.PolylinesByLayer[layerName].Count > 0)
                return _scan.PolylinesByLayer[layerName][0].Color;
            if (_scan.CirclesByLayer.ContainsKey(layerName) && _scan.CirclesByLayer[layerName].Count > 0)
                return _scan.CirclesByLayer[layerName][0].Color;
            if (_scan.BlocksByLayer.ContainsKey(layerName) && _scan.BlocksByLayer[layerName].Count > 0)
                return _scan.BlocksByLayer[layerName][0].Color;
            return Colors.Gray;
        }

        private void RefreshPreview()
        {
            previewCanvas.Children.Clear();

            var selectedLayers = _layerItems.Where(item => item.SelectedCategory != null && item.SelectedCategory.DisplayName != "Ninguno")
                                             .Select(item => item.LayerName)
                                             .ToList();
            if (!selectedLayers.Any()) return;

            var allPolylines = new List<CadPolyline>();
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;

            foreach (string layer in selectedLayers)
            {
                if (_scan.PolylinesByLayer.TryGetValue(layer, out var polys))
                {
                    allPolylines.AddRange(polys);
                    foreach (var poly in polys)
                        foreach (var pt in poly.Points)
                        {
                            minX = Math.Min(minX, pt.X);
                            maxX = Math.Max(maxX, pt.X);
                            minY = Math.Min(minY, pt.Y);
                            maxY = Math.Max(maxY, pt.Y);
                        }
                }
            }

            if (allPolylines.Count == 0) return;

            double width = maxX - minX;
            double height = maxY - minY;
            double scale = Math.Min(330 / width, 330 / height);
            double offsetX = 15 + (330 - width * scale) / 2;
            double offsetY = 15 + (330 - height * scale) / 2;

            previewScaleTransform.ScaleX = 1;
            previewScaleTransform.ScaleY = 1;
            previewTranslateTransform.X = 0;
            previewTranslateTransform.Y = 0;

            foreach (var poly in allPolylines)
            {
                for (int i = 0; i < poly.Points.Count - 1; i++)
                {
                    var p1 = poly.Points[i];
                    var p2 = poly.Points[i + 1];
                    double x1 = (p1.X - minX) * scale + offsetX;
                    double y1 = (p1.Y - minY) * scale + offsetY;
                    double x2 = (p2.X - minX) * scale + offsetX;
                    double y2 = (p2.Y - minY) * scale + offsetY;
                    var line = new DrawingLine
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = new SolidColorBrush(poly.Color),
                        StrokeThickness = 1.5
                    };
                    previewCanvas.Children.Add(line);
                }
            }

            previewCanvas.Width = (maxX - minX) * scale + 30;
            previewCanvas.Height = (maxY - minY) * scale + 30;
        }

        private void PreviewCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = 1.1;
            double scale = previewScaleTransform.ScaleX;
            if (e.Delta > 0)
                scale *= zoomFactor;
            else if (e.Delta < 0)
                scale /= zoomFactor;
            scale = Math.Max(0.1, Math.Min(10, scale));
            previewScaleTransform.ScaleX = scale;
            previewScaleTransform.ScaleY = scale;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(previewScrollViewer);
            previewCanvas.CaptureMouse();
        }

        private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPos = e.GetPosition(previewScrollViewer);
                double deltaX = currentPos.X - _lastMousePosition.X;
                double deltaY = currentPos.Y - _lastMousePosition.Y;
                previewTranslateTransform.X += deltaX;
                previewTranslateTransform.Y += deltaY;
                _lastMousePosition = currentPos;
            }
        }

        private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            previewCanvas.ReleaseMouseCapture();
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

        public List<LayerMappingItem> GetLayerMappings() => _layerItems.ToList();

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (LevelBase == null || LevelTop == null)
            {
                MessageBox.Show("Seleccione los niveles base y superior.", "Aragón Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_layerItems.All(i => i.SelectedCategory?.DisplayName == "Ninguno"))
            {
                MessageBox.Show("Asigne al menos una capa a una categoría BIM.", "Aragón Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // =========================================================================
    // CLASES AUXILIARES
    // =========================================================================

    public class LayerMappingItem : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private bool _loading;

        public string LayerName { get; }
        public int ClosedCount { get; }
        public int OpenCount { get; }
        public int CircleCount { get; }
        public int BlockCount { get; }
        public int ObjectCount => ClosedCount + OpenCount + CircleCount + BlockCount;
        public SolidColorBrush ColorBrush { get; }
        public SolidColorBrush TextColorBrush { get; }

        public List<BimCategory> AvailableCategories { get; }
        public List<FamilyInfo> AvailableFamilies { get; set; }

        private BimCategory _selectedCategory;
        public BimCategory SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (value == null || _selectedCategory == value) return;
                _selectedCategory = value;
                OnPropertyChanged(nameof(SelectedCategory));
                OnPropertyChanged(nameof(IsFamilyEnabled));
                if (!_loading && value.DisplayName != "Ninguno")
                    LoadFamiliesForCategory(value.DisplayName);
                else if (value.DisplayName == "Ninguno")
                    AvailableFamilies.Clear();
            }
        }

        private FamilyInfo _selectedFamily;
        public FamilyInfo SelectedFamily
        {
            get => _selectedFamily;
            set { _selectedFamily = value; OnPropertyChanged(nameof(SelectedFamily)); }
        }

        public bool IsFamilyEnabled => SelectedCategory != null && SelectedCategory.DisplayName != "Ninguno";

        public LayerMappingItem(string layerName, int closed, int open, int circles, int blocks, Document doc, MediaColor color)
        {
            LayerName = layerName;
            ClosedCount = closed;
            OpenCount = open;
            CircleCount = circles;
            BlockCount = blocks;
            _doc = doc;
            ColorBrush = new SolidColorBrush(color);
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            TextColorBrush = luminance > 0.5 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);

            AvailableCategories = new List<BimCategory>();
            if (ClosedCount > 0 || CircleCount > 0)
            {
                AvailableCategories.Add(new BimCategory("Muro", true));
                AvailableCategories.Add(new BimCategory("Columna", true));
                AvailableCategories.Add(new BimCategory("VigaCimentacion", true));
                AvailableCategories.Add(new BimCategory("Suelo", true));
                AvailableCategories.Add(new BimCategory("LosaCimentacion", true));
                AvailableCategories.Add(new BimCategory("Zapata", true));
                AvailableCategories.Add(new BimCategory("Ventana", true));
            }
            if (OpenCount > 0)
            {
                AvailableCategories.Add(new BimCategory("Eje", false));
                AvailableCategories.Add(new BimCategory("Corte", false));
                AvailableCategories.Add(new BimCategory("Tuberia", true));
                AvailableCategories.Add(new BimCategory("Ducto", true));
                AvailableCategories.Add(new BimCategory("Tubo", true)); // NUEVO: Tubos eléctricos
            }
            if (BlockCount > 0)
            {
                AvailableCategories.Add(new BimCategory("Puerta", true));
                AvailableCategories.Add(new BimCategory("Ventana", true));
            }
            if (ObjectCount > 0)
            {
                AvailableCategories.Add(new BimCategory("Texto", false));
            }
            if (AvailableCategories.Count == 0)
                AvailableCategories.Add(new BimCategory("Ninguno", false));
            else
                AvailableCategories.Insert(0, new BimCategory("Ninguno", false));

            AvailableFamilies = new List<FamilyInfo>();
            _loading = true;
            SelectedCategory = AvailableCategories[0];
            _loading = false;
        }

        private void LoadFamiliesForCategory(string category)
        {
            if (_doc == null) return;
            AvailableFamilies.Clear();

            // Para suelos y losas, cargar FloorType
            if (category == "Suelo" || category == "LosaCimentacion")
            {
                var floorTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FloorType))
                    .Cast<FloorType>()
                    .ToList();

                if (category == "Suelo")
                    floorTypes = floorTypes.Where(ft => !ft.Name.ToLower().Contains("foundation") &&
                                                        !ft.Name.ToLower().Contains("structural") &&
                                                        !ft.Name.ToLower().Contains("cimentación")).ToList();
                else
                    floorTypes = floorTypes.Where(ft => ft.Name.ToLower().Contains("foundation") ||
                                                        ft.Name.ToLower().Contains("structural") ||
                                                        ft.Name.ToLower().Contains("cimentación")).ToList();

                if (floorTypes.Any())
                    foreach (var ft in floorTypes)
                        AvailableFamilies.Add(new FamilyInfo(ft.Name, category));
                else
                    AvailableFamilies.Add(new FamilyInfo($"No se encontraron tipos para {category}", category));
            }
            // Para tuberías, cargar PipeType
            else if (category == "Tuberia")
            {
                var pipeTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(PipeType))
                    .Cast<PipeType>()
                    .ToList();
                if (pipeTypes.Any())
                    foreach (var pt in pipeTypes)
                        AvailableFamilies.Add(new FamilyInfo(pt.Name, category));
                else
                    AvailableFamilies.Add(new FamilyInfo($"No se encontraron tipos de tubería", category));
            }
            // Para conductos, cargar DuctType
            else if (category == "Ducto")
            {
                var ductTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(DuctType))
                    .Cast<DuctType>()
                    .ToList();
                if (ductTypes.Any())
                    foreach (var dt in ductTypes)
                        AvailableFamilies.Add(new FamilyInfo(dt.Name, category));
                else
                    AvailableFamilies.Add(new FamilyInfo($"No se encontraron tipos de conducto", category));
            }
            // Para tubos eléctricos, cargar ConduitType
            else if (category == "Tubo")
            {
                var conduitTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ConduitType))
                    .Cast<ConduitType>()
                    .ToList();
                if (conduitTypes.Any())
                    foreach (var ct in conduitTypes)
                        AvailableFamilies.Add(new FamilyInfo(ct.Name, category));
                else
                    AvailableFamilies.Add(new FamilyInfo($"No se encontraron tipos de tubo eléctrico", category));
            }
            // Para el resto, cargar FamilySymbol
            else
            {
                BuiltInCategory bic = category switch
                {
                    "Muro" => BuiltInCategory.OST_Walls,
                    "Columna" => BuiltInCategory.OST_StructuralColumns,
                    "Puerta" => BuiltInCategory.OST_Doors,
                    "Ventana" => BuiltInCategory.OST_Windows,
                    "VigaCimentacion" => BuiltInCategory.OST_StructuralFraming,
                    "Zapata" => BuiltInCategory.OST_StructuralFoundation,
                    "Corte" => BuiltInCategory.OST_Views,
                    "Texto" => BuiltInCategory.OST_TextNotes,
                    _ => BuiltInCategory.INVALID
                };
                if (bic != BuiltInCategory.INVALID)
                {
                    var families = new FilteredElementCollector(_doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(bic)
                        .Cast<FamilySymbol>()
                        .Select(fs => new FamilyInfo(fs.Name, category))
                        .ToList();
                    if (families.Any())
                        AvailableFamilies.AddRange(families);
                    else
                        AvailableFamilies.Add(new FamilyInfo($"No se encontraron familias de {category}", category));
                }
                else
                {
                    AvailableFamilies.Add(new FamilyInfo($"Categoría sin familias", category));
                }
            }
            SelectedFamily = AvailableFamilies.FirstOrDefault();
            OnPropertyChanged(nameof(AvailableFamilies));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BimCategory
    {
        public string DisplayName { get; }
        public bool HasFamily { get; }
        public BimCategory(string name, bool hasFamily) { DisplayName = name; HasFamily = hasFamily; }
    }

    public class FamilyInfo
    {
        public string Name { get; }
        public string Category { get; }
        public FamilyInfo(string name, string category) { Name = name; Category = category; }
    }
}