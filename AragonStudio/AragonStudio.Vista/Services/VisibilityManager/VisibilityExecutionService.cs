using System.Collections.Generic;
using Autodesk.Revit.DB;
using AragonStudio.Enums.VisibilityManager;
using AragonStudio.Models.VisibilityManager;

namespace AragonStudio.Services.VisibilityManager
{
    public class VisibilityExecutionService
    {
        public VisibilityRequest BuildRequest(
            IList<ElementId> selectedIds,
            List<View> targetViews,
            VisibilityActionType action,
            ScopeType scope)
        {
            return new VisibilityRequest
            {
                ElementIds = selectedIds,
                TargetViews = targetViews,
                ActionType = action,
                Scope = scope
            };
        }

        public bool IsValidRequest(VisibilityRequest request)
        {
            if (request == null) return false;
            if (request.ElementIds == null || request.ElementIds.Count == 0) return false;
            if (request.TargetViews == null || request.TargetViews.Count == 0) return false;
            return true;
        }
    }
}