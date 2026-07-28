using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para GeneradorDeAcerosWindow.xaml
    /// </summary>
    public partial class GeneradorDeAcerosWindow : Window
    {
        public GeneradorDeAcerosWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando la generación de elementos de acero estructural...",
                "Generador de Aceros",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí irá la lógica para crear aceros estructurales
            // usando la API de Revit (por ejemplo, FamilyInstance de vigas, columnas, etc.)
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
