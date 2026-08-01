using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AragonStudio.Services.dwgArevit
{
    public static class GeometryHelper
    {
        public static double GetThicknessFromPolyline(IList<XYZ> points)
        {
            if (points == null || points.Count < 3) return 0.15;

            double min = double.MaxValue;
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 2; j < count; j++)
                {
                    double d = points[i].DistanceTo(points[j]);
                    if (d > 0.001 && d < min) min = d;
                }
            }

            return min == double.MaxValue ? 0.15 : min;
        }

        public static XYZ GetCenter(IList<XYZ> points)
        {
            if (points == null || points.Count == 0) return XYZ.Zero;

            double x = points.Average(p => p.X);
            double y = points.Average(p => p.Y);
            double z = points.Average(p => p.Z);
            return new XYZ(x, y, z);
        }

        public static CurveLoop PointsToCurveLoop(IList<XYZ> points)
        {
            CurveLoop loop = new CurveLoop();
            if (points == null || points.Count < 2) return loop;

            for (int i = 0; i < points.Count - 1; i++)
            {
                if (points[i].DistanceTo(points[i + 1]) > 0.001)
                    loop.Append(Line.CreateBound(points[i], points[i + 1]));
            }

            if (points.Count >= 3 && points[0].DistanceTo(points[points.Count - 1]) < 0.001)
            {
                if (points[points.Count - 1].DistanceTo(points[0]) > 0.001)
                    loop.Append(Line.CreateBound(points[points.Count - 1], points[0]));
            }

            return loop;
        }

        public static BoundingBoxXYZ GetBoundingBox(IList<XYZ> points)
        {
            if (points == null || points.Count == 0)
                return new BoundingBoxXYZ { Min = XYZ.Zero, Max = XYZ.Zero };

            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            double minZ = points.Min(p => p.Z);
            double maxZ = points.Max(p => p.Z);

            return new BoundingBoxXYZ
            {
                Min = new XYZ(minX, minY, minZ),
                Max = new XYZ(maxX, maxY, maxZ)
            };
        }

        public static (double Width, double Height, double Depth) GetDimensions(IList<XYZ> points)
        {
            if (points == null || points.Count == 0)
                return (0, 0, 0);

            var bbox = GetBoundingBox(points);
            return (
                bbox.Max.X - bbox.Min.X,
                bbox.Max.Y - bbox.Min.Y,
                bbox.Max.Z - bbox.Min.Z
            );
        }

        public static Line GetCenterLine(IList<XYZ> points)
        {
            if (points == null || points.Count < 2) return null;

            var (width, height, _) = GetDimensions(points);
            var center = GetCenter(points);

            if (width >= height)
            {
                double minX = points.Min(p => p.X);
                double maxX = points.Max(p => p.X);
                return Line.CreateBound(
                    new XYZ(minX, center.Y, 0),
                    new XYZ(maxX, center.Y, 0));
            }
            else
            {
                double minY = points.Min(p => p.Y);
                double maxY = points.Max(p => p.Y);
                return Line.CreateBound(
                    new XYZ(center.X, minY, 0),
                    new XYZ(center.X, maxY, 0));
            }
        }

        public static double FeetToMeters(double feet) => feet * 0.3048;
        public static double MetersToFeet(double meters) => meters / 0.3048;
        public static double RoundToCentimeter(double meters) => Math.Round(meters / 0.01) * 0.01;
    }
}