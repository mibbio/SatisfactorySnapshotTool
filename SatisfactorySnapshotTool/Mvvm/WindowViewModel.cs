
namespace SatisfactorySnapshotTool.Mvvm
{
    public abstract class WindowViewModel : NotifyPropertyChangedBase
    {
        #region Fields
        private string _title = string.Empty;

        private double _width = 100d;

        private double _height = 100d;
        #endregion

        #region Properties
        public string Title
        {
            get => _title;
            set
            {
                if (_title.Equals(value)) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                if (_width == value) return;
                _width = value;
                OnPropertyChanged();
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (_height == value) return;
                _height = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Constructors
        protected WindowViewModel(string title, double width, double height)
        {
            _title = title;
            _width = width;
            _height = height;
        }
        #endregion
    }
}
