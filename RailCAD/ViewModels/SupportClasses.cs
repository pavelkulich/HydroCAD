using System;
using System.Windows;
using System.Windows.Input;

namespace RailCAD.ViewModels
{
    /// <summary>
    /// Simple RelayCommand implementation for MVVM
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }

    // Event argument classes for ViewModel communication
    public class MessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string Title { get; set; }
        public MessageBoxButton Button { get; set; }
        public MessageBoxImage Icon { get; set; }
    }

    public class PromptEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string DefaultValue { get; set; }
        public bool? Result { get; set; }
        public string ResponseText { get; set; }
    }
}
