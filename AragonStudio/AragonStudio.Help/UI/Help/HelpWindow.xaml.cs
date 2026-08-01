using System;
using System.Diagnostics;
using System.Windows;

namespace AragonStudio.UI
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 📧 Abrir Gmail directamente en el navegador
                string email = "aragonstudio31@gmail.com";
                string subject = "Soporte Aragón Studio V2026";
                string body = "Hola,%0D%0A%0D%0A" +
                              "Version Revit:%20" +
                              "%0D%0AVersion Plugin:%20V2026" +
                              "%0D%0ADescripcion del problema:%20";

                string url = $"https://mail.google.com/mail/?view=cm&fs=1&to={email}&su={subject}&body={body}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir Gmail. Error: " + ex.Message,
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}