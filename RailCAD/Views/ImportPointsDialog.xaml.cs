using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;

using RailCAD.CadInterface;
using RailCAD.Models.Geometry;
using RailCAD.ViewModels;

namespace RailCAD.Views
{
    /// <summary>
    /// WPF Dialog for importing points from text file
    /// </summary>
    internal partial class ImportPointsDialog : Window
    {
        private ImportPointsViewModel _viewModel;

        internal ImportPointsDialog(ICadModel cad)
        {
            InitializeComponent();

            // Create and set the view model
            _viewModel = new ImportPointsViewModel(cad);
            this.DataContext = _viewModel;

            // Subscribe to ViewModel events
            _viewModel.CloseDialogRequested += OnCloseDialogRequested;
            _viewModel.BrowseFileRequested += OnBrowseFileRequested;
            _viewModel.ShowMessageRequested += OnShowMessageRequested;
            _viewModel.ShowPromptRequested += OnShowPromptRequested;
        }

        private void OnCloseDialogRequested(object sender, bool? dialogResult)
        {
            this.DialogResult = dialogResult;
            this.Close();
        }

        private void OnBrowseFileRequested(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = Properties.Resources.FileHandling_TextFiles + " (*.txt;*.csv;*.xyz;*.dat)|*.txt;*.csv;*.xyz;*.dat|" + Properties.Resources.FileHandling_AllFiles + " (*.*)|*.*",
                Title = Properties.Resources.FileHandling_SelectFileWithPoints
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _viewModel.FilePath = openFileDialog.FileName;
            }
        }

        private void OnShowMessageRequested(object sender, MessageEventArgs e)
        {
            MessageBox.Show(e.Message, e.Title, e.Button, e.Icon);
        }

        private void OnShowPromptRequested(object sender, PromptEventArgs e)
        {
            var dialog = new PromptDialog(e.Message, e.DefaultValue);
            e.Result = dialog.ShowDialog();
            if (e.Result == true)
            {
                e.ResponseText = dialog.ResponseText;
            }
        }

        // Handle the Browse button click
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BrowseFileCommand.Execute(null);
        }

        // Handle the Import button click
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ImportCommand.Execute(null);
        }

        // Handle the Cancel button click
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        // Handle the Help button click
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.HelpCommand.Execute(null);
        }

        /// <summary>
        /// Shows a dialog for importing points and returns the number of imported points.
        /// </summary>
        /// <returns>A list of imported points as objects, or null if canceled.</returns>
        internal static IList<RCPoint> ImportPointsFromFile(ICadModel cad)
        {
            // Create and show the dialog
            var dialog = new ImportPointsDialog(cad);
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                // Return imported points from ViewModel
                return dialog._viewModel.ImportedPoints;
            }
            else
            {
                // User cancelled
                return null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks
            if (_viewModel != null)
            {
                _viewModel.CloseDialogRequested -= OnCloseDialogRequested;
                _viewModel.BrowseFileRequested -= OnBrowseFileRequested;
                _viewModel.ShowMessageRequested -= OnShowMessageRequested;
                _viewModel.ShowPromptRequested -= OnShowPromptRequested;
            }

            base.OnClosed(e);
        }
    }
}