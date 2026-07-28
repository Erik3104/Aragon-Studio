using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace AragonStudio.UI
{
    /// <summary>
    /// Lógica de interacción para CatalogoDeModelosWindow.xaml
    /// </summary>
    public partial class CatalogoDeModelosWindow : Window
    {
        public ObservableCollection<ModeloInfo> Modelos { get; set; }

        public CatalogoDeModelosWindow()
        {
            InitializeComponent();
            CargarModelos();
            ListaModelos.ItemsSource = Modelos;
        }

        private void CargarModelos()
        {
            // 🧠 Aquí se implementará la carga real de modelos desde el proyecto Revit.
            // Por ahora, mostramos ejemplos de prueba.
            Modelos = new ObservableCollection<ModeloInfo>
            {
                new ModeloInfo { Nombre = "Estructural_Base", Categoria = "Estructura", Fecha = DateTime.Now.AddDays(-5).ToShortDateString() },
                new ModeloInfo { Nombre = "Arquitectura_Principal", Categoria = "Arquitectura", Fecha = DateTime.Now.AddDays(-3).ToShortDateString() },
                new ModeloInfo { Nombre = "Instalaciones_MEP", Categoria = "MEP", Fecha = DateTime.Now.AddDays(-1).ToShortDateString() }
            };
        }

        private void Actualizar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Actualizando catálogo de modelos...", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarModelos();
            ListaModelos.ItemsSource = Modelos;
        }

        private void Abrir_Click(object sender, RoutedEventArgs e)
        {
            if (ListaModelos.SelectedItem is ModeloInfo modelo)
            {
                MessageBox.Show($"Abriendo el modelo: {modelo.Nombre}", "Abrir modelo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Por favor selecciona un modelo de la lista.", "Ningún modelo seleccionado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class ModeloInfo
    {
        public string Nombre { get; set; }
        public string Categoria { get; set; }
        public string Fecha { get; set; }
    }
}
