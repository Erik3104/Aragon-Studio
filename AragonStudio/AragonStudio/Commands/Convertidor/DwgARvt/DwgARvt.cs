using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using AragonStudio.UI.Convertidor.DwgARvt;
using AragonStudio.Services.dwgArevit;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Electrical;

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

                using (Transaction trans = new Transaction(doc, "Generar modelo BIM desde DWG"))
                {
                    trans.Start();

                    foreach (var mapping in window.GetLayerMappings())
                    {
                        if (mapping.SelectedCategory?.DisplayName == "Ninguno") continue;
                        string category = mapping.SelectedCategory.DisplayName;
                        string familyName = mapping.SelectedFamily?.Name;

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
                            case "Zapata":
                                CreateFootings(doc, scan, mapping.LayerName, levelBase, footingThick, familyName);
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
                            case "Tuberia":
                                CreatePipes(doc, scan, mapping.LayerName, levelBase, familyName);
                                break;
                            case "Ducto":
                                CreateDucts(doc, scan, mapping.LayerName, levelBase, familyName);
                                break;
                            case "Tubo":
                                CreateConduits(doc, scan, mapping.LayerName, levelBase, familyName);
                                break;
                        }
                    }

                    foreach (var mapping in window.GetLayerMappings())
                    {
                        if (mapping.SelectedCategory?.DisplayName == "Ninguno") continue;
                        string category = mapping.SelectedCategory.DisplayName;
                        string familyName = mapping.SelectedFamily?.Name;

                        switch (category)
                        {
                            case "Puerta":
                                CreateDoors(doc, scan, mapping.LayerName, levelBase, doorHeight, familyName);
                                break;
                            case "Ventana":
                                CreateWindows(doc, scan, mapping.LayerName, levelBase, windowHeight, sillHeight, familyName);
                                break;
                        }
                    }

                    FlipAllWindows(doc);
                    trans.Commit();
                }

                TaskDialog.Show("Aragón Studio", "Modelo BIM generado exitosamente.\nLas ventanas han sido orientadas automáticamente.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"Error al ejecutar el comando:\n{ex.Message}");
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

            Dictionary<int, WallType> wallTypeCache = new Dictionary<int, WallType>();

            foreach (var poly in closed)
            {
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

                if (!wallTypeCache.ContainsKey(grosorCm))
                {
                    WallType wt = GetOrCreateWallType(doc, grosorFeet, familyName, grosorCm);
                    if (wt != null)
                        wallTypeCache[grosorCm] = wt;
                    else
                        continue;
                }
                WallType wallType = wallTypeCache[grosorCm];

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

                Wall wall = Wall.Create(doc, centerLine, wallType.Id, levelBase.Id, grosorFeet, 0, true, false);
                if (wall != null && levelTop.Id != levelBase.Id)
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(levelTop.Id);
            }
        }

        private WallType GetOrCreateWallType(Document doc, double thicknessFeet, string familyName, int thicknessCm)
        {
            var allWallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>();

            foreach (WallType wt in allWallTypes)
            {
                Parameter p = wt.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                if (p != null && Math.Abs(p.AsDouble() - thicknessFeet) < 0.001)
                    return wt;
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
            else
            {
                CompoundStructure cs = newType.GetCompoundStructure();
                if (cs != null && cs.GetLayers().Count > 0)
                {
                    IList<CompoundStructureLayer> layers = cs.GetLayers();
                    layers[0].Width = thicknessFeet;
                    cs.SetLayers(layers);
                    newType.SetCompoundStructure(cs);
                }
            }
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

            foreach (var poly in closed)
            {
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

                FamilySymbol columnType = GetOrCreateColumnType(doc, widthFeetRounded, depthFeetRounded, familyName, widthMeters, depthMeters);
                if (columnType == null) continue;
                if (!columnType.IsActive) columnType.Activate();

                XYZ center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, levelBase.Elevation);
                FamilyInstance column = doc.Create.NewFamilyInstance(center, columnType, levelBase, StructuralType.Column);
                if (column != null && levelTop.Id != levelBase.Id)
                {
                    Parameter topParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topParam != null && topParam.HasValue)
                        topParam.Set(levelTop.Id);
                }
            }
        }

        private FamilySymbol GetOrCreateColumnType(Document doc, double widthFeet, double depthFeet, string familyName, double widthMeters, double depthMeters)
        {
            var cols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>();

            foreach (FamilySymbol fs in cols)
            {
                Parameter w = fs.LookupParameter("b");
                Parameter d = fs.LookupParameter("h");
                if (w != null && d != null &&
                    Math.Abs(w.AsDouble() - widthFeet) < 0.001 &&
                    Math.Abs(d.AsDouble() - depthFeet) < 0.001)
                    return fs;
            }

            FamilySymbol baseType = cols.FirstOrDefault(f => f.Name.Contains(familyName ?? "Concrete Column")) ?? cols.FirstOrDefault();
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
        // CREACIÓN DE VIGAS DE CIMENTACIÓN (CON DESFASE)
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

            FamilySymbol baseType = beamTypes.FirstOrDefault(b => b.Name.Contains(familyName ?? "Concrete Rectangular Beam")) ?? beamTypes.FirstOrDefault();
            if (baseType == null) return;

            Dictionary<string, FamilySymbol> beamTypeCache = new Dictionary<string, FamilySymbol>();

            foreach (var poly in closed)
            {
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

                Line axis = GetCenterLineFromPolygon(poly.Points);
                if (axis.ApproximateLength < 0.001) continue;

                FamilyInstance beam = doc.Create.NewFamilyInstance(axis, finalType, levelBase, StructuralType.Beam);

                if (Math.Abs(offsetFeet) > 0.001)
                {
                    Parameter startOffset = beam.LookupParameter("Start Level Offset");
                    if (startOffset != null && !startOffset.IsReadOnly)
                        startOffset.Set(offsetFeet);

                    Parameter endOffset = beam.LookupParameter("End Level Offset");
                    if (endOffset != null && !endOffset.IsReadOnly)
                        endOffset.Set(offsetFeet);

                    if (startOffset == null)
                    {
                        startOffset = beam.LookupParameter("Start Offset");
                        if (startOffset != null && !startOffset.IsReadOnly)
                            startOffset.Set(offsetFeet);
                    }
                    if (endOffset == null)
                    {
                        endOffset = beam.LookupParameter("End Offset");
                        if (endOffset != null && !endOffset.IsReadOnly)
                            endOffset.Set(offsetFeet);
                    }
                }
            }
        }

        // =========================================================================
        // CREACIÓN DE ZAPATAS
        // =========================================================================
        private void CreateFootings(Document doc, ScanResult scan, string layerName, Level levelBase,
                                    double thicknessMeters, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var closed = polylines.Where(p => p.IsClosed).ToList();
            if (closed.Count == 0) return;

            double thicknessFeet = thicknessMeters / 0.3048;

            var footingTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .Cast<FamilySymbol>()
                .ToList();

            if (footingTypes.Count == 0) return;

            FamilySymbol baseType = footingTypes.FirstOrDefault(f => f.Name.Contains(familyName ?? "Isolated")) ?? footingTypes.FirstOrDefault();
            if (baseType == null) return;

            Dictionary<string, FamilySymbol> footingTypeCache = new Dictionary<string, FamilySymbol>();

            foreach (var poly in closed)
            {
                double minX = poly.Points.Min(p => p.X);
                double maxX = poly.Points.Max(p => p.X);
                double minY = poly.Points.Min(p => p.Y);
                double maxY = poly.Points.Max(p => p.Y);

                double widthFeet = maxX - minX;
                double lengthFeet = maxY - minY;
                if (widthFeet < 0.001 || lengthFeet < 0.001) continue;

                double widthM = widthFeet * 0.3048;
                double lengthM = lengthFeet * 0.3048;
                widthM = Math.Round(widthM / 0.01) * 0.01;
                lengthM = Math.Round(lengthM / 0.01) * 0.01;
                widthFeet = widthM / 0.3048;
                lengthFeet = lengthM / 0.3048;

                string key = $"{widthFeet:F4}_{lengthFeet:F4}_{thicknessFeet:F4}";

                if (!footingTypeCache.ContainsKey(key))
                {
                    FamilySymbol ft = footingTypes.FirstOrDefault(f =>
                    {
                        double w = f.LookupParameter("Width")?.AsDouble() ?? 0;
                        double l = f.LookupParameter("Length")?.AsDouble() ?? 0;
                        double t = f.LookupParameter("Thickness")?.AsDouble() ?? 0;
                        return Math.Abs(w - widthFeet) < 0.001 &&
                               Math.Abs(l - lengthFeet) < 0.001 &&
                               Math.Abs(t - thicknessFeet) < 0.001;
                    });

                    if (ft == null)
                    {
                        int widthCm = (int)Math.Round(widthM * 100);
                        int lengthCm = (int)Math.Round(lengthM * 100);
                        int thickCm = (int)Math.Round(thicknessMeters * 100);
                        string baseName = $"Zapata_{widthCm}x{lengthCm}x{thickCm}cm";
                        string uniqueName = GetUniqueTypeName(doc, baseName, typeof(FamilySymbol), BuiltInCategory.OST_StructuralFoundation);

                        ft = baseType.Duplicate(uniqueName) as FamilySymbol;
                        if (ft == null) continue;

                        ft.LookupParameter("Width")?.Set(widthFeet);
                        ft.LookupParameter("Length")?.Set(lengthFeet);
                        ft.LookupParameter("Thickness")?.Set(thicknessFeet);
                        ft.LookupParameter("b")?.Set(widthFeet);
                        ft.LookupParameter("h")?.Set(lengthFeet);
                        ft.LookupParameter("d")?.Set(thicknessFeet);
                    }

                    if (!ft.IsActive) ft.Activate();
                    footingTypeCache[key] = ft;
                }

                FamilySymbol finalType = footingTypeCache[key];
                if (finalType == null) continue;

                XYZ center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, levelBase.Elevation);
                doc.Create.NewFamilyInstance(center, finalType, levelBase, StructuralType.NonStructural);
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
                ft.Name.ToLower().Contains("cimentación")
            ).ToList();

            if (structuralFloorTypes.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", "No se encontraron tipos de losa de cimentación en el proyecto.");
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
                CurveLoop curveLoop = PointsToCurveLoop(poly.Points);
                if (curveLoop == null || curveLoop.Count() < 3) continue;
                Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, levelBase.Id, false, null, 0);
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
                       !name.Contains("footing");
            }).ToList();

            if (architecturalFloorTypes.Count == 0)
            {
                TaskDialog.Show("Aragón Studio", "No se encontró ningún tipo de suelo arquitectónico en el proyecto.");
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
                CurveLoop curveLoop = PointsToCurveLoop(poly.Points);
                if (curveLoop == null || curveLoop.Count() < 3) continue;
                Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorType.Id, levelBase.Id, false, null, 0);
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
                if (poly.Points.Count >= 2)
                {
                    Line line = Line.CreateBound(poly.Points[0], poly.Points[poly.Points.Count - 1]);
                    if (line.ApproximateLength > 0.001)
                        Grid.Create(doc, line);
                }
            }
        }

        // =========================================================================
        // CREACIÓN DE CORTES (SECCIONES)
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
                if (poly.Points.Count < 2) continue;

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
        // CREACIÓN DE TUBERÍAS (PIPES) - CORREGIDO CON PIPE.CREATE
        // =========================================================================
        private void CreatePipes(Document doc, ScanResult scan, string layerName, Level level, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var open = polylines.Where(p => !p.IsClosed).ToList();
            if (open.Count == 0) return;

            PipeType pipeType = null;
            var allPipeTypes = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().ToList();
            if (!string.IsNullOrEmpty(familyName))
                pipeType = allPipeTypes.FirstOrDefault(pt => pt.Name.Contains(familyName));
            if (pipeType == null)
                pipeType = allPipeTypes.FirstOrDefault();
            if (pipeType == null)
            {
                TaskDialog.Show("Aragón Studio", "No se encontró ningún tipo de tubería en el proyecto.");
                return;
            }

            foreach (var poly in open)
            {
                for (int i = 0; i < poly.Points.Count - 1; i++)
                {
                    XYZ start = poly.Points[i];
                    XYZ end = poly.Points[i + 1];
                    if (start.DistanceTo(end) < 0.001) continue;
                    try
                    {
                        // Sobrecarga correcta: Pipe.Create(Document, ElementId pipeTypeId, ElementId levelId, XYZ start, XYZ end)
                        Pipe.Create(doc, pipeType.Id, level.Id, start, end);
                    }
                    catch { }
                }
            }
        }

        // =========================================================================
        // CREACIÓN DE CONDUCTOS (DUCTS) - CORREGIDO CON DUCT.CREATE
        // =========================================================================
        private void CreateDucts(Document doc, ScanResult scan, string layerName, Level level, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var open = polylines.Where(p => !p.IsClosed).ToList();
            if (open.Count == 0) return;

            DuctType ductType = null;
            var allDuctTypes = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().ToList();
            if (!string.IsNullOrEmpty(familyName))
                ductType = allDuctTypes.FirstOrDefault(dt => dt.Name.Contains(familyName));
            if (ductType == null)
                ductType = allDuctTypes.FirstOrDefault();
            if (ductType == null)
            {
                TaskDialog.Show("Aragón Studio", "No se encontró ningún tipo de conducto en el proyecto.");
                return;
            }

            foreach (var poly in open)
            {
                for (int i = 0; i < poly.Points.Count - 1; i++)
                {
                    XYZ start = poly.Points[i];
                    XYZ end = poly.Points[i + 1];
                    if (start.DistanceTo(end) < 0.001) continue;
                    try
                    {
                        // Sobrecarga correcta: Duct.Create(Document, ElementId ductTypeId, ElementId levelId, XYZ start, XYZ end)
                        Duct.Create(doc, ductType.Id, level.Id, start, end);
                    }
                    catch { }
                }
            }
        }

        // =========================================================================
        // CREACIÓN DE TUBOS ELÉCTRICOS (CONDUITS) - CORREGIDO CON CONDUIT.CREATE
        // =========================================================================
        private void CreateConduits(Document doc, ScanResult scan, string layerName, Level level, string familyName)
        {
            if (!scan.PolylinesByLayer.TryGetValue(layerName, out var polylines)) return;
            var open = polylines.Where(p => !p.IsClosed).ToList();
            if (open.Count == 0) return;

            ConduitType conduitType = null;
            var allConduitTypes = new FilteredElementCollector(doc).OfClass(typeof(ConduitType)).Cast<ConduitType>().ToList();
            if (!string.IsNullOrEmpty(familyName))
                conduitType = allConduitTypes.FirstOrDefault(ct => ct.Name.Contains(familyName));
            if (conduitType == null)
                conduitType = allConduitTypes.FirstOrDefault();
            if (conduitType == null)
            {
                TaskDialog.Show("Aragón Studio", "No se encontró ningún tipo de tubo eléctrico en el proyecto.");
                return;
            }

            foreach (var poly in open)
            {
                for (int i = 0; i < poly.Points.Count - 1; i++)
                {
                    XYZ start = poly.Points[i];
                    XYZ end = poly.Points[i + 1];
                    if (start.DistanceTo(end) < 0.001) continue;
                    try
                    {
                        // Sobrecarga correcta: Conduit.Create(Document, ElementId conduitTypeId, ElementId levelId, XYZ start, XYZ end)
                        Conduit.Create(doc, conduitType.Id, level.Id, start, end);
                    }
                    catch { }
                }
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
                TaskDialog.Show("Aragón Studio", $"No se pudo crear ninguna puerta en la capa '{layerName}'. Verifique que haya muros cercanos.");
            else
                TaskDialog.Show("Aragón Studio", $"Se crearon {created} puerta(s) en la capa '{layerName}'.");
        }

        // =========================================================================
        // CREACIÓN DE VENTANAS (CON ANTEPECHO CORREGIDO)
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
                    else
                    {
                        sillParam = window.LookupParameter("Sill Height");
                        if (sillParam != null && !sillParam.IsReadOnly)
                            sillParam.Set(sillFeet);
                        else
                        {
                            sillParam = window.LookupParameter("Sill Height Offset");
                            if (sillParam != null && !sillParam.IsReadOnly)
                                sillParam.Set(sillFeet);
                        }
                    }

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
                TaskDialog.Show("Aragón Studio", $"No se pudo crear ninguna ventana en la capa '{layerName}'. Verifique que haya muros cercanos (tolerancia 1 pie).");
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
        // VOLTEO AUTOMÁTICO DE VENTANAS
        // =========================================================================
        private void FlipAllWindows(Document doc)
        {
            var windows = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Windows)
                .Cast<FamilyInstance>()
                .ToList();

            int flippedCount = 0;
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
                if (changed) flippedCount++;
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

        private CurveLoop PointsToCurveLoop(IList<XYZ> points)
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

        private Line GetCenterLineFromPolygon(List<XYZ> points)
        {
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double width = maxX - minX;
            double height = maxY - minY;

            if (width >= height)
                return Line.CreateBound(
                    new XYZ(minX, (minY + maxY) / 2, 0),
                    new XYZ(maxX, (minY + maxY) / 2, 0));
            else
                return Line.CreateBound(
                    new XYZ((minX + maxX) / 2, minY, 0),
                    new XYZ((minX + maxX) / 2, maxY, 0));
        }
    }

    public class DwgSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is ImportInstance || elem is RevitLinkInstance;
        public bool AllowReference(Reference refer, XYZ point) => true;
    }
}