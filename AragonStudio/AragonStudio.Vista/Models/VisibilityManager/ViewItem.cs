using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace AragonStudio.Models.VisibilityManager
{
    public class ViewItem : INotifyPropertyChanged
    {
        private string _name;
        private bool _isSelected;
        private Autodesk.Revit.DB.View _view;
        private ObservableCollection<ViewItem> _children = new ObservableCollection<ViewItem>();

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public Autodesk.Revit.DB.View View
        {
            get => _view;
            set { _view = value; OnPropertyChanged(nameof(View)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public ObservableCollection<ViewItem> Children
        {
            get => _children;
            set { _children = value; OnPropertyChanged(nameof(Children)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}