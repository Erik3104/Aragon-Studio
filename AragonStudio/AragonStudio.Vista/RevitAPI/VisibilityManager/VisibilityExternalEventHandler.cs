using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AragonStudio.Enums.VisibilityManager;
using AragonStudio.Models.VisibilityManager;

namespace AragonStudio.RevitAPI.VisibilityManager
{
    public class VisibilityExternalEventHandler : IExternalEventHandler
    {
        public VisibilityRequest Request { get; set; }
        public Action<string> OnProgress { get; set; }
        public Action<string> OnStatus { get; set; }

        public void Execute(UIApplication app)
        {
            if (Request == null || Request.ElementIds == null || Request.ElementIds.Count == 0)
            {
                OnStatus?.Invoke("No hay elementos para procesar.");
                return;
            }

            var doc = app.ActiveUIDocument.Document;
            var views = Request.TargetViews;
            if (views == null || views.Count == 0)
            {
                OnStatus?.Invoke("No hay vistas destino.");
                return;
            }

            IList<ElementId> elementsToProcess = null;

            switch (Request.Scope)
            {
                case ScopeType.SelectedOnly:
                    elementsToProcess = Request.ElementIds;
                    break;

                case ScopeType.SameType:
                    var typeIds = new List<ElementId>();
                    foreach (var id in Request.ElementIds)
                    {
                        var elem = doc.GetElement(id);
                        if (elem != null)
                        {
                            var typeId = elem.GetTypeId();
                            if (typeId != ElementId.InvalidElementId && !typeIds.Contains(typeId))
                                typeIds.Add(typeId);
                        }
                    }

                    if (typeIds.Count == 0)
                    {
                        OnStatus?.Invoke("No se encontraron tipos para procesar.");
                        return;
                    }

                    var allInstances = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    var elementsByType = new List<ElementId>();
                    foreach (var typeId in typeIds)
                    {
                        var filtered = allInstances.Where(e => e.GetTypeId() == typeId);
                        elementsByType.AddRange(filtered.Select(e => e.Id));
                    }
                    elementsToProcess = elementsByType;
                    break;

                case ScopeType.Category:
                    var categoryIds = new List<ElementId>();
                    foreach (var id in Request.ElementIds)
                    {
                        var elem = doc.GetElement(id);
                        if (elem?.Category != null)
                        {
                            var catId = elem.Category.Id;
                            if (!categoryIds.Contains(catId))
                                categoryIds.Add(catId);
                        }
                    }

                    if (categoryIds.Count == 0)
                    {
                        OnStatus?.Invoke("No se encontraron categorías para procesar.");
                        return;
                    }

                    var elementsByCategory = new List<ElementId>();
                    foreach (var catId in categoryIds)
                    {
                        var collector = new FilteredElementCollector(doc)
                            .OfCategoryId(catId)
                            .WhereElementIsNotElementType();
                        elementsByCategory.AddRange(collector.ToElementIds());
                    }
                    elementsToProcess = elementsByCategory;
                    break;

                default:
                    elementsToProcess = Request.ElementIds;
                    break;
            }

            if (elementsToProcess == null || elementsToProcess.Count == 0)
            {
                OnStatus?.Invoke("No se encontraron elementos para procesar.");
                return;
            }

            int total = views.Count;
            int processed = 0;

            using (TransactionGroup tg = new TransactionGroup(doc, "Visibilidad BIM"))
            {
                tg.Start();
                foreach (var view in views)
                {
                    using (Transaction t = new Transaction(view.Document, "Aplicar visibilidad"))
                    {
                        t.Start();
                        try
                        {
                            if (Request.ActionType == VisibilityActionType.Hide)
                                view.HideElements(elementsToProcess);
                            else
                                view.UnhideElements(elementsToProcess);
                            t.Commit();
                        }
                        catch
                        {
                            t.RollBack();
                            throw;
                        }
                    }
                    processed++;
                    OnProgress?.Invoke($"Progreso: {processed * 100 / total}%");
                    OnStatus?.Invoke($"Vista {view.Name} ({processed}/{total})");
                }
                tg.Assimilate();
            }
            OnStatus?.Invoke($"✅ Completado. Elementos procesados: {elementsToProcess.Count}");
        }

        public string GetName() => "Visibility External Event";
    }
}