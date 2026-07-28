using System.Collections.Generic;

namespace AragonStudio.UI.Gestión.ClasificacionPorTipo
{
    public class ConfigData
    {
        public string Categoria { get; set; }
        public string Parametro { get; set; }
        public string Prefijo { get; set; }
        public List<string> Niveles { get; set; }
    }
}