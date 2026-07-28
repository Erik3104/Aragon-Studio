using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para GeneradorDeAcabadosWindow.xaml
    /// </summary>
    public partial class GeneradorDeAcabadosWindow : Window
    {
        public GeneradorDeAcabadosWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Generando acabados arquitectónicos...",
                "Generador de Acabados",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí irá la lógica para aplicar materiales o parámetros de acabados
            // a los elementos arquitectónicos seleccionados en el documento Revit.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
