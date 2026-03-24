using System.Windows;
using HydroCAD.ViewModels;

namespace HydroCAD.Views
{
    internal partial class DrawProfileDialog : Window
    {
        internal DrawProfileDialog(DrawProfileViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseDialogRequested += (s, result) => { DialogResult = result; Close(); };
        }
    }
}
