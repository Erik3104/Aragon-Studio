using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using AragonStudio.Services.Guardado;

namespace AragonStudio.UI.Gestión.Guardado
{
    public partial class GuardadoWindow : Window
    {
        private readonly UIApplication _uiApp;

        public GuardadoWindow(UIApplication uiApp)
        {
            InitializeComponent();

            try
            {
                Uri iconUri = new Uri("pack://application:,,,/AragonStudio;component/Resources/Icons/SvgIcons/Logo.ico", UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
            }
            catch { this.Icon = null; }

            _uiApp = uiApp;

            try
            {
                var doc = _uiApp.ActiveUIDocument?.Document;
                if (doc != null)
                {
                    var (freq, activo) = RecordatorioService.ObtenerConfig(doc);
                    chkActivo.IsChecked = activo;
                    txtFrecuencia.Text = freq.ToString();
                }
                ActualizarPesoArchivo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarPesoArchivo()
        {
            long? peso = null;
            var doc = _uiApp.ActiveUIDocument?.Document;
            if (doc != null && !string.IsNullOrEmpty(doc.PathName) && System.IO.File.Exists(doc.PathName))
            {
                peso = new System.IO.FileInfo(doc.PathName).Length;
            }
            txtPesoArchivo.Text = peso.HasValue ? $"Proyecto: {RecordatorioService.FormatearPeso(peso.Value)}" : "Proyecto: No guardado aún.";
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            var doc = _uiApp.ActiveUIDocument?.Document;
            if (doc == null)
            {
                MessageBox.Show("No hay documento activo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (int.TryParse(txtFrecuencia.Text, out int freq))
            {
                RecordatorioService.GuardarConfig(doc, freq, chkActivo.IsChecked == true);
                this.Close();
            }
            else
            {
                MessageBox.Show("Frecuencia debe ser número entero.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}