using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para GeneradorDeVegetacionWindow.xaml
    /// </summary>
    public partial class GeneradorDeVegetacionWindow : Window
    {
        public GeneradorDeVegetacionWindow()
        {
            InitializeComponent();
        }

        private void Generar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Iniciando la generación de vegetación en el modelo...",
                "Generador de Vegetación",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // 🧠 Aquí se implementará la lógica para colocar familias de vegetación (árboles, arbustos, etc.)
            // usando la API de Revit, por ejemplo FamilySymbol.Activate() y doc.Create.NewFamilyInstance().
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
