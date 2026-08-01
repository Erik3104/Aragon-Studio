using AragonStudio.Services.dwgArevit;
using AragonStudio.UI.Convertidor.DwgARvt;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Transactions;

namespace AragonStudio.Commands.Convertidor.DwgARvt
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class DwgARvt : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Aragón Studio", "No hay un documento activo.");
                    return Result.Failed;
                }

                Reference pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new DwgSelectionFilter(),
                    "Selecciona el DWG importado o vinculado");
                Element dwgElement = doc.GetElement(pickedRef);
                if (dwgElement == null) return Result.Failed;

                DwgScanner scanner = new DwgScanner(dwgElement, doc);
                ScanResult scan = scanner.Scan();
                if (scan.Layers.Count == 0)
                {
                    TaskDialog.Show("Aragón Studio", "No se detectaron polilíneas ni bloques en el DWG.");
                    return Result.Failed;
                }

                DwgARvtWindow window = new DwgARvtWindow(scan, doc);
                if (window.ShowDialog() != true) return Result.Cancelled;

                Level levelBase = window.LevelBase;
                Level levelTop = window.LevelTop;
                double doorHeight = window.DoorHeight;
                double windowHeight = window.WindowHeight;
                double sillHeight = window.SillHeight;
                double beamHeight = window.BeamHeight;
                double beamOffset = window.BeamOffset;
                double footingThick = window.FootingThickness;
                double floorThick = window.FloorThickness;

                if (levelBase == null)
                {
                    TaskDialog.Show("Aragón Studio", "Debe seleccionar un nivel base válido.");
                    return Result.Failed;
                }

                using (Transaction trans = new Transaction(doc, "Generar modelo BIM desde DWG"))
                {
                    trans.Start();

                    foreach (var mapping in window.GetLayerMappings())
                    {
                        if (mapping.SelectedCategory?.DisplayName == "Ninguno") continue;
                        string category = mapping.SelectedCategory.DisplayName;
                        string familyName = mapping.SelectedFamily?.Name;

                        try
                        {
                            switch (category)
                            {
                                case "Muro":
                                    CreateWalls(doc, scan, mapping.LayerName, levelBase, levelTop, familyName);
                                    break;
                                case "Columna":
                                    CreateColumns(doc, scan, mapping.LayerName, levelBase, levelTop, familyName);
                                    break;
                                case "VigaCimentacion":
                                    CreateFoundationBeams(doc, scan, mapping.LayerName, levelBase, beamHeight, beamOffset, familyName);
                                    break;
                                case "VigaEstructural":
                                    CreateStructuralBeams(doc, scan, mapping.LayerName, levelBase, beamHeight, beamOffset, familyName);
                                    break;
                                case "Zapata":
                                    CreateFootingsFromContour(doc, scan, mapping.LayerName, levelBase, footingThick, familyName);
                                    break;
                                case "LosaCimentacion":
                                    CreateFoundationSlabs(doc, scan, mapping.LayerName, levelBase, familyName);
                                    break;
                                case "Suelo":
                                    CreateFloors(doc, scan, mapping.LayerName, levelBase, familyName);
                                    break;
                                case "Eje":
                                    CreateGrids(doc, scan, mapping.LayerName);
                                    break;
                                case "Corte":
                                    CreateSectionCuts(doc, scan, mapping.LayerName);
                                    break;
                                case "Puerta":
                                    CreateDoors(doc, scan, mapping.LayerName, levelBase, doorHeight, familyName);
                                    break;
                                case "Ventana":
                                    CreateWindows(doc, scan, mapping.LayerName, levelBase, windowHeight, sillHeight, familyName);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskDialog.Show("Advertencia", $"Error al procesar la capa '{mapping.LayerName}': {ex.Message}");
                        }
                    }

                    FlipAllWindows(doc);
                    trans.Commit();
                }

                TaskDialog.Show("Aragón Studio", "✅ Modelo BIM generado exitosamente.");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"❌ Error al ejecutar el comando:\n{ex.Message}");
                return Result.Failed;
            }
        }

        // =========================================================================
        // CREACIÓN DE MUROS
        // =========================================================================
        private void CreateWalls(Document doc, ScanResult scan, string layerName, Level levelBase, Level levelTop, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            Dictionary<string, WallType> wallTypeCache = new Dictionary<string, WallType>();

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;

                double minX = poly.Points.Min(p => p.X);
                double maxX = poly.Points.Max(p => p.X);
                double minY = poly.Points.Min(p => p.Y);
                double maxY = poly.Points.Max(p => p.Y);

                double anchoFeet = maxX - minX;
                double altoFeet = maxY - minY;

                double anchoM = anchoFeet * 0.3048;
                double altoM = altoFeet * 0.3048;

                double grosorM = Math.Min(anchoM, altoM);
                grosorM = Math.Round(grosorM / 0.01) * 0.01;
                if (grosorM < 0.01) grosorM = 0.01;

                int grosorCm = (int)Math.Round(grosorM * 100);
                double grosorFeet = grosorM / 0.3048;

                string cacheKey = $"{grosorFeet:F4}_{familyName ?? "default"}";
                if (!wallTypeCache.ContainsKey(cacheKey))
                {
                    WallType wt = GetOrCreateWallType(doc, grosorFeet, familyName, grosorCm);
                    if (wt != null)
                        wallTypeCache[cacheKey] = wt;
                    else
                        continue;
                }
                WallType wallType = wallTypeCache[cacheKey];

                XYZ p1, p2;
                if (anchoM >= altoM)
                {
                    double centroY = (minY + maxY) / 2;
                    p1 = new XYZ(minX, centroY, 0);
                    p2 = new XYZ(maxX, centroY, 0);
                }
                else
                {
                    double centroX = (minX + maxX) / 2;
                    p1 = new XYZ(centroX, minY, 0);
                    p2 = new XYZ(centroX, maxY, 0);
                }

                Line centerLine = Line.CreateBound(p1, p2);
                if (centerLine.ApproximateLength < 0.001) continue;

                try
                {
                    Wall wall = Wall.Create(doc, centerLine, wallType.Id, levelBase.Id, grosorFeet, 0, true, false);
                    if (wall != null && levelTop != null && levelTop.Id != levelBase.Id)
                        wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(levelTop.Id);
                }
                catch { }
            }
        }

        private WallType GetOrCreateWallType(Document doc, double thicknessFeet, string familyName, int thicknessCm)
        {
            var allWallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().ToList();

            foreach (WallType wt in allWallTypes)
            {
                Parameter p = wt.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                if (p != null && Math.Abs(p.AsDouble() - thicknessFeet) < 0.001)
                {
                    if (wt.Name.Contains($"{thicknessCm}cm")) return wt;
                }
            }

            if (!string.IsNullOrEmpty(familyName))
            {
                foreach (WallType wt in allWallTypes)
                {
                    if (wt.Name.Contains(familyName) && wt.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM) != null)
                    {
                        Parameter p = wt.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                        if (p != null && Math.Abs(p.AsDouble() - thicknessFeet) < 0.001)
                            return wt;
                    }
                }
            }

            WallType baseType = allWallTypes.FirstOrDefault(w =>
                w.Name.Contains("Generic") || w.Name.Contains("Genérico") ||
                w.Name.Contains("Basic") || w.Name.Contains("Por defecto"));
            if (baseType == null)
                baseType = allWallTypes.FirstOrDefault();
            if (baseType == null) return null;

            string baseName = $"Muro_{thicknessCm}cm";
            string uniqueName = GetUniqueTypeName(doc, baseName, typeof(WallType));

            WallType newType = baseType.Duplicate(uniqueName) as WallType;
            if (newType == null) return null;

            Parameter pWidth = newType.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
            if (pWidth != null && !pWidth.IsReadOnly)
                pWidth.Set(thicknessFeet);

            return newType;
        }

        // =========================================================================
        // CREACIÓN DE COLUMNAS
        // =========================================================================
        private void CreateColumns(Document doc, ScanResult scan, string layerName, Level levelBase, Level levelTop, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            Dictionary<string, FamilySymbol> columnTypeCache = new Dictionary<string, FamilySymbol>();

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;

                double minX = poly.Points.Min(p => p.X);
                double maxX = poly.Points.Max(p => p.X);
                double minY = poly.Points.Min(p => p.Y);
                double maxY = poly.Points.Max(p => p.Y);

                double widthFeet = maxX - minX;
                double depthFeet = maxY - minY;

                double widthMeters = widthFeet * 0.3048;
                double depthMeters = depthFeet * 0.3048;

                widthMeters = Math.Round(widthMeters / 0.01) * 0.01;
                depthMeters = Math.Round(depthMeters / 0.01) * 0.01;

                double widthFeetRounded = widthMeters / 0.3048;
                double depthFeetRounded = depthMeters / 0.3048;

                if (widthFeetRounded < 0.001 || depthFeetRounded < 0.001) continue;

                string cacheKey = $"{widthFeetRounded:F4}_{depthFeetRounded:F4}_{familyName ?? "default"}";
                if (!columnTypeCache.ContainsKey(cacheKey))
                {
                    FamilySymbol columnType = GetOrCreateColumnType(doc, widthFeetRounded, depthFeetRounded, familyName, widthMeters, depthMeters);
                    if (columnType != null && !columnType.IsActive) columnType.Activate();
                    columnTypeCache[cacheKey] = columnType;
                }

                FamilySymbol finalType = columnTypeCache[cacheKey];
                if (finalType == null) continue;

                XYZ center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, levelBase.Elevation);
                try
                {
                    FamilyInstance column = doc.Create.NewFamilyInstance(center, finalType, levelBase, StructuralType.Column);
                    if (column != null && levelTop != null && levelTop.Id != levelBase.Id)
                    {
                        Parameter topParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topParam != null && topParam.HasValue)
                            topParam.Set(levelTop.Id);
                    }
                }
                catch { }
            }
        }

        private FamilySymbol GetOrCreateColumnType(Document doc, double widthFeet, double depthFeet, string familyName, double widthMeters, double depthMeters)
        {
            var cols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>()
                .ToList();

            foreach (FamilySymbol fs in cols)
            {
                Parameter w = fs.LookupParameter("b");
                Parameter d = fs.LookupParameter("h");
                if (w != null && d != null &&
                    Math.Abs(w.AsDouble() - widthFeet) < 0.001 &&
                    Math.Abs(d.AsDouble() - depthFeet) < 0.001)
                    return fs;
            }

            FamilySymbol baseType = cols.FirstOrDefault(f => !string.IsNullOrEmpty(familyName) && f.Name.Contains(familyName));
            if (baseType == null) baseType = cols.FirstOrDefault(f => f.Name.Contains("Concrete") || f.Name.Contains("Hormigón"));
            if (baseType == null) baseType = cols.FirstOrDefault();
            if (baseType == null) return null;

            int widthCm = (int)Math.Round(widthMeters * 100);
            int depthCm = (int)Math.Round(depthMeters * 100);
            string baseName = $"Columna_{widthCm}x{depthCm}cm";
            string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_StructuralColumns);

            FamilySymbol newType = baseType.Duplicate(uniqueName) as FamilySymbol;
            if (newType == null) return null;

            newType.LookupParameter("b")?.Set(widthFeet);
            newType.LookupParameter("h")?.Set(depthFeet);
            newType.LookupParameter("Width")?.Set(widthFeet);
            newType.LookupParameter("Depth")?.Set(depthFeet);

            return newType;
        }

        // =========================================================================
        // CREACIÓN DE VIGAS DE CIMENTACIÓN
        // =========================================================================
        private void CreateFoundationBeams(Document doc, ScanResult scan, string layerName, Level levelBase,
                                           double heightMeters, double offsetMeters, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            double heightFeet = heightMeters / 0.3048;
            double offsetFeet = offsetMeters / 0.3048;

            var beamTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .ToList();

            if (beamTypes.Count == 0) return;

            FamilySymbol baseType = beamTypes.FirstOrDefault(b => !string.IsNullOrEmpty(familyName) && b.Name.Contains(familyName));
            if (baseType == null) baseType = beamTypes.FirstOrDefault(b => b.Name.Contains("Concrete") || b.Name.Contains("Hormigón"));
            if (baseType == null) baseType = beamTypes.FirstOrDefault();
            if (baseType == null) return;

            Dictionary<string, FamilySymbol> beamTypeCache = new Dictionary<string, FamilySymbol>();

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;

                double minX = poly.Points.Min(p => p.X);
                double maxX = poly.Points.Max(p => p.X);
                double minY = poly.Points.Min(p => p.Y);
                double maxY = poly.Points.Max(p => p.Y);

                double widthFeet = Math.Min(maxX - minX, maxY - minY);
                if (widthFeet < 0.001) continue;

                double widthMeters = widthFeet * 0.3048;
                widthMeters = Math.Round(widthMeters / 0.01) * 0.01;
                widthFeet = widthMeters / 0.3048;

                string key = $"{widthFeet:F4}_{heightFeet:F4}";

                if (!beamTypeCache.ContainsKey(key))
                {
                    FamilySymbol beamType = beamTypes.FirstOrDefault(b =>
                    {
                        double w = b.LookupParameter("Width")?.AsDouble() ?? 0;
                        double h = b.LookupParameter("Height")?.AsDouble() ?? 0;
                        return Math.Abs(w - widthFeet) < 0.001 && Math.Abs(h - heightFeet) < 0.001;
                    });

                    if (beamType == null)
                    {
                        int widthCm = (int)Math.Round(widthMeters * 100);
                        int heightCm = (int)Math.Round(heightMeters * 100);
                        string baseName = $"Viga_{widthCm}x{heightCm}cm";
                        string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_StructuralFraming);

                        beamType = baseType.Duplicate(uniqueName) as FamilySymbol;
                        if (beamType == null) continue;

                        beamType.LookupParameter("Width")?.Set(widthFeet);
                        beamType.LookupParameter("Height")?.Set(heightFeet);
                        beamType.LookupParameter("b")?.Set(widthFeet);
                        beamType.LookupParameter("h")?.Set(heightFeet);
                    }

                    if (!beamType.IsActive) beamType.Activate();
                    beamTypeCache[key] = beamType;
                }

                FamilySymbol finalType = beamTypeCache[key];
                if (finalType == null) continue;

                Line axis = GeometryHelper.GetCenterLine(poly.Points);
                if (axis == null || axis.ApproximateLength < 0.001) continue;

                try
                {
                    FamilyInstance beam = doc.Create.NewFamilyInstance(axis, finalType, levelBase, StructuralType.Beam);

                    if (Math.Abs(offsetFeet) > 0.001)
                    {
                        Parameter startOffset = beam.LookupParameter("Start Level Offset");
                        if (startOffset != null && !startOffset.IsReadOnly)
                            startOffset.Set(offsetFeet);

                        Parameter endOffset = beam.LookupParameter("End Level Offset");
                        if (endOffset != null && !endOffset.IsReadOnly)
                            endOffset.Set(offsetFeet);
                    }
                }
                catch { }
            }
        }

        // =========================================================================
        // CREACIÓN DE VIGAS ESTRUCTURALES
        // =========================================================================
        private void CreateStructuralBeams(Document doc, ScanResult scan, string layerName, Level levelBase,
                                           double heightMeters, double offsetMeters, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;

            var openPolylines = polylines.Where(p => !p.IsClosed).ToList();
            var closedPolylines = polylines.Where(p => p.IsClosed).ToList();

            List<CadPolyline> allBeams = new List<CadPolyline>();
            allBeams.AddRange(openPolylines);

            foreach (var poly in closedPolylines)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;

                var (width, height, _) = GeometryHelper.GetDimensions(poly.Points);
                double ratio = Math.Max(width, height) / Math.Min(width, height);

                if (ratio > 2.0 && Math.Min(width, height) < 1.0)
                {
                    allBeams.Add(poly);
                }
            }

            if (allBeams.Count == 0) return;

            double heightFeet = heightMeters / 0.3048;
            double offsetFeet = offsetMeters / 0.3048;

            var beamTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .ToList();

            if (beamTypes.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", "⚠️ No se encontraron familias de vigas estructurales en el proyecto.");
                return;
            }

            FamilySymbol baseType = beamTypes.FirstOrDefault(b => !string.IsNullOrEmpty(familyName) && b.Name.Contains(familyName));
            if (baseType == null) baseType = beamTypes.FirstOrDefault(b => b.Name.Contains("Concrete") || b.Name.Contains("Hormigón"));
            if (baseType == null) baseType = beamTypes.FirstOrDefault();
            if (baseType == null) return;

            if (!baseType.IsActive) baseType.Activate();

            Dictionary<string, FamilySymbol> beamTypeCache = new Dictionary<string, FamilySymbol>();
            int createdCount = 0;

            foreach (var poly in allBeams)
            {
                if (poly.Points == null || poly.Points.Count < 2) continue;

                double widthFeet = 0.30 / 0.3048;
                Line centerLine;

                if (poly.IsClosed)
                {
                    widthFeet = GeometryHelper.GetThicknessFromPolyline(poly.Points);
                    centerLine = GeometryHelper.GetCenterLine(poly.Points);

                    if (centerLine == null || centerLine.ApproximateLength < 0.001)
                    {
                        var center = GeometryHelper.GetCenter(poly.Points);
                        var (w, h, _) = GeometryHelper.GetDimensions(poly.Points);

                        if (w >= h)
                        {
                            centerLine = Line.CreateBound(
                                new XYZ(center.X - w / 2, center.Y, 0),
                                new XYZ(center.X + w / 2, center.Y, 0));
                        }
                        else
                        {
                            centerLine = Line.CreateBound(
                                new XYZ(center.X, center.Y - h / 2, 0),
                                new XYZ(center.X, center.Y + h / 2, 0));
                        }
                    }
                }
                else
                {
                    centerLine = Line.CreateBound(poly.Points[0], poly.Points[poly.Points.Count - 1]);
                    if (poly.Points.Count > 2)
                    {
                        widthFeet = GeometryHelper.GetThicknessFromPolyline(poly.Points);
                    }
                }

                if (centerLine == null || centerLine.ApproximateLength < 0.001) continue;
                if (widthFeet < 0.001 || heightFeet < 0.001) continue;

                double widthCm = Math.Round(widthFeet * 0.3048 * 100);
                double heightCm = Math.Round(heightFeet * 0.3048 * 100);
                widthFeet = (widthCm / 100) / 0.3048;
                heightFeet = (heightCm / 100) / 0.3048;

                string cacheKey = $"{widthFeet:F4}_{heightFeet:F4}";

                if (!beamTypeCache.ContainsKey(cacheKey))
                {
                    FamilySymbol beamType = beamTypes.FirstOrDefault(b =>
                    {
                        double w = b.LookupParameter("Width")?.AsDouble() ?? 0;
                        double h = b.LookupParameter("Height")?.AsDouble() ?? 0;
                        double bVal = b.LookupParameter("b")?.AsDouble() ?? 0;
                        double hVal = b.LookupParameter("h")?.AsDouble() ?? 0;

                        return (Math.Abs(w - widthFeet) < 0.001 && Math.Abs(h - heightFeet) < 0.001) ||
                               (Math.Abs(bVal - widthFeet) < 0.001 && Math.Abs(hVal - heightFeet) < 0.001);
                    });

                    if (beamType == null)
                    {
                        string baseName = $"Viga_{widthCm}x{heightCm}cm";
                        string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_StructuralFraming);

                        beamType = baseType.Duplicate(uniqueName) as FamilySymbol;
                        if (beamType == null) continue;

                        beamType.LookupParameter("Width")?.Set(widthFeet);
                        beamType.LookupParameter("Height")?.Set(heightFeet);
                        beamType.LookupParameter("b")?.Set(widthFeet);
                        beamType.LookupParameter("h")?.Set(heightFeet);
                    }

                    if (!beamType.IsActive) beamType.Activate();
                    beamTypeCache[cacheKey] = beamType;
                }

                FamilySymbol finalType = beamTypeCache[cacheKey];
                if (finalType == null) continue;

                try
                {
                    FamilyInstance beam = doc.Create.NewFamilyInstance(centerLine, finalType, levelBase, StructuralType.Beam);

                    if (Math.Abs(offsetFeet) > 0.001)
                    {
                        Parameter startOffset = beam.LookupParameter("Start Level Offset");
                        if (startOffset != null && !startOffset.IsReadOnly)
                            startOffset.Set(offsetFeet);

                        Parameter endOffset = beam.LookupParameter("End Level Offset");
                        if (endOffset != null && !endOffset.IsReadOnly)
                            endOffset.Set(offsetFeet);
                    }

                    createdCount++;
                }
                catch { }
            }

            if (createdCount > 0)
            {
                TaskDialog.Show("Aragón Studio", $"✅ Se crearon {createdCount} viga(s) estructural(es) en la capa '{layerName}'.");
            }
        }

        // =========================================================================
        // CREACIÓN DE ZAPATAS AISLADAS POR CONTORNO
        // =========================================================================
        private void CreateFootingsFromContour(Document doc, ScanResult scan, string layerName, Level levelBase,
                                               double thicknessMeters, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;

            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", $"⚠️ No se encontraron polilíneas cerradas en la capa '{layerName}' para crear zapatas.");
                return;
            }

            double thicknessFeet = thicknessMeters / 0.3048;

            var footingFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .Cast<FamilySymbol>()
                .Where(f => !f.Name.ToLower().Contains("wall") &&
                            !f.Name.ToLower().Contains("muro") &&
                            !f.Name.ToLower().Contains("strip") &&
                            !f.Name.ToLower().Contains("corrida") &&
                            !f.Name.ToLower().Contains("mat") &&
                            !f.Name.ToLower().Contains("losa"))
                .ToList();

            if (footingFamilies.Count == 0)
            {
                footingFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                    .Cast<FamilySymbol>()
                    .ToList();
            }

            if (footingFamilies.Count == 0)
            {
                TaskDialog.Show("Aragón Studio",
                    "⚠️ No se encontraron familias de zapatas aisladas en el proyecto.\n" +
                    "Por favor, carga una familia de zapata aislada.");
                return;
            }

            FamilySymbol baseType = null;

            if (!string.IsNullOrEmpty(familyName))
            {
                baseType = footingFamilies.FirstOrDefault(f => f.Name.Contains(familyName));
            }

            if (baseType == null)
            {
                baseType = footingFamilies.FirstOrDefault(f =>
                    f.Name.Contains("Isolated") ||
                    f.Name.Contains("Aislada") ||
                    f.Name.Contains("Zapata") ||
                    f.Name.Contains("Footing"));
            }

            if (baseType == null) baseType = footingFamilies.FirstOrDefault();
            if (baseType == null) return;

            if (!baseType.IsActive) baseType.Activate();

            Dictionary<string, FamilySymbol> footingTypeCache = new Dictionary<string, FamilySymbol>();
            int createdCount = 0;

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;

                var contourPoints = poly.Points.ToList();
                var (widthFeet, heightFeet, _) = GeometryHelper.GetDimensions(contourPoints);

                if (widthFeet < 0.001 || heightFeet < 0.001) continue;

                double widthM = widthFeet * 0.3048;
                double heightM = heightFeet * 0.3048;
                widthM = Math.Round(widthM / 0.01) * 0.01;
                heightM = Math.Round(heightM / 0.01) * 0.01;
                widthFeet = widthM / 0.3048;
                heightFeet = heightM / 0.3048;

                double thicknessFeetRounded = Math.Round(thicknessFeet / 0.01) * 0.01;

                int widthCm = (int)Math.Round(widthM * 100);
                int heightCm = (int)Math.Round(heightM * 100);
                int thickCm = (int)Math.Round(thicknessMeters * 100);
                string cacheKey = $"{widthFeet:F4}_{heightFeet:F4}_{thicknessFeetRounded:F4}";

                FamilySymbol footingType = null;

                if (!footingTypeCache.ContainsKey(cacheKey))
                {
                    footingType = footingFamilies.FirstOrDefault(f =>
                    {
                        double w = GetParameterValue(f, "Width", "b", "Ancho") ?? 0;
                        double h = GetParameterValue(f, "Length", "l", "Largo", "h") ?? 0;
                        double t = GetParameterValue(f, "Thickness", "d", "t", "Espesor") ?? 0;

                        return Math.Abs(w - widthFeet) < 0.001 &&
                               Math.Abs(h - heightFeet) < 0.001 &&
                               Math.Abs(t - thicknessFeetRounded) < 0.001;
                    });

                    if (footingType == null)
                    {
                        string baseName = $"Zapata_{widthCm}x{heightCm}x{thickCm}cm";
                        string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_StructuralFoundation);

                        footingType = baseType.Duplicate(uniqueName) as FamilySymbol;
                        if (footingType == null) continue;

                        SetParameterValue(footingType, widthFeet, "Width", "b", "Ancho");
                        SetParameterValue(footingType, heightFeet, "Length", "l", "Largo", "h");
                        SetParameterValue(footingType, thicknessFeetRounded, "Thickness", "d", "t", "Espesor");

                        if (!footingType.IsActive) footingType.Activate();
                    }

                    footingTypeCache[cacheKey] = footingType;
                }

                footingType = footingTypeCache[cacheKey];
                if (footingType == null) continue;

                XYZ center = GeometryHelper.GetCenter(contourPoints);

                try
                {
                    FamilyInstance footing = doc.Create.NewFamilyInstance(
                        new XYZ(center.X, center.Y, levelBase.Elevation),
                        footingType,
                        levelBase,
                        StructuralType.NonStructural);

                    if (footing != null) createdCount++;
                }
                catch
                {
                    try
                    {
                        FamilyInstance footing = doc.Create.NewFamilyInstance(
                            new XYZ(center.X, center.Y, levelBase.Elevation),
                            footingType,
                            StructuralType.NonStructural);

                        if (footing != null) createdCount++;
                    }
                    catch { }
                }
            }

            if (createdCount > 0)
            {
                TaskDialog.Show("Aragón Studio",
                    $"✅ Se crearon {createdCount} zapata(s) aislada(s) en la capa '{layerName}'.");
            }
            else
            {
                TaskDialog.Show("Aragón Studio",
                    $"⚠️ No se pudo crear ninguna zapata en la capa '{layerName}'.\n" +
                    "Verifica que las polilíneas estén cerradas correctamente.");
            }
        }

        // =========================================================================
        // CREACIÓN DE LOSAS DE CIMENTACIÓN
        // =========================================================================
        private void CreateFoundationSlabs(Document doc, ScanResult scan, string layerName, Level levelBase, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            var allFloorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
            var structuralFloorTypes = allFloorTypes.Where(ft =>
                ft.Name.ToLower().Contains("foundation") ||
                ft.Name.ToLower().Contains("structural") ||
                ft.Name.ToLower().Contains("cimentación") ||
                ft.Name.ToLower().Contains("slab")
            ).ToList();

            if (structuralFloorTypes.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", "⚠️ No se encontraron tipos de losa de cimentación en el proyecto.");
                return;
            }

            FloorType floorType = null;
            if (!string.IsNullOrEmpty(familyName))
                floorType = structuralFloorTypes.FirstOrDefault(ft => ft.Name.Contains(familyName));
            if (floorType == null)
                floorType = structuralFloorTypes.FirstOrDefault();
            if (floorType == null) return;

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;
                CurveLoop curveLoop = GeometryHelper.PointsToCurveLoop(poly.Points);
                if (curveLoop == null || curveLoop.Count() < 3) continue;
                try
                {
                    Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, levelBase.Id, false, null, 0);
                }
                catch { }
            }
        }

        // =========================================================================
        // CREACIÓN DE SUELOS ARQUITECTÓNICOS
        // =========================================================================
        private void CreateFloors(Document doc, ScanResult scan, string layerName, Level levelBase, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            var allFloorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).Cast<FloorType>().ToList();
            var architecturalFloorTypes = allFloorTypes.Where(ft =>
            {
                string name = ft.Name.ToLower();
                return !name.Contains("foundation") &&
                       !name.Contains("cimentación") &&
                       !name.Contains("structural") &&
                       !name.Contains("zapata") &&
                       !name.Contains("footing") &&
                       !name.Contains("slab");
            }).ToList();

            if (architecturalFloorTypes.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", "⚠️ No se encontró ningún tipo de suelo arquitectónico en el proyecto.");
                return;
            }

            FloorType floorType = null;
            if (!string.IsNullOrEmpty(familyName))
                floorType = architecturalFloorTypes.FirstOrDefault(ft => ft.Name.Contains(familyName));
            if (floorType == null)
                floorType = architecturalFloorTypes.FirstOrDefault();
            if (floorType == null) return;

            foreach (var poly in closed)
            {
                if (poly.Points == null || poly.Points.Count < 3) continue;
                CurveLoop curveLoop = GeometryHelper.PointsToCurveLoop(poly.Points);
                if (curveLoop == null || curveLoop.Count() < 3) continue;
                try
                {
                    Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, levelBase.Id, false, null, 0);
                }
                catch { }
            }
        }

        // =========================================================================
        // CREACIÓN DE EJES
        // =========================================================================
        private void CreateGrids(Document doc, ScanResult scan, string layerName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var open = polylines.Where(p => !p.IsClosed).ToList();
            foreach (var poly in open)
            {
                if (poly.Points != null && poly.Points.Count >= 2)
                {
                    Line line = Line.CreateBound(poly.Points[0], poly.Points[poly.Points.Count - 1]);
                    if (line.ApproximateLength > 0.001)
                    {
                        try { Grid.Create(doc, line); } catch { }
                    }
                }
            }
        }

        // =========================================================================
        // CREACIÓN DE CORTES
        // =========================================================================
        private void CreateSectionCuts(Document doc, ScanResult scan, string layerName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var open = polylines.Where(p => !p.IsClosed).ToList();
            if (open.Count == 0) return;

            View3D view3D = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);
            if (view3D == null) return;

            foreach (var poly in open)
            {
                if (poly.Points == null || poly.Points.Count < 2) continue;

                XYZ p1 = poly.Points[0];
                XYZ p2 = poly.Points[poly.Points.Count - 1];
                Line cutLine = Line.CreateBound(p1, p2);
                if (cutLine.ApproximateLength < 0.001) continue;

                XYZ direction = (p2 - p1).Normalize();
                XYZ right = direction.CrossProduct(XYZ.BasisZ).Normalize();
                XYZ center = (p1 + p2) / 2;

                BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                sectionBox.Transform = Transform.Identity;
                sectionBox.Transform.Origin = center;
                sectionBox.Transform.BasisX = direction;
                sectionBox.Transform.BasisY = right;
                sectionBox.Transform.BasisZ = XYZ.BasisZ;

                double length = cutLine.Length;
                double depth = 10;
                double height = 20;

                sectionBox.Min = new XYZ(-length / 2, -depth / 2, -height / 2);
                sectionBox.Max = new XYZ(length / 2, depth / 2, height / 2);

                try
                {
                    ViewSection.CreateSection(doc, view3D.Id, sectionBox);
                }
                catch { }
            }
        }

        // =========================================================================
        // CREACIÓN DE PUERTAS
        // =========================================================================
        private void CreateDoors(Document doc, ScanResult scan, string layerName, Level level, double heightMeters, string familyName)
        {
            if (!scan.BlocksByLayer.TryGetValue(layerName, out var blocks) || blocks.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", $"No se encontraron bloques en la capa '{layerName}' para crear puertas.");
                return;
            }

            double heightFeet = heightMeters / 0.3048;

            FamilySymbol doorType = FindFamilySymbol(doc, "Single-Flush", BuiltInCategory.OST_Doors);
            if (doorType == null)
                doorType = FindFamilySymbol(doc, "Single Flush", BuiltInCategory.OST_Doors);
            if (doorType == null)
                doorType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();

            if (doorType == null)
            {
                TaskDialog.Show("Aragón Studio", "No se encontró ninguna familia de puerta cargada en el proyecto.");
                return;
            }
            if (!doorType.IsActive) doorType.Activate();

            int created = 0;
            foreach (var block in blocks)
            {
                double widthFeet = block.WidthFeet;
                double widthMeters = widthFeet * 0.3048;
                widthMeters = Math.Round(widthMeters / 0.01) * 0.01;
                widthFeet = widthMeters / 0.3048;
                if (widthFeet < 0.001) continue;

                XYZ location = new XYZ(block.Center.X, block.Center.Y, level.Elevation);
                var hostInfo = FindHostWallAndProjectedPoint(doc, location);
                if (hostInfo.Wall == null) continue;

                try
                {
                    FamilyInstance door = doc.Create.NewFamilyInstance(
                        hostInfo.ProjectedPoint, doorType, hostInfo.Wall, level, StructuralType.NonStructural);
                    door.get_Parameter(BuiltInParameter.DOOR_HEIGHT)?.Set(heightFeet);
                    door.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.Set(widthFeet);
                    created++;
                }
                catch { }
            }

            if (created == 0)
                TaskDialog.Show("Aragón Studio", $"No se pudo crear ninguna puerta en la capa '{layerName}'.");
            else
                TaskDialog.Show("Aragón Studio", $"Se crearon {created} puerta(s) en la capa '{layerName}'.");
        }

        // =========================================================================
        // CREACIÓN DE VENTANAS
        // =========================================================================
        private void CreateWindows(Document doc, ScanResult scan, string layerName, Level level,
                                   double heightMeters, double sillMeters, string familyName)
        {
            bool hasBlocks = scan.BlocksByLayer.TryGetValue(layerName, out var blocks) && blocks.Count > 0;
            bool hasPolys = scan.PolylinesByLayer.TryGetValue(layerName, out var polys) && polys.Any(p => p.IsClosed);
            if (!hasBlocks && !hasPolys)
            {
                TaskDialog.Show("Aragón Studio", $"No se encontraron bloques ni polilíneas cerradas en la capa '{layerName}' para crear ventanas.");
                return;
            }

            double heightFeet = heightMeters / 0.3048;
            double sillFeet = sillMeters / 0.3048;
            int created = 0;

            void CreateWindowInstance(XYZ point, double widthFeet, Wall hostWall)
            {
                if (hostWall == null) return;

                FamilySymbol windowType = GetOrCreateWindowType(doc, widthFeet, heightFeet, familyName);
                if (windowType == null) return;
                if (!windowType.IsActive) windowType.Activate();

                try
                {
                    FamilyInstance window = doc.Create.NewFamilyInstance(
                        point, windowType, hostWall, level, StructuralType.NonStructural);

                    JoinGeometryUtils.JoinGeometry(doc, hostWall, window);
                    doc.Regenerate();

                    Parameter sillParam = window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                    if (sillParam != null && !sillParam.IsReadOnly)
                        sillParam.Set(sillFeet);

                    created++;
                }
                catch { }
            }

            if (hasBlocks)
            {
                foreach (var block in blocks)
                {
                    double widthFeet = block.WidthFeet;
                    double widthMeters = widthFeet * 0.3048;
                    widthMeters = Math.Round(widthMeters / 0.01) * 0.01;
                    widthFeet = widthMeters / 0.3048;
                    if (widthFeet < 0.001) continue;

                    XYZ point = new XYZ(block.Center.X, block.Center.Y, level.Elevation);
                    var hostInfo = FindHostWallAndProjectedPoint(doc, point);
                    if (hostInfo.Wall == null) continue;
                    CreateWindowInstance(hostInfo.ProjectedPoint, widthFeet, hostInfo.Wall);
                }
            }

            if (hasPolys)
            {
                var closedPolys = polys.Where(p => p.IsClosed).ToList();
                foreach (var poly in closedPolys)
                {
                    double minX = poly.Points.Min(p => p.X);
                    double maxX = poly.Points.Max(p => p.X);
                    double minY = poly.Points.Min(p => p.Y);
                    double maxY = poly.Points.Max(p => p.Y);

                    double sizeXFeet = maxX - minX;
                    double sizeYFeet = maxY - minY;
                    double widthFeet = Math.Max(sizeXFeet, sizeYFeet);
                    double widthMeters = widthFeet * 0.3048;
                    widthMeters = Math.Round(widthMeters / 0.01) * 0.01;
                    widthFeet = widthMeters / 0.3048;
                    if (widthFeet < 0.001) continue;

                    XYZ center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, level.Elevation);
                    var hostInfo = FindHostWallAndProjectedPoint(doc, center);
                    if (hostInfo.Wall == null) continue;
                    CreateWindowInstance(hostInfo.ProjectedPoint, widthFeet, hostInfo.Wall);
                }
            }

            if (created == 0)
                TaskDialog.Show("Aragón Studio", $"No se pudo crear ninguna ventana en la capa '{layerName}'.");
            else
                TaskDialog.Show("Aragón Studio", $"Se crearon {created} ventana(s) en la capa '{layerName}'.");
        }

        private FamilySymbol GetOrCreateWindowType(Document doc, double widthFeet, double heightFeet, string familyName)
        {
            FamilySymbol baseType = FindFamilySymbol(doc, "Fixed", BuiltInCategory.OST_Windows);
            if (baseType == null)
                baseType = FindFamilySymbol(doc, "Fixed Wall", BuiltInCategory.OST_Windows);
            if (baseType == null)
                baseType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();
            if (baseType == null) return null;

            var allWindowTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .Cast<FamilySymbol>();

            foreach (FamilySymbol wt in allWindowTypes)
            {
                Parameter widthParam = wt.get_Parameter(BuiltInParameter.WINDOW_WIDTH);
                Parameter heightParam = wt.get_Parameter(BuiltInParameter.WINDOW_HEIGHT);
                if (widthParam != null && heightParam != null &&
                    Math.Abs(widthParam.AsDouble() - widthFeet) < 0.001 &&
                    Math.Abs(heightParam.AsDouble() - heightFeet) < 0.001)
                    return wt;
            }

            int widthCm = (int)Math.Round(widthFeet * 0.3048 * 100);
            int heightCm = (int)Math.Round(heightFeet * 0.3048 * 100);
            string baseName = $"Ventana_{widthCm}x{heightCm}cm";
            string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_Windows);

            FamilySymbol newType = baseType.Duplicate(uniqueName) as FamilySymbol;
            if (newType == null) return null;

            Parameter newWidth = newType.get_Parameter(BuiltInParameter.WINDOW_WIDTH);
            if (newWidth != null && !newWidth.IsReadOnly)
                newWidth.Set(widthFeet);
            Parameter newHeight = newType.get_Parameter(BuiltInParameter.WINDOW_HEIGHT);
            if (newHeight != null && !newHeight.IsReadOnly)
                newHeight.Set(heightFeet);

            return newType;
        }

        // =========================================================================
        // VOLTEO DE VENTANAS
        // =========================================================================
        private void FlipAllWindows(Document doc)
        {
            var windows = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Windows)
                .Cast<FamilyInstance>()
                .ToList();

            foreach (FamilyInstance wi in windows)
            {
                bool changed = false;
                if (wi.CanFlipHand)
                {
                    wi.flipHand();
                    changed = true;
                }
                if (wi.CanFlipFacing)
                {
                    wi.flipFacing();
                    changed = true;
                }
            }
            doc.Regenerate();
        }

        // =========================================================================
        // MÉTODOS AUXILIARES
        // =========================================================================

        private string GetUniqueTypeName(Document doc, string baseName, Type elementType, BuiltInCategory category = BuiltInCategory.INVALID)
        {
            HashSet<string> existingNames = new HashSet<string>();

            if (category != BuiltInCategory.INVALID)
            {
                existingNames = new FilteredElementCollector(doc)
                    .OfClass(elementType)
                    .OfCategory(category)
                    .Cast<ElementType>()
                    .Select(et => et.Name)
                    .ToHashSet();
            }
            else
            {
                existingNames = new FilteredElementCollector(doc)
                    .OfClass(elementType)
                    .Cast<ElementType>()
                    .Select(et => et.Name)
                    .ToHashSet();
            }

            if (!existingNames.Contains(baseName)) return baseName;

            int suffix = 1;
            while (existingNames.Contains($"{baseName}_{suffix}")) suffix++;
            return $"{baseName}_{suffix}";
        }

        private FamilySymbol FindFamilySymbol(Document doc, string nameContains, BuiltInCategory category)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(category)
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Name.Contains(nameContains));
        }

        private (Wall Wall, XYZ ProjectedPoint) FindHostWallAndProjectedPoint(Document doc, XYZ point)
        {
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            double tolerance = 1.0;
            Wall closestWall = null;
            XYZ closestPoint = null;
            double minDist = tolerance;

            foreach (Wall wall in walls)
            {
                LocationCurve loc = wall.Location as LocationCurve;
                if (loc == null) continue;

                Curve curve = loc.Curve;
                IntersectionResult result = curve.Project(point);
                if (result == null) continue;

                XYZ projected = result.XYZPoint;
                double dist = projected.DistanceTo(point);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestWall = wall;
                    closestPoint = projected;
                }
            }

            if (closestWall == null)
            {
                XYZ pointAtLevel = new XYZ(point.X, point.Y, 0);
                foreach (Wall wall in walls)
                {
                    LocationCurve loc = wall.Location as LocationCurve;
                    if (loc == null) continue;

                    Curve curve = loc.Curve;
                    IntersectionResult result = curve.Project(pointAtLevel);
                    if (result == null) continue;

                    XYZ projected = result.XYZPoint;
                    double dist = projected.DistanceTo(pointAtLevel);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestWall = wall;
                        closestPoint = projected;
                    }
                }
            }

            return (closestWall, closestPoint);
        }

        private double? GetParameterValue(FamilySymbol symbol, params string[] paramNames)
        {
            if (symbol == null || paramNames == null) return null;

            foreach (string name in paramNames)
            {
                Parameter param = symbol.LookupParameter(name);
                if (param != null && param.HasValue)
                {
                    return param.AsDouble();
                }
            }
            return null;
        }

        private void SetParameterValue(FamilySymbol symbol, double value, params string[] paramNames)
        {
            if (symbol == null || paramNames == null) return;

            foreach (string name in paramNames)
            {
                Parameter param = symbol.LookupParameter(name);
                if (param != null && !param.IsReadOnly)
                {
                    param.Set(value);
                    return;
                }
            }
        }
    }

    public class DwgSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is ImportInstance || elem is RevitLinkInstance;
        public bool AllowReference(Reference refer, XYZ point) => true;
    }
}