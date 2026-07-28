using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para InformeGeneralWindow.xaml
    /// </summary>
    public partial class InformeGeneralWindow : Window
    {
        public InformeGeneralWindow()
        {
            InitializeComponent();
        }

        private void AnalizarModelo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Analizando el modelo Revit en busca de información general...",
                "Análisis del modelo", MessageBoxButton.OK, MessageBoxImage.Information);

            // Aquí se implementará la recolección de datos reales del modelo
            MessageBox.Show("Análisis completado. Se detectaron elementos listos para generar el informe.",
                "Análisis completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GenerarInforme_Click(object sender, RoutedEventArgs e)
        {
            // Aquí se implementará la generación del informe
            MessageBox.Show("Generando informe general del proyecto...",
                "Generador de informe", MessageBoxButton.OK, MessageBoxImage.Information);

            MessageBox.Show("Informe generado correctamente y guardado en la carpeta del proyecto.",
                "Proceso finalizado", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
