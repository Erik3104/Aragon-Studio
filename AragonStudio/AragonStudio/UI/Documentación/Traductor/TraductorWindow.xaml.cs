using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para TraductorWindow.xaml
    /// </summary>
    public partial class TraductorWindow : Window
    {
        public TraductorWindow()
        {
            InitializeComponent();
        }

        private void Traducir_Click(object sender, RoutedEventArgs e)
        {
            string textoOriginal = InputText.Text.Trim();

            if (string.IsNullOrEmpty(textoOriginal))
            {
                MessageBox.Show("Por favor ingrese un texto para traducir.",
                    "Campo vacío", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🧠 Aquí iría la lógica de traducción real
            // (por ejemplo, conexión a una API de traducción)
            OutputText.Text = $"[Traducción simulada de]: {textoOriginal}";
        }

        private void Limpiar_Click(object sender, RoutedEventArgs e)
        {
            InputText.Clear();
            OutputText.Clear();
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
