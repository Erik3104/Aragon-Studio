using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para ModeladoArquitectonicoWindow.xaml
    /// </summary>
    public partial class ModeladoArquitectonicoWindow : Window
    {
        public ModeladoArquitectonicoWindow()
        {
            InitializeComponent();
        }

        private void Iniciar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando modelado arquitectónico...",
                "Modelado Arquitectónico",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí se implementará la lógica de automatización
            // para generar muros, pisos, cubiertas y componentes arquitectónicos.
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
