using System.Windows;
using Autodesk.Revit.UI;

namespace AragonStudio.UI.Gestión.ClasificacionPorTipo
{
    public partial class SeleccionBaseWindow : Window
    {
        public UIApplication UiApp { get; private set; }

        public SeleccionBaseWindow(UIApplication uiApp)
        {
            InitializeComponent();
            UiApp = uiApp;
        }

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}