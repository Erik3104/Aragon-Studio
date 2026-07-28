using System;
using System.Windows;
using Autodesk.Revit.DB;

namespace AragonStudio.UI.Gestión.Guardado
{
    public partial class GuardarAhoraWindow : Window
    {
        private readonly Document _doc;

        public GuardarAhoraWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _doc.Save();
                this.DialogResult = true;
                this.Close();
            }
            catch
            {
                this.DialogResult = false;
                this.Close();
            }
        }
    }
}