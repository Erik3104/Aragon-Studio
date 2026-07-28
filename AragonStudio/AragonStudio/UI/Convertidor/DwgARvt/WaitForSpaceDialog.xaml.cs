using System.Windows;
using System.Windows.Input;

namespace AragonStudio.UI.Convertidor.DwgARvt
{
    public partial class WaitForSpaceDialog : Window
    {
        public WaitForSpaceDialog()
        {
            InitializeComponent();
            this.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}