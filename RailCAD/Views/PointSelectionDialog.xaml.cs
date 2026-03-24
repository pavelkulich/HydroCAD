using System;
using System.Collections.Generic;
using System.Windows;

using RailCAD.CadInterface;
using RailCAD.Models.Geometry;
using RailCAD.ViewModels;

namespace RailCAD.Views
{
    internal partial class PointSelectionDialog : Window
    {
        private PointSelectionViewModel _viewModel;
        private ICadModel cad;
        private enum DialogTagValue
        {
            None,
            SelectPoints,
        }
        private DialogTagValue DialogTag
        {
            get => (DialogTagValue)(this.Tag ?? DialogTagValue.None);
            set => this.Tag = value;
        }

        internal PointSelectionDialog(ICadModel cad, string dialogTitle = null)
        {
            this.cad = cad;
            InitializeComponent();

            _viewModel = new PointSelectionViewModel(cad, dialogTitle);
            _viewModel.InitializeCommands();

            // Subscribe to ViewModel events
            _viewModel.ShowRequested += OnShowRequested;
            _viewModel.DialogCloseRequested += OnDialogCloseRequested;

            this.DataContext = _viewModel;
        }

        internal IList<RCPoint> SelectedPoints => _viewModel.SelectedPoints;

        private void OnShowRequested(object sender, EventArgs e)
        {
            this.DialogResult = false;
            this.DialogTag = DialogTagValue.SelectPoints;
            this.Close();
        }

        private void OnDialogCloseRequested(object sender, bool result)
        {
            this.DialogResult = result;
            this.Close();
        }

        private void PointNumbersText_GotFocus(object sender, RoutedEventArgs e)
        {
            _viewModel.PointNumbersTouched = true;
        }

        private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _viewModel.FilterFieldTouched = true;
        }

        internal static IList<RCPoint> SelectPointsDialog(ICadModel cad, string dialogTitle = "")
        {
            List<RCPoint> selectedPoints = new List<RCPoint>();

            while (true)
            {
                var dialog = new PointSelectionDialog(cad, dialogTitle);

                if (selectedPoints.Count > 0)
                {
                    dialog._viewModel.SetSelectedPoints(selectedPoints);
                }

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    cad.WriteMessageNoDebug(string.Format(Properties.Resources.PointTools_InfoXPointsFound, dialog.SelectedPoints.Count));
                    return dialog.SelectedPoints;
                }
                else if (dialog.DialogTag == DialogTagValue.SelectPoints)
                {
                    IList<RCPoint> points = cad.SelectPointsManually("\n" + dialogTitle);

                    if (points != null)
                    {
                        selectedPoints.Clear();
                        selectedPoints.AddRange(points);
                        continue;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
    }
}