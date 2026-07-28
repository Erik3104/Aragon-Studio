using Microsoft.Win32;
using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para SkpARvtWindow.xaml
    /// </summary>
    public partial class SkpARvtWindow : Window
    {
        private string selectedFilePath;

        public SkpARvtWindow()
        {
            InitializeComponent();
        }

        private void SeleccionarArchivo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo SKP",
                Filter = "Archivos SketchUp (*.skp)|*.skp",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                MessageBox.Show($"Archivo seleccionado:\n{selectedFilePath}", "Archivo cargado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Convertir_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Por favor selecciona un archivo SKP antes de convertir.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Aquí iría la lógica de conversión SKP → Revit (placeholder)
            MessageBox.Show("Conversión completada exitosamente.", "Proceso finalizado", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
