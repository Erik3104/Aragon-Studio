using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace AragonStudio.Resources.Icons
{
    public static class SvgIconLoader
    {
        /// <summary>
        /// Carga un archivo SVG desde la carpeta Resources/Icons/SvgIcons/
        /// </summary>
        public static ImageSource LoadSvg(string iconFile, int size = 32)
        {
            try
            {
                // Obtener la carpeta donde está el .dll
                string baseDir = Path.GetDirectoryName(typeof(SvgIconLoader).Assembly.Location) ?? "";

                // Buscar en Resources/Icons/SvgIcons/
                string fullPath = Path.Combine(baseDir, "Resources", "Icons", "SvgIcons", iconFile);

                if (!File.Exists(fullPath))
                {
                    // Fallback: buscar directamente en la carpeta del .dll
                    string altPath = Path.Combine(baseDir, iconFile);
                    if (File.Exists(altPath))
                        fullPath = altPath;
                    else
                        throw new FileNotFoundException($"No se encontró el archivo SVG: {iconFile}");
                }

                // Configuración de renderizado SVG
                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = true,
                    TextAsGeometry = true,
                    OptimizePath = true,
                    IgnoreRootViewbox = false
                };

                // Cargar y convertir el SVG a un dibujo WPF
                var reader = new FileSvgReader(settings);
                var drawing = reader.Read(fullPath);

                if (drawing == null)
                    throw new Exception($"No se pudo leer el SVG: {iconFile}");

                // Calcular el tamaño del dibujo original
                var bounds = drawing.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    throw new Exception("El SVG tiene dimensiones inválidas o vacías.");

                // Calcular el factor de escala
                double scaleX = size / bounds.Width;
                double scaleY = size / bounds.Height;

                // Crear un grupo escalado
                var group = new DrawingGroup();
                group.Children.Add(drawing);
                group.Transform = new ScaleTransform(scaleX, scaleY);

                // Convertir a ImageSource congelado
                var drawingImage = new DrawingImage(group);
                drawingImage.Freeze();

                return drawingImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SvgIconLoader] Error al cargar SVG '{iconFile}': {ex.Message}");

                // Fallback a Logo.ico
                try
                {
                    string baseDir = Path.GetDirectoryName(typeof(SvgIconLoader).Assembly.Location) ?? "";
                    string icoPath = Path.Combine(baseDir, "Resources", "Icons", "Logo.ico");
                    if (File.Exists(icoPath))
                    {
                        var bitmap = new BitmapImage(new Uri(icoPath, UriKind.Absolute));
                        bitmap.DecodePixelWidth = size;
                        return bitmap;
                    }
                }
                catch { }

                return null;
            }
        }
    }
}