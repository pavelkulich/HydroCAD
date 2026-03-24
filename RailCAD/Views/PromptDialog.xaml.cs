using System;
using System.Windows;

using RailCAD.ViewModels;

namespace RailCAD.Views
{
    /// <summary>
    /// Interaction logic for PromptDialog.xaml
    /// </summary>
    public partial class PromptDialog : Window
    {
        public PromptDialogViewModel ViewModel { get; private set; }

        public string ResponseText => ViewModel?.ResponseText ?? string.Empty;

        public PromptDialog(string promptText, string defaultValue = "")
        {
            InitializeComponent();
            
            ViewModel = new PromptDialogViewModel(promptText, defaultValue);
            ViewModel.DialogResult += OnDialogResult;
            
            DataContext = ViewModel;
            
            // Set focus to text box when loaded
            Loaded += (s, e) => ResponseTextBox.Focus();
        }

        private void OnDialogResult(bool? result)
        {
            DialogResult = result;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.DialogResult -= OnDialogResult;
            }
            base.OnClosed(e);
        }
    }
}