using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AragonStudio.Resources.Icons
{
    public static class SvgIconLoader
    {
        /// <summary>
        /// Carga un archivo SVG desde una ruta relativa al ensamblado
        /// y lo convierte en ImageSource escalado al tamaño deseado.
        /// </summary>
        public static ImageSource LoadSvg(string relativePath, int size = 32)
        {
            try
            {
                // 📂 Obtiene la ruta base del ensamblado (donde está el .dll)
                string baseDir = Path.GetDirectoryName(typeof(SvgIconLoader).Assembly.Location) ?? "";
                string fullPath = Path.Combine(baseDir, relativePath);

                if (!File.Exists(fullPath))
                {
                    // Si no se encuentra, intenta buscar en la carpeta Resources
                    string altPath = Path.Combine(baseDir, "Resources", Path.GetFileName(relativePath));
                    if (File.Exists(altPath))
                        fullPath = altPath;
                    else
                        throw new FileNotFoundException($"No se encontró el archivo SVG: {relativePath}");
                }

                // ⚙️ Configuración de renderizado SVG
                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = true,
                    TextAsGeometry = true,
                    OptimizePath = true,
                    IgnoreRootViewbox = false
                };

                // 🖼️ Carga y convierte el SVG a un dibujo WPF
                var reader = new FileSvgReader(settings);
                var drawing = reader.Read(fullPath);

                if (drawing == null)
                    throw new Exception($"No se pudo leer el SVG: {relativePath}");

                // 📏 Calcula el tamaño del dibujo original
                var bounds = drawing.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    throw new Exception("El SVG tiene dimensiones inválidas o vacías.");

                // 🔍 Calcula el factor de escala para que ocupe el tamaño deseado
                double scaleX = size / bounds.Width;
                double scaleY = size / bounds.Height;

                // 🎨 Crea un grupo escalado
                var group = new DrawingGroup();
                group.Children.Add(drawing);
                group.Transform = new ScaleTransform(scaleX, scaleY);

                // 🧊 Convierte a ImageSource congelado (rendimiento)
                var drawingImage = new DrawingImage(group);
                drawingImage.Freeze();

                return drawingImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SvgIconLoader] Error al cargar SVG '{relativePath}': {ex.Message}");

                // 🧩 Fallback a ícono predeterminado si falla
                return new BitmapImage(new Uri(
                    "pack://application:,,,/AragonStudio;component/Resources/Icons/Logo.ico",
                    UriKind.Absolute));
            }
        }
    }
}
