using System.ComponentModel;

namespace Global_List_Editor.Model
{
    // This file is auto-generated from the XML schema. Do not modify it manually.
    // To regenerate, use the provided tool or script that processes the XML schema.
    // Ensure that the namespace and class names match

    public partial class GLOBALLISTS : INotifyPropertyChanged
    {

        private List<GLOBALLIST> _globalListField;

        public List<GLOBALLIST> GlobalList
        {
            get
            {
                return this._globalListField;
            }
            set
            {
                this._globalListField = value;
                OnPropertyChanged("GLOBALLIST");
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }

    public partial class GLOBALLIST : INotifyPropertyChanged
    {

        private List<GLOBALLISTLISTITEM> _listItemField;

        private string _nameField;

        private bool _isSelected;

        public List<GLOBALLISTLISTITEM> ListItem
        {
            get
            {
                return this._listItemField;
            }
            set
            {
                this._listItemField = value;
                OnPropertyChanged("LISTITEM");
            }
        }

        public string Name
        {
            get
            {
                return this._nameField;
            }
            set
            {
                this._nameField = value;
                OnPropertyChanged("name");
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public partial class GLOBALLISTLISTITEM : INotifyPropertyChanged
    {

        private string _valueField;

        public string Value
        {
            get
            {
                return this._valueField;
            }
            set
            {
                this._valueField = value;
                OnPropertyChanged("value");
            }
        }

        public bool IsSelected { get; set; }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}

