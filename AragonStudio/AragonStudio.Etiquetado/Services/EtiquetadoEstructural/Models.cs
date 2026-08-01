using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace AragonStudio.Services.EtiquetadoEstructural
{
    public class TaggingRequest
    {
        public List<View> SelectedViews { get; set; }
        public BuiltInCategory StructuralCategory { get; set; }
        public TaggingMode Mode { get; set; }
        public ElementId TagSymbolId { get; set; }
        public bool HasLeader { get; set; }
        public int MaxTagGroups { get; set; }
    }

    public enum TaggingMode { ByType, ByElement, Intelligent }
    public enum SaturationLevel { Low, Medium, High }

    public class AnalysisResult
    {
        public int EstimatedTagCount { get; set; }
        public int ZonesCount { get; set; }
        public SaturationLevel Saturation { get; set; }
        public double SaturationPercent { get; set; }
    }
}