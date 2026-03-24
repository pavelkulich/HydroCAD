using System.Collections.Generic;
using System.Windows;
using HydroCAD.CadInterface;
using HydroCAD.Models.Geometry;
using HydroCAD.ViewModels;
using Microsoft.Win32;

namespace HydroCAD.Views
{
    internal partial class ImportPointsDialog : Window
    {
        private readonly ImportPointsViewModel _viewModel;

        internal ImportPointsDialog(ICadModel cad)
        {
            InitializeComponent();
            _viewModel = new ImportPointsViewModel(cad);
            _viewModel.CloseDialogRequested += (s, result) => { DialogResult = result; Close(); };
            _viewModel.BrowseFileRequested += OnBrowseFileRequested;
            _viewModel.ShowMessageRequested += OnShowMessageRequested;
            DataContext = _viewModel;
        }

        internal static IList<HCPoint> ImportPointsFromFile(ICadModel cad)
        {
            var dlg = new ImportPointsDialog(cad);
            bool? result = dlg.ShowDialog();
            if (result == true)
                return dlg._viewModel.ImportedPoints;
            return null;
        }

        private void OnBrowseFileRequested(object sender, System.EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select Survey Points File",
                Filter = "Text files (*.txt;*.csv)|*.txt;*.csv|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
                _viewModel.LoadFile(ofd.FileName);
        }

        private void OnShowMessageRequested(object sender, MessageEventArgs e)
        {
            MessageBox.Show(e.Message, "HydroCAD", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
