using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace AragonStudio.Services.EtiquetadoEstructural
{
    public class RebarGroupItem
    {
        public Element SourceElement;
        public string SourceType;
        public double Diameter;
        public int ShapeId;
        public int LayoutRule;
        public double Spacing;
    }

    public class StructuralIntentEngine
    {
        private UIApplication _uiApp;
        private UIDocument _uidoc;
        private Document _doc;

        public StructuralIntentEngine(UIApplication uiApp, UIDocument uidoc, Document doc)
        {
            _uiApp = uiApp;
            _uidoc = uidoc;
            _doc = doc;
        }

        public AnalysisResult EstimateImpact(TaggingRequest request)
        {
            if (request == null || request.SelectedViews == null || request.SelectedViews.Count == 0)
                return new AnalysisResult { EstimatedTagCount = 0, ZonesCount = 0, Saturation = SaturationLevel.Low, SaturationPercent = 0 };

            int totalEtiquetas = 0;
            int totalGrupos = 0;

            foreach (View view in request.SelectedViews)
            {
                if (view == null) continue;

                if (request.StructuralCategory == BuiltInCategory.OST_Rebar && request.Mode == TaggingMode.Intelligent)
                {
                    List<RebarGroupItem> todasArmaduras = ObtenerTodasLasArmadurasEnVista(view);
                    var grupos = AgruparArmaduras(todasArmaduras);
                    totalGrupos += grupos.Count;
                    foreach (var grupo in grupos)
                        totalEtiquetas += Math.Min(request.MaxTagGroups, grupo.Count);
                }
                else
                {
                    var elements = new FilteredElementCollector(_doc, view.Id)
                        .OfCategory(request.StructuralCategory)
                        .WhereElementIsNotElementType()
                        .Cast<Element>()
                        .ToList();

                    if (request.Mode == TaggingMode.ByType)
                    {
                        var gruposPorTipo = elements.GroupBy(e => e.GetTypeId());
                        totalEtiquetas += gruposPorTipo.Count();
                    }
                    else
                    {
                        totalEtiquetas += elements.Count;
                    }
                }
            }

            double percent = Math.Min(100, (totalEtiquetas / 100.0) * 1.5);
            SaturationLevel sat = totalEtiquetas < 30 ? SaturationLevel.Low : (totalEtiquetas < 80 ? SaturationLevel.Medium : SaturationLevel.High);

            return new AnalysisResult
            {
                EstimatedTagCount = totalEtiquetas,
                ZonesCount = totalGrupos > 0 ? totalGrupos : (totalEtiquetas / 5 + 1),
                Saturation = sat,
                SaturationPercent = percent
            };
        }

        public void ExecuteTagging(TaggingRequest request)
        {
            if (request == null || request.SelectedViews == null || request.SelectedViews.Count == 0)
                return;

            using (Transaction trans = new Transaction(_doc, "Etiquetado Estructural Inteligente"))
            {
                trans.Start();

                try
                {
                    FamilySymbol tagSymbol = null;
                    if (request.TagSymbolId != null && request.TagSymbolId != ElementId.InvalidElementId)
                    {
                        tagSymbol = _doc.GetElement(request.TagSymbolId) as FamilySymbol;
                        if (tagSymbol != null && !tagSymbol.IsActive)
                        {
                            tagSymbol.Activate();
                            _doc.Regenerate();
                        }
                    }

                    foreach (View view in request.SelectedViews)
                    {
                        if (view == null) continue;

                        if (request.Mode == TaggingMode.Intelligent && request.StructuralCategory == BuiltInCategory.OST_Rebar)
                        {
                            List<RebarGroupItem> todasArmaduras = ObtenerTodasLasArmadurasEnVista(view);
                            if (todasArmaduras.Count == 0) continue;

                            var grupos = AgruparArmaduras(todasArmaduras);
                            if (grupos.Count == 0) continue;

                            int tagsPorGrupo = request.MaxTagGroups;
                            foreach (var grupo in grupos)
                            {
                                int count = Math.Min(tagsPorGrupo, grupo.Count);
                                for (int i = 0; i < count; i++)
                                {
                                    var item = grupo[i];
                                    CreateTag(item.SourceElement, view, tagSymbol, request.HasLeader);
                                }
                            }
                        }
                        else
                        {
                            var elements = new FilteredElementCollector(_doc, view.Id)
                                                .OfCategory(request.StructuralCategory)
                                                .WhereElementIsNotElementType()
                                                .Cast<Element>()
                                                .ToList();

                            if (request.Mode == TaggingMode.ByType)
                            {
                                var grupos = elements.GroupBy(e => e.GetTypeId());
                                foreach (var grupo in grupos)
                                {
                                    var elem = grupo.First();
                                    CreateTag(elem, view, tagSymbol, request.HasLeader);
                                }
                            }
                            else
                            {
                                foreach (Element elem in elements)
                                {
                                    CreateTag(elem, view, tagSymbol, request.HasLeader);
                                }
                            }
                        }
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("Error en etiquetado", $"Ocurrió un error: {ex.Message}\n\nLa operación se ha cancelado.");
                    throw;
                }
            }
        }

        private List<RebarGroupItem> ObtenerTodasLasArmadurasEnVista(View view)
        {
            List<RebarGroupItem> resultado = new List<RebarGroupItem>();
            HashSet<ElementId> idsProcesados = new HashSet<ElementId>();

            var barrasRebar = new FilteredElementCollector(_doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .ToList();

            foreach (var elem in barrasRebar)
            {
                if (idsProcesados.Contains(elem.Id)) continue;
                idsProcesados.Add(elem.Id);

                if (elem is Rebar rebar)
                {
                    var item = ConvertirRebarARebarGroupItem(rebar);
                    if (item != null) resultado.Add(item);
                }
                else
                {
                    var item = ConvertirElementoGenericoARebarGroupItem(elem);
                    if (item != null) resultado.Add(item);
                }
            }

            var areas = new FilteredElementCollector(_doc, view.Id)
                .OfClass(typeof(AreaReinforcement))
                .Cast<AreaReinforcement>()
                .ToList();

            foreach (var area in areas)
            {
                if (idsProcesados.Contains(area.Id)) continue;
                idsProcesados.Add(area.Id);

                IList<ElementId> rebarInSystemIds = area.GetRebarInSystemIds();
                foreach (ElementId id in rebarInSystemIds)
                {
                    Element rebarElem = _doc.GetElement(id);
                    if (rebarElem != null && !idsProcesados.Contains(rebarElem.Id))
                    {
                        idsProcesados.Add(rebarElem.Id);
                        var item = ConvertirElementoGenericoARebarGroupItem(rebarElem);
                        if (item != null) resultado.Add(item);
                    }
                }
            }

            var paths = new FilteredElementCollector(_doc, view.Id)
                .OfClass(typeof(PathReinforcement))
                .Cast<PathReinforcement>()
                .ToList();

            foreach (var path in paths)
            {
                if (idsProcesados.Contains(path.Id)) continue;
                idsProcesados.Add(path.Id);

                IList<ElementId> rebarInSystemIds = path.GetRebarInSystemIds();
                foreach (ElementId id in rebarInSystemIds)
                {
                    Element rebarElem = _doc.GetElement(id);
                    if (rebarElem != null && !idsProcesados.Contains(rebarElem.Id))
                    {
                        idsProcesados.Add(rebarElem.Id);
                        var item = ConvertirElementoGenericoARebarGroupItem(rebarElem);
                        if (item != null) resultado.Add(item);
                    }
                }
            }

            return resultado;
        }

        private RebarGroupItem ConvertirRebarARebarGroupItem(Rebar rebar)
        {
            try
            {
                RebarGroupItem item = new RebarGroupItem();
                item.SourceElement = rebar;
                item.SourceType = "Rebar";

                item.Diameter = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0;
                item.Diameter = Math.Round(item.Diameter, 2);


                item.LayoutRule = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE)?.AsInteger() ?? 0;

                item.Spacing = 0;
                if (rebar.LayoutRule != RebarLayoutRule.Single)
                    item.Spacing = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble() ?? 0;
                item.Spacing = Math.Round(item.Spacing, 2);

                item.ShapeId = rebar.GetShapeId().GetHashCode();
                return item;
            }
            catch
            {
                return null;
            }
        }

        private RebarGroupItem ConvertirElementoGenericoARebarGroupItem(Element elem)
        {
            try
            {
                RebarGroupItem item = new RebarGroupItem();
                item.SourceElement = elem;
                item.SourceType = elem.GetType().Name;

                item.Diameter = elem.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0;
                item.Diameter = Math.Round(item.Diameter, 2);

                var layoutRuleParam = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE);
                if (layoutRuleParam != null)
                    item.LayoutRule = layoutRuleParam.AsInteger();
                else
                    item.LayoutRule = 0;

                item.Spacing = elem.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble() ?? 0;
                item.Spacing = Math.Round(item.Spacing, 2);

                item.ShapeId = 0;
                return item;
            }
            catch
            {
                return null;
            }
        }

        private List<List<RebarGroupItem>> AgruparArmaduras(List<RebarGroupItem> armaduras)
        {
            var dict = new Dictionary<string, List<RebarGroupItem>>();

            foreach (var item in armaduras)
            {
                string key = $"{item.Diameter}|{item.ShapeId}|{item.LayoutRule}|{item.Spacing}";
                if (!dict.ContainsKey(key))
                    dict[key] = new List<RebarGroupItem>();
                dict[key].Add(item);
            }

            var result = dict.Values.ToList();
            result.Sort((a, b) => b.Count.CompareTo(a.Count));
            return result;
        }

        private void CreateTag(Element element, View view, FamilySymbol tagSymbol, bool hasLeader)
        {
            if (element == null || view == null) return;

            Reference refToTag = ObtenerReferenciaElemento(element);
            if (refToTag == null) return;

            XYZ center = GetElementCenter(element, view);
            if (center == null) return;

            try
            {
                IndependentTag tag = IndependentTag.Create(_doc, view.Id, refToTag, hasLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, center);
                if (tagSymbol != null && tagSymbol.Id != tag.Id)
                    tag.ChangeTypeId(tagSymbol.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando etiqueta para elemento {element.Id}: {ex.Message}");
            }
        }

        private Reference ObtenerReferenciaElemento(Element element)
        {
            if (element == null) return null;
            try
            {
                if (element is AreaReinforcement area && area.GetSubelements().Count > 0)
                    return area.GetSubelements()[0].GetReference();
                if (element is PathReinforcement path && path.GetSubelements().Count > 0)
                    return path.GetSubelements()[0].GetReference();
                if (element is Rebar rebar)
                {
                    var sub = rebar.GetSubelements();
                    return sub.Count > 0 ? sub[0].GetReference() : new Reference(rebar);
                }
                if (element.GetType().Name == "RebarInSystem")
                {
                    var method = element.GetType().GetMethod("GetSubelements");
                    if (method != null)
                    {
                        var subelems = method.Invoke(element, null) as System.Collections.IList;
                        if (subelems != null && subelems.Count > 0)
                        {
                            var getRef = subelems[0].GetType().GetMethod("GetReference");
                            if (getRef != null)
                                return getRef.Invoke(subelems[0], null) as Reference;
                        }
                    }
                    return new Reference(element);
                }
                return new Reference(element);
            }
            catch
            {
                return null;
            }
        }

        private XYZ GetElementCenter(Element element, View view)
        {
            if (element == null) return null;
            if (element.Location is LocationPoint lp)
                return lp.Point;
            if (element.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            BoundingBoxXYZ bb = element.get_BoundingBox(view);
            if (bb != null)
                return (bb.Min + bb.Max) / 2;

            return null;
        }
    }
}