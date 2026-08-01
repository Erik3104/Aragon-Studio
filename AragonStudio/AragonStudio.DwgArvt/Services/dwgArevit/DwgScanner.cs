using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using MediaColor = System.Windows.Media.Color;

namespace AragonStudio.Services.dwgArevit
{
    public class DwgScanner
    {
        private readonly Element _dwgElement;
        private readonly Document _doc;
        private readonly Options _options;

        public DwgScanner(Element dwgElement, Document doc)
        {
            _dwgElement = dwgElement ?? throw new ArgumentNullException(nameof(dwgElement));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            _options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };
        }

        public ScanResult Scan()
        {
            var result = new ScanResult();

            try
            {
                GeometryElement geomElem = _dwgElement.get_Geometry(_options);
                if (geomElem == null) return result;

                TraverseGeometry(geomElem, Transform.Identity, result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en DwgScanner.Scan: {ex.Message}");
            }

            return result;
        }

        private void TraverseGeometry(GeometryElement geomElem, Transform transform, ScanResult result)
        {
            if (geomElem == null) return;

            foreach (GeometryObject geomObj in geomElem)
            {
                try
                {
                    string layer = GetLayerName(geomObj);
                    if (string.IsNullOrEmpty(layer)) layer = "Desconocido";
                    MediaColor color = GetLayerColor(geomObj);

                    if (geomObj is PolyLine poly)
                    {
                        IList<XYZ> points = poly.GetCoordinates();
                        if (points != null && points.Count >= 2)
                        {
                            if (!transform.IsIdentity)
                            {
                                points = points.Select(p => transform.OfPoint(p)).ToList();
                            }

                            bool isClosed = points.Count >= 3 && points[0].DistanceTo(points[points.Count - 1]) < 0.001;
                            result.AddPolyline(layer, points, isClosed, color);
                        }
                    }
                    else if (geomObj is Curve curve)
                    {
                        IList<XYZ> pts = curve.Tessellate();
                        if (pts != null && pts.Count >= 2)
                        {
                            if (!transform.IsIdentity)
                            {
                                pts = pts.Select(p => transform.OfPoint(p)).ToList();
                            }

                            bool isClosed = pts.Count >= 3 && pts[0].DistanceTo(pts[pts.Count - 1]) < 0.001;

                            if (curve is Arc arc && Math.Abs(arc.Length - 2 * Math.PI * arc.Radius) < 0.001)
                            {
                                result.AddCircle(layer, arc, color);
                            }
                            else
                            {
                                result.AddPolyline(layer, pts, isClosed, color);
                            }
                        }
                    }
                    else if (geomObj is GeometryInstance inst)
                    {
                        Transform newTransform = transform.Multiply(inst.Transform);
                        GeometryElement instGeom = inst.GetInstanceGeometry();

                        TraverseGeometry(instGeom, newTransform, result);
                        ProcessBlock(instGeom, newTransform, layer, color, result);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error procesando GeometryObject: {ex.Message}");
                }
            }
        }

        private void ProcessBlock(GeometryElement instGeom, Transform transform, string layer, MediaColor color, ScanResult result)
        {
            try
            {
                BoundingBoxXYZ bbox = GetBoundingBoxFromGeometry(instGeom, transform);

                double widthFeet = 0.90 / 0.3048;
                double heightFeet = 0.90 / 0.3048;
                XYZ center = transform.Origin;
                XYZ direction = transform.BasisX.Normalize();

                if (bbox != null)
                {
                    double width = bbox.Max.X - bbox.Min.X;
                    double height = bbox.Max.Y - bbox.Min.Y;

                    if (width > 0.001 && height > 0.001)
                    {
                        widthFeet = Math.Min(width, height);
                        heightFeet = Math.Max(width, height);
                        center = (bbox.Min + bbox.Max) / 2;
                    }
                }

                result.AddBlock(layer, "CAD_BLOCK", widthFeet, heightFeet, center, direction, color);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error procesando bloque: {ex.Message}");
            }
        }

        private MediaColor GetLayerColor(GeometryObject geomObj)
        {
            try
            {
                if (geomObj.GraphicsStyleId != ElementId.InvalidElementId)
                {
                    GraphicsStyle gs = _doc.GetElement(geomObj.GraphicsStyleId) as GraphicsStyle;
                    if (gs?.GraphicsStyleCategory?.LineColor != null)
                    {
                        var revitColor = gs.GraphicsStyleCategory.LineColor;
                        return MediaColor.FromArgb(255, revitColor.Red, revitColor.Green, revitColor.Blue);
                    }
                }

                if (geomObj is Curve curve)
                {
                    Element elem = _doc.GetElement(curve.GraphicsStyleId);
                    if (elem is GraphicsStyle gs2 && gs2.GraphicsStyleCategory != null)
                    {
                        var color = gs2.GraphicsStyleCategory.LineColor;
                        if (color != null)
                        {
                            return MediaColor.FromArgb(255, color.Red, color.Green, color.Blue);
                        }
                    }
                }
            }
            catch { }

            return MediaColor.FromArgb(255, 128, 128, 128);
        }

        private string GetLayerName(GeometryObject geomObj)
        {
            try
            {
                if (geomObj.GraphicsStyleId != ElementId.InvalidElementId)
                {
                    GraphicsStyle gs = _doc.GetElement(geomObj.GraphicsStyleId) as GraphicsStyle;
                    if (gs?.GraphicsStyleCategory != null)
                    {
                        string name = gs.GraphicsStyleCategory.Name;
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }

                if (geomObj is Curve curve)
                {
                    Element elem = _doc.GetElement(curve.GraphicsStyleId);
                    if (elem is GraphicsStyle gs2 && gs2.GraphicsStyleCategory != null)
                    {
                        string name = gs2.GraphicsStyleCategory.Name;
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            }
            catch { }

            return null;
        }

        private BoundingBoxXYZ GetBoundingBoxFromGeometry(GeometryElement geomElem, Transform transform)
        {
            if (geomElem == null) return null;

            try
            {
                BoundingBoxXYZ bbox = new BoundingBoxXYZ
                {
                    Min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue),
                    Max = new XYZ(double.MinValue, double.MinValue, double.MinValue)
                };

                bool hasGeometry = false;

                foreach (GeometryObject obj in geomElem)
                {
                    if (obj is Curve curve)
                    {
                        Curve transformed = curve.CreateTransformed(transform);
                        IList<XYZ> pts = transformed.Tessellate();

                        foreach (XYZ p in pts)
                        {
                            bbox.Min = new XYZ(
                                Math.Min(bbox.Min.X, p.X),
                                Math.Min(bbox.Min.Y, p.Y),
                                Math.Min(bbox.Min.Z, p.Z)
                            );
                            bbox.Max = new XYZ(
                                Math.Max(bbox.Max.X, p.X),
                                Math.Max(bbox.Max.Y, p.Y),
                                Math.Max(bbox.Max.Z, p.Z)
                            );
                            hasGeometry = true;
                        }
                    }
                    else if (obj is GeometryInstance inst)
                    {
                        BoundingBoxXYZ subBbox = GetBoundingBoxFromGeometry(
                            inst.GetInstanceGeometry(),
                            transform.Multiply(inst.Transform)
                        );

                        if (subBbox != null)
                        {
                            bbox.Min = new XYZ(
                                Math.Min(bbox.Min.X, subBbox.Min.X),
                                Math.Min(bbox.Min.Y, subBbox.Min.Y),
                                Math.Min(bbox.Min.Z, subBbox.Min.Z)
                            );
                            bbox.Max = new XYZ(
                                Math.Max(bbox.Max.X, subBbox.Max.X),
                                Math.Max(bbox.Max.Y, subBbox.Max.Y),
                                Math.Max(bbox.Max.Z, subBbox.Max.Z)
                            );
                            hasGeometry = true;
                        }
                    }
                }

                return hasGeometry ? bbox : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ScanResult
    {
        public HashSet<string> Layers { get; } = new HashSet<string>();
        public Dictionary<string, List<CadPolyline>> PolylinesByLayer { get; } = new Dictionary<string, List<CadPolyline>>();
        public Dictionary<string, List<CadCircle>> CirclesByLayer { get; } = new Dictionary<string, List<CadCircle>>();
        public Dictionary<string, List<CadBlock>> BlocksByLayer { get; } = new Dictionary<string, List<CadBlock>>();

        public void AddPolyline(string layer, IList<XYZ> points, bool isClosed, MediaColor color)
        {
            if (string.IsNullOrEmpty(layer)) return;
            if (points == null || points.Count < 2) return;

            Layers.Add(layer);

            if (!PolylinesByLayer.ContainsKey(layer))
                PolylinesByLayer[layer] = new List<CadPolyline>();

            PolylinesByLayer[layer].Add(new CadPolyline
            {
                Points = points.ToList(),
                IsClosed = isClosed,
                Color = color
            });
        }

        public void AddCircle(string layer, Arc arc, MediaColor color)
        {
            if (string.IsNullOrEmpty(layer)) return;
            if (arc == null) return;

            Layers.Add(layer);

            if (!CirclesByLayer.ContainsKey(layer))
                CirclesByLayer[layer] = new List<CadCircle>();

            CirclesByLayer[layer].Add(new CadCircle
            {
                Arc = arc,
                Center = arc.Center,
                Radius = arc.Radius,
                Color = color
            });
        }

        public void AddBlock(string layer, string name, double widthFeet, double heightFeet, XYZ center, XYZ direction, MediaColor color)
        {
            if (string.IsNullOrEmpty(layer)) return;
            if (widthFeet < 0.001 || heightFeet < 0.001) return;

            Layers.Add(layer);

            if (!BlocksByLayer.ContainsKey(layer))
                BlocksByLayer[layer] = new List<CadBlock>();

            BlocksByLayer[layer].Add(new CadBlock
            {
                Name = name ?? "CAD_BLOCK",
                WidthFeet = widthFeet,
                HeightFeet = heightFeet,
                Center = center,
                Direction = direction,
                Color = color
            });
        }
    }

    public class CadPolyline
    {
        public List<XYZ> Points { get; set; } = new List<XYZ>();
        public bool IsClosed { get; set; }
        public MediaColor Color { get; set; }
    }

    public class CadCircle
    {
        public Arc Arc { get; set; }
        public XYZ Center { get; set; }
        public double Radius { get; set; }
        public MediaColor Color { get; set; }
    }

    public class CadBlock
    {
        public string Name { get; set; } = "CAD_BLOCK";
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }
        public XYZ Center { get; set; }
        public XYZ Direction { get; set; }
        public MediaColor Color { get; set; }
    }
}