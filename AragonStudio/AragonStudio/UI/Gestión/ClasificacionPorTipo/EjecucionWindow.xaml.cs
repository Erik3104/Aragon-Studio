using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfVisibility = System.Windows.Visibility;

namespace AragonStudio.UI.Gestión.ClasificacionPorTipo
{
    public partial class EjecucionWindow : Window
    {
        public ConfigData Config { get; }
        public ElementId Type1Id { get; }
        public ElementId Type2Id { get; }
        private UIApplication _uiApp;
        private Document _doc;

        public EjecucionWindow(UIApplication uiApp, ConfigData config, ElementId type1Id, ElementId type2Id)
        {
            InitializeComponent();
            try
            {
                Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
            }
            catch { this.Icon = null; }

            Config = config;
            Type1Id = type1Id;
            Type2Id = type2Id;
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;

            txtResumenCategoria.Text = $"Categoría: {Config.Categoria}";
            txtResumenParametro.Text = $"Parámetro: {Config.Parametro}";
            txtResumenPrefijo.Text = $"Prefijo: {Config.Prefijo}";
            txtResumenNiveles.Text = $"Niveles: {string.Join(" → ", Config.Niveles)}";
            var tipo1 = _doc.GetElement(Type1Id) as ElementType;
            var tipo2 = _doc.GetElement(Type2Id) as ElementType;
            txtResumenBase.Text = $"Base 1: {tipo1?.FamilyName ?? tipo1?.Name} : {tipo1?.Name}  |  Base 2: {tipo2?.FamilyName ?? tipo2?.Name} : {tipo2?.Name}";
        }

        private void BtnEjecutar_Click(object sender, RoutedEventArgs e)
        {
            btnEjecutar.IsEnabled = false;
            progressBar.Visibility = WpfVisibility.Visible;
            txtResultado.Text = "Clasificando...";

            try
            {
                EjecutarClasificacion();
                progressBar.Visibility = WpfVisibility.Collapsed;
                btnEjecutar.Visibility = WpfVisibility.Collapsed;
                btnCerrar.Visibility = WpfVisibility.Visible;
                txtResultado.Text = "Operación completada correctamente.";
            }
            catch (Exception ex)
            {
                progressBar.Visibility = WpfVisibility.Collapsed;
                btnEjecutar.IsEnabled = true;
                txtResultado.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EjecutarClasificacion()
        {
            var category = _doc.Settings.Categories.Cast<Category>().First(c => c.Name == Config.Categoria);

            var levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            var levelMap = levels.ToDictionary(l => l.Name, l => l.Id);

            var collector = new FilteredElementCollector(_doc).OfCategoryId(category.Id).WhereElementIsNotElementType();
            var instances = collector.Cast<Element>().Where(e => e.GetTypeId() != null && e.GetTypeId() != ElementId.InvalidElementId).ToList();

            if (instances.Count == 0)
            {
                throw new InvalidOperationException($"No se encontraron elementos en la categoría '{Config.Categoria}'.");
            }

            var symbolToInstances = new Dictionary<ElementId, List<Element>>();
            foreach (var inst in instances)
            {
                var symId = inst.GetTypeId();
                if (!symbolToInstances.ContainsKey(symId))
                    symbolToInstances[symId] = new List<Element>();
                symbolToInstances[symId].Add(inst);
            }

            var typeSequence = new Dictionary<ElementId, string>();
            var classifiedTypes = new HashSet<ElementId>();

            typeSequence[Type1Id] = Config.Prefijo + "1";
            typeSequence[Type2Id] = Config.Prefijo + "2";
            classifiedTypes.Add(Type1Id);
            classifiedTypes.Add(Type2Id);
            int counter = 3;

            foreach (var levelName in Config.Niveles)
            {
                if (!levelMap.ContainsKey(levelName)) continue;
                var levelId = levelMap[levelName];
                foreach (var symId in symbolToInstances.Keys)
                {
                    if (classifiedTypes.Contains(symId)) continue;
                    if (symbolToInstances[symId].Any(inst => inst.LevelId == levelId))
                    {
                        typeSequence[symId] = Config.Prefijo + counter.ToString();
                        counter++;
                        classifiedTypes.Add(symId);
                    }
                }
            }

            foreach (var symId in symbolToInstances.Keys)
            {
                if (!classifiedTypes.Contains(symId))
                {
                    typeSequence[symId] = Config.Prefijo + counter.ToString();
                    counter++;
                }
            }

            using (var t = new Transaction(_doc, "Clasificación por Tipo"))
            {
                t.Start();
                foreach (var kvp in typeSequence)
                {
                    var elementType = _doc.GetElement(kvp.Key) as ElementType;
                    if (elementType == null) continue;
                    var param = elementType.LookupParameter(Config.Parametro);
                    if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                        param.Set(kvp.Value);
                }
                t.Commit();
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}