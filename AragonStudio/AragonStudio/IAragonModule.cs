using Autodesk.Revit.UI;

namespace AragonStudio
{
    public interface IAragonModule
    {
        string Name { get; }
        void Register(UIControlledApplication app);
    }
}