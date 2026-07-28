using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para SeccionesPorHabitacionWindow.xaml
    /// </summary>
    public partial class SeccionesPorHabitacionWindow : Window
    {
        public SeccionesPorHabitacionWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Generando secciones automáticas por habitación...",
                "Secciones por Habitación",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Lógica futura:
            // 1. Identificar habitaciones en el modelo.
            // 2. Crear secciones perpendiculares a los muros principales.
            // 3. Asignar nombre y escala personalizados a cada sección.
            // 4. Agrupar las vistas en hojas si es necesario.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
