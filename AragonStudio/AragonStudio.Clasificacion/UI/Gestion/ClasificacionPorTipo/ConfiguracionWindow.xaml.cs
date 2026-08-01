using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using AragonStudio.UI.Gestión.ClasificacionPorTipo;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AragonStudio.UI.Gestión.ClasificacionPorTipo
{
    public partial class ConfiguracionWindow : Window
    {
        private UIApplication _uiApp;
        private Document _doc;
        private List<Category> _categorias;
        private List<string> _todosLosNiveles;
        private List<string> _nivelesSeleccionados;
        private string _configPath;

        public ConfigData Config { get; private set; }

        public ConfiguracionWindow(UIApplication uiApp)
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
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AragonStudio", "ClasificacionPorTipo", $"{_doc.Title}_config.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
            CargarCategorias();
            CargarConfiguracion();
        }

        private void CargarCategorias()
        {
            _categorias = _doc.Settings.Categories.Cast<Category>()
                .Where(c => c.CategoryType == CategoryType.Model && !c.IsTagCategory)
                .OrderBy(c => c.Name).ToList();
            cmbCategorias.ItemsSource = _categorias.Select(c => c.Name).ToList();
        }

        private void CargarConfiguracion()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<ConfigData>(json);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.Categoria)) cmbCategorias.SelectedItem = config.Categoria;
                        txtPrefijo.Text = config.Prefijo ?? "P";
                        if (!string.IsNullOrEmpty(config.Parametro)) CargarParametrosPorCategoria(config.Categoria, config.Parametro);
                        if (config.Niveles != null) _nivelesSeleccionados = config.Niveles;
                    }
                }
                catch { }
            }
            CargarNiveles();
        }

        private void GuardarConfiguracion()
        {
            try
            {
                var config = new ConfigData
                {
                    Categoria = cmbCategorias.SelectedItem?.ToString(),
                    Parametro = cmbParametro.SelectedItem?.ToString(),
                    Prefijo = txtPrefijo.Text,
                    Niveles = _nivelesSeleccionados
                };
                File.WriteAllText(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void CmbCategorias_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbCategorias.SelectedIndex >= 0)
            {
                var catName = cmbCategorias.SelectedItem.ToString();
                var category = _categorias.First(c => c.Name == catName);
                var collector = new FilteredElementCollector(_doc).OfCategoryId(category.Id).WhereElementIsElementType().FirstOrDefault();
                if (collector != null)
                {
                    var parameters = collector.Parameters.Cast<Parameter>()
                        .Where(p => p.StorageType == StorageType.String && !p.IsReadOnly)
                        .Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();
                    cmbParametro.ItemsSource = parameters;
                    if (parameters.Any()) cmbParametro.SelectedIndex = 0;
                }
                CargarNiveles();
                btnSiguiente.IsEnabled = true;
            }
        }

        private void CargarParametrosPorCategoria(string categoria, string seleccionado)
        {
            var cat = _categorias.FirstOrDefault(c => c.Name == categoria);
            if (cat != null)
            {
                var collector = new FilteredElementCollector(_doc).OfCategoryId(cat.Id).WhereElementIsElementType().FirstOrDefault();
                if (collector != null)
                {
                    var parameters = collector.Parameters.Cast<Parameter>()
                        .Where(p => p.StorageType == StorageType.String && !p.IsReadOnly)
                        .Select(p => p.Definition.Name).Distinct().OrderBy(n => n).ToList();
                    cmbParametro.ItemsSource = parameters;
                    if (!string.IsNullOrEmpty(seleccionado) && parameters.Contains(seleccionado))
                        cmbParametro.SelectedItem = seleccionado;
                }
            }
        }

        private void CargarNiveles()
        {
            var levels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            _todosLosNiveles = levels.Select(l => l.Name).OrderBy(n => n).ToList();
            if (_nivelesSeleccionados == null) _nivelesSeleccionados = new List<string>();
            else _nivelesSeleccionados = _nivelesSeleccionados.Intersect(_todosLosNiveles).ToList();
            ActualizarListas();
        }

        private void ActualizarListas()
        {
            lstDisponibles.Items.Clear();
            lstSeleccionados.Items.Clear();
            foreach (var n in _todosLosNiveles.Except(_nivelesSeleccionados).OrderBy(n => n))
                lstDisponibles.Items.Add(n);
            foreach (var n in _nivelesSeleccionados)
                lstSeleccionados.Items.Add(n);
        }

        private void AgregarNiveles_Click(object sender, RoutedEventArgs e)
        {
            foreach (string n in lstDisponibles.SelectedItems)
                if (!_nivelesSeleccionados.Contains(n)) _nivelesSeleccionados.Add(n);
            ActualizarListas();
        }

        private void QuitarNiveles_Click(object sender, RoutedEventArgs e)
        {
            foreach (string n in lstSeleccionados.SelectedItems)
                _nivelesSeleccionados.Remove(n);
            ActualizarListas();
        }

        private void SubirNivel_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstSeleccionados.SelectedIndex;
            if (idx > 0)
            {
                var item = lstSeleccionados.Items[idx];
                lstSeleccionados.Items.RemoveAt(idx);
                lstSeleccionados.Items.Insert(idx - 1, item);
                lstSeleccionados.SelectedIndex = idx - 1;
                _nivelesSeleccionados = lstSeleccionados.Items.Cast<string>().ToList();
            }
        }

        private void BajarNivel_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstSeleccionados.SelectedIndex;
            if (idx >= 0 && idx < lstSeleccionados.Items.Count - 1)
            {
                var item = lstSeleccionados.Items[idx];
                lstSeleccionados.Items.RemoveAt(idx);
                lstSeleccionados.Items.Insert(idx + 1, item);
                lstSeleccionados.SelectedIndex = idx + 1;
                _nivelesSeleccionados = lstSeleccionados.Items.Cast<string>().ToList();
            }
        }

        private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCategorias.SelectedItem == null) { MessageBox.Show("Selecciona una categoría."); return; }
            if (cmbParametro.SelectedItem == null) { MessageBox.Show("Selecciona un parámetro."); return; }
            if (string.IsNullOrWhiteSpace(txtPrefijo.Text)) { MessageBox.Show("Ingresa un prefijo."); return; }
            if (_nivelesSeleccionados.Count == 0) { MessageBox.Show("Selecciona al menos un nivel."); return; }

            GuardarConfiguracion();
            Config = new ConfigData
            {
                Categoria = cmbCategorias.SelectedItem.ToString(),
                Parametro = cmbParametro.SelectedItem.ToString(),
                Prefijo = txtPrefijo.Text,
                Niveles = _nivelesSeleccionados
            };
            this.DialogResult = true;
            this.Close();
        }
    }
}