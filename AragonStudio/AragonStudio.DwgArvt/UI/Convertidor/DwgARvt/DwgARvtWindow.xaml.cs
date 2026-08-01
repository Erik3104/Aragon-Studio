using AragonStudio.Services.dwgArevit;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawingLine = System.Windows.Shapes.Line;
using MediaColor = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace AragonStudio.UI.Convertidor.DwgARvt
{
    public partial class DwgARvtWindow : Window
    {
        private readonly ScanResult _scan;
        private readonly Document _doc;
        private readonly ObservableCollection<LayerMappingItem> _layerItems;
        private readonly List<Level> _levels;

        private bool _isPanning;
        private Point _lastMousePosition;
        private bool _isLoading = true;

        public Level LevelBase => cmbLevelBase.SelectedItem as Level;
        public Level LevelTop => cmbLevelTop.SelectedItem as Level;
        public double DoorHeight => ParseDouble(txtDoorHeight.Text, 2.10);
        public double WindowHeight => ParseDouble(txtWindowHeight.Text, 1.20);
        public double SillHeight => ParseDouble(txtSillHeight.Text, 0.90);
        public double BeamHeight => ParseDouble(txtBeamHeight.Text, 0.30);
        public double BeamOffset => ParseDouble(txtBeamOffset.Text, 0.00);
        public double FootingThickness => ParseDouble(txtFootingThick.Text, 0.30);
        public double FloorThickness => ParseDouble(txtFloorThick.Text, 0.20);

        public DwgARvtWindow(ScanResult scan, Document doc)
        {
            InitializeComponent();

            _scan = scan ?? throw new ArgumentNullException(nameof(scan));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            _levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            cmbLevelBase.ItemsSource = _levels;
            cmbLevelTop.ItemsSource = _levels;

            if (_levels.Count > 0) cmbLevelBase.SelectedIndex = 0;
            if (_levels.Count > 1) cmbLevelTop.SelectedIndex = 1;
            else if (_levels.Count > 0) cmbLevelTop.SelectedIndex = 0;

            _layerItems = new ObservableCollection<LayerMappingItem>();
            foreach (string layer in scan.Layers.OrderBy(l => l))
            {
                int closed = scan.PolylinesByLayer.TryGetValue(layer, out var polys)
                    ? polys.Count(p => p.IsClosed) : 0;
                int open = scan.PolylinesByLayer.TryGetValue(layer, out var polys2)
                    ? polys2.Count(p => !p.IsClosed) : 0;
                int circles = scan.CirclesByLayer.TryGetValue(layer, out var circlesList)
                    ? circlesList.Count : 0;
                int blocks = scan.BlocksByLayer.TryGetValue(layer, out var blocksList)
                    ? blocksList.Count : 0;

                var color = GetLayerColor(layer);
                _layerItems.Add(new LayerMappingItem(layer, closed, open, circles, blocks, doc, color));
            }

            lvLayers.ItemsSource = _layerItems;

            foreach (var item in _layerItems)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LayerMappingItem.SelectedCategory) ||
                        e.PropertyName == nameof(LayerMappingItem.SelectedFamily))
                    {
                        RefreshPreview();
                    }
                };
            }

            UpdateStats();

            _isLoading = false;
            RefreshPreview();
            btnGenerate.IsEnabled = true;
        }

        private MediaColor GetLayerColor(string layerName)
        {
            if (_scan.PolylinesByLayer.TryGetValue(layerName, out var polys) && polys.Count > 0)
                return polys[0].Color;
            if (_scan.CirclesByLayer.TryGetValue(layerName, out var circles) && circles.Count > 0)
                return circles[0].Color;
            if (_scan.BlocksByLayer.TryGetValue(layerName, out var blocks) && blocks.Count > 0)
                return blocks[0].Color;
            return Colors.Gray;
        }

        private void UpdateStats()
        {
            int closedTotal = _layerItems.Sum(i => i.ClosedCount);
            int openTotal = _layerItems.Sum(i => i.OpenCount);
            int circlesTotal = _layerItems.Sum(i => i.CircleCount);
            int blocksTotal = _layerItems.Sum(i => i.BlockCount);
            int layersWithObjects = _layerItems.Count(i => i.ObjectCount > 0);

            txtStats.Text = $"📐 {layersWithObjects} capas activas | " +
                           $"📏 {closedTotal} cerradas | " +
                           $"📐 {openTotal} abiertas | " +
                           $"⭕ {circlesTotal} círculos | " +
                           $"🧩 {blocksTotal} bloques";
        }

        private void RefreshPreview()
        {
            previewCanvas.Children.Clear();

            var selectedItems = _layerItems
                .Where(item => item.SelectedCategory != null &&
                              item.SelectedCategory.DisplayName != "Ninguno")
                .ToList();

            if (!selectedItems.Any()) return;

            var allPoints = new List<XYZ>();
            var polylinesToDraw = new List<(List<XYZ> Points, MediaColor Color, bool IsClosed)>();

            foreach (var item in selectedItems)
            {
                if (_scan.PolylinesByLayer.TryGetValue(item.LayerName, out var polys))
                {
                    foreach (var poly in polys)
                    {
                        if (poly.Points == null || poly.Points.Count < 2) continue;
                        allPoints.AddRange(poly.Points);
                        polylinesToDraw.Add((poly.Points.ToList(), poly.Color, poly.IsClosed));
                    }
                }

                if (_scan.CirclesByLayer.TryGetValue(item.LayerName, out var circles))
                {
                    foreach (var circle in circles)
                    {
                        var circlePoints = new List<XYZ>();
                        int segments = 24;
                        for (int i = 0; i <= segments; i++)
                        {
                            double angle = (2 * Math.PI * i) / segments;
                            double x = circle.Center.X + circle.Radius * Math.Cos(angle);
                            double y = circle.Center.Y + circle.Radius * Math.Sin(angle);
                            circlePoints.Add(new XYZ(x, y, 0));
                        }
                        allPoints.AddRange(circlePoints);
                        polylinesToDraw.Add((circlePoints, circle.Color, true));
                    }
                }

                if (_scan.BlocksByLayer.TryGetValue(item.LayerName, out var blocks))
                {
                    foreach (var block in blocks)
                    {
                        var rectPoints = CreateRectanglePoints(block.Center, block.WidthFeet, block.HeightFeet);
                        allPoints.AddRange(rectPoints);
                        polylinesToDraw.Add((rectPoints, block.Color, true));
                    }
                }
            }

            if (allPoints.Count == 0) return;

            var bbox = GeometryHelper.GetBoundingBox(allPoints);
            double minX = bbox.Min.X;
            double maxX = bbox.Max.X;
            double minY = bbox.Min.Y;
            double maxY = bbox.Max.Y;
            double width = maxX - minX;
            double height = maxY - minY;

            if (width < 0.001 || height < 0.001) return;

            double canvasWidth = previewScrollViewer.ActualWidth - 30;
            double canvasHeight = previewScrollViewer.ActualHeight - 30;

            if (canvasWidth < 10) canvasWidth = 700;
            if (canvasHeight < 10) canvasHeight = 330;

            double scale = Math.Min(canvasWidth / width, canvasHeight / height);
            scale = Math.Min(scale, 50);
            scale = Math.Max(scale, 0.1);

            double offsetX = (canvasWidth - width * scale) / 2;
            double offsetY = (canvasHeight - height * scale) / 2;

            foreach (var (points, color, isClosed) in polylinesToDraw)
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var p1 = points[i];
                    var p2 = points[i + 1];

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
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = isClosed ? 2 : 1.5
                    };
                    previewCanvas.Children.Add(line);
                }
            }

            previewCanvas.Width = canvasWidth;
            previewCanvas.Height = canvasHeight;

            int totalElements = polylinesToDraw.Count;
            txtStats.Text = $"👁️ Mostrando {totalElements} elementos | " +
                           $"📐 {width:F2} x {height:F2} pies | " +
                           $"🔍 Escala: {scale:F2}x";
        }

        private List<XYZ> CreateRectanglePoints(XYZ center, double width, double height)
        {
            return new List<XYZ>
            {
                new XYZ(center.X - width/2, center.Y - height/2, 0),
                new XYZ(center.X + width/2, center.Y - height/2, 0),
                new XYZ(center.X + width/2, center.Y + height/2, 0),
                new XYZ(center.X - width/2, center.Y + height/2, 0),
                new XYZ(center.X - width/2, center.Y - height/2, 0)
            };
        }

        private void PreviewCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            double scale = previewScaleTransform.ScaleX * zoomFactor;
            scale = Math.Max(0.1, Math.Min(10, scale));
            previewScaleTransform.ScaleX = scale;
            previewScaleTransform.ScaleY = scale;
            e.Handled = true;
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(previewScrollViewer);
            previewCanvas.CaptureMouse();
            previewCanvas.Cursor = Cursors.Hand;
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
            previewCanvas.Cursor = Cursors.Arrow;
        }

        private void PreviewCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            previewScaleTransform.ScaleX = 1;
            previewScaleTransform.ScaleY = 1;
            previewTranslateTransform.X = 0;
            previewTranslateTransform.Y = 0;
            RefreshPreview();
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => RefreshPreview();

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (LevelBase == null)
            {
                MessageBox.Show("Por favor selecciona un nivel base.",
                    "Aragón Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_layerItems.All(i => i.SelectedCategory == null ||
                                     i.SelectedCategory.DisplayName == "Ninguno"))
            {
                MessageBox.Show("Asigna al menos una capa a una categoría BIM.",
                    "Aragón Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (e.Key == Key.Enter && btnGenerate.IsEnabled)
            {
                BtnGenerate_Click(sender, e);
            }
        }

        private double ParseDouble(string text, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text)) return defaultValue;
            text = text.Replace('.', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]);
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return Math.Max(0, value);
            return defaultValue;
        }

        public List<LayerMappingItem> GetLayerMappings() => _layerItems.ToList();
    }

    // =========================================================================
    // CLASES AUXILIARES
    // =========================================================================

    public class LayerMappingItem : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private bool _loading;
        private BimCategory _selectedCategory;
        private FamilyInfo _selectedFamily;

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

        public FamilyInfo SelectedFamily
        {
            get => _selectedFamily;
            set
            {
                if (_selectedFamily == value) return;
                _selectedFamily = value;
                OnPropertyChanged(nameof(SelectedFamily));
            }
        }

        public bool IsFamilyEnabled => SelectedCategory != null &&
                                       SelectedCategory.DisplayName != "Ninguno" &&
                                       SelectedCategory.HasFamily;

        public LayerMappingItem(string layerName, int closed, int open, int circles, int blocks,
                                Document doc, MediaColor color)
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
                AvailableCategories.Add(new BimCategory("VigaEstructural", true));
                AvailableCategories.Add(new BimCategory("VigaCimentacion", true));
                AvailableCategories.Add(new BimCategory("Suelo", true));
                AvailableCategories.Add(new BimCategory("LosaCimentacion", true));
                AvailableCategories.Add(new BimCategory("Zapata", true));
                AvailableCategories.Add(new BimCategory("Ventana", true));
            }

            if (OpenCount > 0)
            {
                AvailableCategories.Add(new BimCategory("VigaEstructural", true));
                AvailableCategories.Add(new BimCategory("Eje", false));
                AvailableCategories.Add(new BimCategory("Corte", false));
            }

            if (BlockCount > 0)
            {
                AvailableCategories.Add(new BimCategory("Puerta", true));
                AvailableCategories.Add(new BimCategory("Ventana", true));
            }

            if (AvailableCategories.Count == 0)
                AvailableCategories.Add(new BimCategory("Ninguno", false));
            else
                AvailableCategories.Insert(0, new BimCategory("Ninguno", false));

            AvailableFamilies = new List<FamilyInfo>();
            _loading = true;
            SelectedCategory = AvailableCategories.FirstOrDefault();
            _loading = false;
        }

        private void LoadFamiliesForCategory(string category)
        {
            if (_doc == null) return;
            AvailableFamilies.Clear();

            try
            {
                switch (category)
                {
                    case "Suelo":
                    case "LosaCimentacion":
                        LoadFloorTypes(category);
                        break;
                    case "VigaEstructural":
                        LoadStructuralBeams();
                        break;
                    default:
                        LoadFamilySymbols(category);
                        break;
                }
            }
            catch (Exception ex)
            {
                AvailableFamilies.Add(new FamilyInfo($"Error: {ex.Message}", category));
            }

            SelectedFamily = AvailableFamilies.FirstOrDefault();
            OnPropertyChanged(nameof(AvailableFamilies));
        }

        private void LoadFloorTypes(string category)
        {
            var floorTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .ToList();

            bool isFoundation = category == "LosaCimentacion";
            floorTypes = floorTypes.Where(ft =>
                isFoundation
                    ? ft.Name.ToLower().Contains("foundation") ||
                      ft.Name.ToLower().Contains("structural") ||
                      ft.Name.ToLower().Contains("cimentación")
                    : !ft.Name.ToLower().Contains("foundation") &&
                      !ft.Name.ToLower().Contains("structural") &&
                      !ft.Name.ToLower().Contains("cimentación")
            ).ToList();

            if (floorTypes.Any())
                foreach (var ft in floorTypes)
                    AvailableFamilies.Add(new FamilyInfo(ft.Name, category));
            else
                AvailableFamilies.Add(new FamilyInfo($"No hay tipos para {category}", category));
        }

        private void LoadStructuralBeams()
        {
            var beamTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .ToList();

            if (beamTypes.Any())
                foreach (var bt in beamTypes)
                    AvailableFamilies.Add(new FamilyInfo(bt.Name, "VigaEstructural"));
            else
                AvailableFamilies.Add(new FamilyInfo("No hay familias de vigas estructurales", "VigaEstructural"));
        }

        private void LoadFamilySymbols(string category)
        {
            BuiltInCategory? bic = category switch
            {
                "Muro" => BuiltInCategory.OST_Walls,
                "Columna" => BuiltInCategory.OST_StructuralColumns,
                "Puerta" => BuiltInCategory.OST_Doors,
                "Ventana" => BuiltInCategory.OST_Windows,
                "VigaCimentacion" => BuiltInCategory.OST_StructuralFraming,
                "Zapata" => BuiltInCategory.OST_StructuralFoundation,
                "Corte" => BuiltInCategory.OST_Views,
                "Eje" => BuiltInCategory.OST_Grids,
                _ => null
            };

            if (bic.HasValue && bic.Value != BuiltInCategory.INVALID)
            {
                var families = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(bic.Value)
                    .Cast<FamilySymbol>()
                    .Select(fs => new FamilyInfo(fs.Name, category))
                    .Distinct()
                    .ToList();

                if (families.Any())
                    AvailableFamilies.AddRange(families);
                else
                    AvailableFamilies.Add(new FamilyInfo($"No hay familias de {category}", category));
            }
            else
            {
                AvailableFamilies.Add(new FamilyInfo($"Categoría sin familias", category));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BimCategory
    {
        public string DisplayName { get; }
        public bool HasFamily { get; }

        public BimCategory(string name, bool hasFamily)
        {
            DisplayName = name;
            HasFamily = hasFamily;
        }

        public override bool Equals(object obj)
        {
            return obj is BimCategory other && DisplayName == other.DisplayName;
        }

        public override int GetHashCode() => DisplayName?.GetHashCode() ?? 0;
    }

    public class FamilyInfo
    {
        public string Name { get; }
        public string Category { get; }

        public FamilyInfo(string name, string category)
        {
            Name = name;
            Category = category;
        }

        public override bool Equals(object obj)
        {
            return obj is FamilyInfo other && Name == other.Name && Category == other.Category;
        }

        public override int GetHashCode() => (Name?.GetHashCode() ?? 0) ^ (Category?.GetHashCode() ?? 0);
    }
}