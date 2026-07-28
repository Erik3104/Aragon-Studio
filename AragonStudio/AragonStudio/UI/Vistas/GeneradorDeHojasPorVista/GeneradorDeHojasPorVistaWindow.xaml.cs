using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para GeneradorDeHojasPorVistaWindow.xaml
    /// </summary>
    public partial class GeneradorDeHojasPorVistaWindow : Window
    {
        public GeneradorDeHojasPorVistaWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando la generación automática de hojas por vista...",
                "Generador de Hojas por Vista",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí se implementará la lógica para:
            // 1. Obtener las vistas seleccionadas.
            // 2. Crear hojas nuevas por cada vista.
            // 3. Asignar nombre, escala y plantilla según parámetros definidos.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
