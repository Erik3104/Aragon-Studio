using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para CorreccionOrtograficaWindow.xaml
    /// </summary>
    public partial class CorreccionOrtograficaWindow : Window
    {
        public CorreccionOrtograficaWindow()
        {
            InitializeComponent();
        }

        private void Analizar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analizando textos del proyecto en busca de errores ortográficos...",
                "Análisis ortográfico", MessageBoxButton.OK, MessageBoxImage.Information);

            // Aquí se implementará el análisis real de textos (placeholder)
            MessageBox.Show("Análisis completado. Se detectaron algunos posibles errores.",
                "Resultados", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Corregir_Click(object sender, RoutedEventArgs e)
        {
            // Aquí se aplicaría la lógica para corregir automáticamente los textos detectados.
            MessageBox.Show("Correcciones aplicadas exitosamente.",
                "Corrección completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
