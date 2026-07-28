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
            double min = double.MaxValue;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 2; j < points.Count; j++)
                {
                    double d = points[i].DistanceTo(points[j]);
                    if (d > 0.001 && d < min) min = d;
                }
            return min == double.MaxValue ? 0.15 : min;
        }

        public static XYZ GetCenter(IList<XYZ> points)
        {
            double x = points.Average(p => p.X);
            double y = points.Average(p => p.Y);
            double z = points.Average(p => p.Z);
            return new XYZ(x, y, z);
        }

        public static CurveLoop PointsToCurveLoop(IList<XYZ> points)
        {
            CurveLoop loop = new CurveLoop();
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
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            return new BoundingBoxXYZ { Min = new XYZ(minX, minY, 0), Max = new XYZ(maxX, maxY, 0) };
        }
    }
}