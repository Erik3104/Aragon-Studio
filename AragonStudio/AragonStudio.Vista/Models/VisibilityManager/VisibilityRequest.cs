using System.Collections.Generic;
using Autodesk.Revit.DB;
using AragonStudio.Enums.VisibilityManager;

namespace AragonStudio.Models.VisibilityManager
{
    public class VisibilityRequest
    {
        public IList<ElementId> ElementIds { get; set; }
        public List<View> TargetViews { get; set; }
        public VisibilityActionType ActionType { get; set; }
        public ScopeType Scope { get; set; }
    }
}