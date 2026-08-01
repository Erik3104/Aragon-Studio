using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AragonStudio.Models.VisibilityManager;

namespace AragonStudio.Services.VisibilityManager
{
    public class VisibilityDataService
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        public VisibilityDataService(UIApplication uiApp)
        {
            if (uiApp == null) throw new ArgumentNullException(nameof(uiApp));
            if (uiApp.ActiveUIDocument == null)
                throw new InvalidOperationException("No hay documento activo.");

            _uiApp = uiApp;
            _uiDoc = uiApp.ActiveUIDocument;
            _doc = _uiDoc.Document;
        }

        public List<ViewItem> GetViewGroups()
        {
            var groups = new List<ViewItem>();
            try
            {
                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v != null && !v.IsTemplate && !(v is View3D))
                    .ToList();

                var grouped = views.GroupBy(v => v.ViewType);
                foreach (var group in grouped)
                {
                    var groupItem = new ViewItem
                    {
                        Name = GetViewTypeDisplayName(group.Key),
                        IsSelected = false
                    };
                    foreach (var view in group.OrderBy(v => v.Name))
                        if (view != null)
                            groupItem.Children.Add(new ViewItem
                            {
                                Name = view.Name,
                                View = view,
                                IsSelected = false
                            });
                    groups.Add(groupItem);
                }

                var views3D = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => v != null && !v.IsTemplate)
                    .ToList();

                if (views3D.Any())
                {
                    var group3D = new ViewItem
                    {
                        Name = "Vistas 3D",
                        IsSelected = false
                    };
                    foreach (var v in views3D.OrderBy(v => v.Name))
                        if (v != null)
                            group3D.Children.Add(new ViewItem
                            {
                                Name = v.Name,
                                View = v,
                                IsSelected = false
                            });
                    groups.Add(group3D);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error al obtener vistas: {ex.Message}");
                return new List<ViewItem>();
            }
            return groups;
        }

        private string GetViewTypeDisplayName(ViewType viewType)
        {
            switch (viewType)
            {
                case ViewType.FloorPlan: return "Plantas";
                case ViewType.CeilingPlan: return "Techos";
                case ViewType.Section: return "Secciones";
                case ViewType.Elevation: return "Elevaciones";
                case ViewType.ThreeD: return "Vistas 3D";
                case ViewType.DraftingView: return "Vistas de Detalle";
                case ViewType.EngineeringPlan: return "Planos de Ingeniería";
                case ViewType.AreaPlan: return "Planos de Área";
                case ViewType.Rendering: return "Renderizados";
                case ViewType.Walkthrough: return "Recorridos";
                case ViewType.Legend: return "Leyendas";
                case ViewType.Schedule: return "Programaciones";
                default: return viewType.ToString();
            }
        }

        public (int count, string categoryName, string typeName) GetSelectionInfo(IList<ElementId> ids)
        {
            if (ids == null || ids.Count == 0)
                return (0, "Ninguna", "Ninguno");

            var firstElem = _doc.GetElement(ids.First());
            if (firstElem == null) return (0, "Ninguna", "Ninguno");

            string categoryName = firstElem.Category?.Name ?? "Sin categoría";
            string typeName = "Varios";

            if (ids.Count == 1)
            {
                var type = _doc.GetElement(firstElem.GetTypeId()) as ElementType;
                typeName = type?.Name ?? "Sin tipo";
            }
            else
            {
                var firstTypeId = firstElem.GetTypeId();
                bool allSameType = ids.All(id =>
                {
                    var e = _doc.GetElement(id);
                    return e != null && e.GetTypeId() == firstTypeId;
                });
                if (allSameType && firstTypeId != ElementId.InvalidElementId)
                {
                    var type = _doc.GetElement(firstTypeId) as ElementType;
                    typeName = type?.Name ?? "Varios";
                }
                else
                {
                    typeName = "Varios";
                }
            }

            return (ids.Count, categoryName, typeName);
        }
    }
}