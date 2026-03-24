using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DynamicData.Binding;
using RailCAD.CadInterface;
using RailCAD.Models.Geometry;

namespace RailCAD.ViewModels
{
    /// <summary>
    /// ViewModel for the point selection dialog
    /// </summary>
    internal class PointSelectionViewModel : INotifyPropertyChanged
    {
        private string _pointNumbersText;
        private string _filterText;
        private IList<RCPoint> _selectedPoints;
        private IList<RCPoint> _allPoints;
        private bool _pointsLoaded = false;
        private bool _pointNumbersTouched = false;
        private bool _filterFieldTouched = false;
        private ICadModel cad;
        private string _dialogTitle;

        public string DialogTitle
        {
            get => _dialogTitle;
            set
            {
                if (_dialogTitle != value)
                {
                    _dialogTitle = value;
                    OnPropertyChanged();
                }
            }
        }

        internal PointSelectionViewModel(ICadModel cad, string dialogTitle = null)
        {
            this.cad = cad;
            this.DialogTitle = dialogTitle ?? Properties.Resources.PointTools_PromptSelectPoints;
            _selectedPoints = new List<RCPoint>();
        }

        public string PointNumbersText
        {
            get => _pointNumbersText;
            set
            {
                if (_pointNumbersText != value)
                {
                    _pointNumbersText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PointNumbersTouched
        {
            get => _pointNumbersTouched;
            set
            {
                if (_pointNumbersTouched != value)
                {
                    _pointNumbersTouched = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool FilterFieldTouched
        {
            get => _filterFieldTouched;
            set
            {
                if (_filterFieldTouched != value)
                {
                    _filterFieldTouched = value;
                    OnPropertyChanged();
                }
            }
        }

        public IList<RCPoint> SelectedPoints => _selectedPoints;

        // Commands
        public ICommand ShowCommand { get; private set; }
        public ICommand OkCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        // Events for dialog actions
        public event EventHandler ShowRequested;
        public event EventHandler<bool> DialogCloseRequested;

        internal void InitializeCommands()
        {
            ShowCommand = new RelayCommand(ExecuteShow);
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
            HelpCommand = new RelayCommand(ExecuteHelp);
        }

        private void ExecuteShow()
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteOk()
        {
            try
            {
                if (PointNumbersTouched)
                {
                    ParsePointNumbers();
                }

                if (FilterFieldTouched)
                {
                    ApplyFilter();
                }

                DialogCloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.PointTools_ErrorWhileProcessingPoints, ex.Message),
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExecuteCancel()
        {
            DialogCloseRequested?.Invoke(this, false);
        }

        private void ExecuteHelp()
        {
            MessageBox.Show(
                Properties.Resources.PointTools_SelectionDialogHelpText,
                Properties.Resources.PointTools_SelectionDialogHelpTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        internal void SetSelectedPoints(IList<RCPoint> points)
        {
            _selectedPoints = new List<RCPoint>(points);

            if (points.Count > 0)
            {
                _allPoints = points;
                _pointsLoaded = true;
            }

            UpdatePointNumbersText();
        }

        private void ParsePointNumbers()
        {
            if (!_pointsLoaded)
            {
                LoadPoints();
            }

            var ranges = ParseRangesFromText();

            // sort all points by number
            var orderedPoints = _allPoints.OrderBy(p => p.Number).ToList();

            _selectedPoints.Clear();

            int index = 0;

            foreach (var range in ranges)
            {
                // find first point >= range.Start
                while (index < orderedPoints.Count && orderedPoints[index].Number < range.Start)
                    index++;

                // run through points if they are within interval
                while (index < orderedPoints.Count && orderedPoints[index].Number <= range.End)
                {
                    _selectedPoints.Add(orderedPoints[index]);
                    index++;
                }
            }
        }

        private List<NumberRange> ParseRangesFromText()
        {
            var result = new List<NumberRange>();

            if (string.IsNullOrWhiteSpace(PointNumbersText))
                return result;

            foreach (string part in PointNumbersText.Split(','))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.Contains('-'))
                {
                    var range = trimmed.Split('-');
                    if (range.Length == 2 &&
                        int.TryParse(range[0], out int start) &&
                        int.TryParse(range[1], out int end))
                    {
                        if (end >= start)
                            result.Add(new NumberRange(start, end));
                    }
                }
                else if (int.TryParse(trimmed, out int num))
                {
                    result.Add(new NumberRange(num, num));
                }
            }

            return result;
        }

        private struct NumberRange
        {
            public int Start { get; }
            public int End { get; }

            public NumberRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public override string ToString()
            {
                return Start == End ? Start.ToString() : $"{Start}-{End}";
            }
        }

        private void ApplyFilter()
        {
            if (!FilterFieldTouched)
                return;

            string filter = FilterText ?? string.Empty;
            string pattern = "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            var filteredPoints = _selectedPoints.Where(p => regex.IsMatch(p.Tag ?? string.Empty)).ToList();

            _selectedPoints = new List<RCPoint>(filteredPoints);
        }

        private void LoadPoints()
        {
            _allPoints = cad.SelectAllPoints();
            _pointsLoaded = true;
        }

        private void UpdatePointNumbersText()
        {
            if (_selectedPoints == null || _selectedPoints.Count == 0)
            {
                PointNumbersText = string.Empty;
                return;
            }

            // get sorted point numbers
            var sorted = _selectedPoints.Select(p => p.Number)
                                        .OrderBy(n => n)
                                        .ToList();

            //var ranges = new List<NumberRange>();
            //int start = sorted[0];
            //int prev = sorted[0];

            //for (int i = 1; i < sorted.Count; i++)
            //{
            //    int curr = sorted[i];
            //    if (curr != prev + 1)
            //    {
            //        ranges.Add(new NumberRange(start, prev));
            //        start = curr;
            //    }
            //    prev = curr;
            //}
            //ranges.Add(new NumberRange(start, prev));

            //// create text with point numbers
            //PointNumbersText = string.Join(",", ranges.Select(r => r.ToString()));
            if (sorted.Count == 1)
                PointNumbersText = sorted[0].ToString();
            else
                PointNumbersText = sorted[0] + "-" + sorted[sorted.Count - 1];
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}