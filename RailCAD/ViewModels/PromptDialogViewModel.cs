using System;
using System.ComponentModel;
using System.Windows.Input;

namespace RailCAD.ViewModels
{
    public class PromptDialogViewModel : INotifyPropertyChanged
    {
        private string _responseText;
        
        public string PromptText { get; }
        
        public string ResponseText
        {
            get => _responseText;
            set
            {
                if (_responseText != value)
                {
                    _responseText = value;
                    OnPropertyChanged(nameof(ResponseText));
                }
            }
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<bool?> DialogResult;

        public PromptDialogViewModel(string promptText, string defaultValue = "")
        {
            PromptText = promptText;
            _responseText = defaultValue;
            
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteOk()
        {
            DialogResult?.Invoke(true);
        }

        private void ExecuteCancel()
        {
            DialogResult?.Invoke(false);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}