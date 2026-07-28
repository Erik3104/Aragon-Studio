using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para ModeladoEstructuralWindow.xaml
    /// </summary>
    public partial class ModeladoEstructuralWindow : Window
    {
        public ModeladoEstructuralWindow()
        {
            InitializeComponent();
        }

        private void Iniciar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando modelado estructural...",
                "Modelado Estructural",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí se implementará la lógica para automatizar el modelado estructural:
            // creación de vigas, columnas, zapatas, muros estructurales, etc.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
