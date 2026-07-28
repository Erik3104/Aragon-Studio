using System.Windows.Controls;
using AragonStudio.Resources.Icons;

namespace AragonStudio.UI.Controls
{
    public partial class LogoControl : UserControl
    {
        public LogoControl()
        {
            InitializeComponent();
            LogoImage.Source = SvgIconLoader.LoadSvg("Resources/Icons/SvgIcons/Logo.svg", 32);
        }
    }
}