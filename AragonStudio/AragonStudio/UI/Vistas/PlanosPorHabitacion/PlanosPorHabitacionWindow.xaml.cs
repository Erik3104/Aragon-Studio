using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para PlanosPorHabitacionWindow.xaml
    /// </summary>
    public partial class PlanosPorHabitacionWindow : Window
    {
        public PlanosPorHabitacionWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Generando planos por habitación...",
                "Planos por Habitación",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Lógica futura:
            // 1. Recorre todas las habitaciones del modelo.
            // 2. Crea vistas de planta, techo y elevaciones.
            // 3. Genera una hoja para cada habitación con su respectiva escala y nombre.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
