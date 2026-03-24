using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HydroCAD.CadInterface;
using HydroCAD.Models.Geometry;

namespace HydroCAD.ViewModels
{
    internal class ImportPointsViewModel : INotifyPropertyChanged
    {
        private string _filePath;
        private string _filePreview;
        private double _scaleX = 1.0, _scaleY = 1.0, _scaleZ = 1.0;
        private double _offsetX = 0.0, _offsetY = 0.0, _offsetZ = 0.0;
        private IList<string> _columnTypes;
        private ObservableCollection<string> _selectedColumnTypes;
        private List<string[]> _fileData;
        private bool _hasHeader = false;
        private char _delimiter = ' ';
        private readonly ICadModel cad;
        private IList<HCPoint> _importedPoints;

        public ICommand BrowseFileCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public event EventHandler<bool?> CloseDialogRequested;
        public event EventHandler BrowseFileRequested;
        public event EventHandler<MessageEventArgs> ShowMessageRequested;
        public event PropertyChangedEventHandler PropertyChanged;

        internal ImportPointsViewModel(ICadModel cad)
        {
            this.cad = cad;
            _importedPoints = new List<HCPoint>();
            BrowseFileCommand = new RelayCommand(ExecuteBrowseFile);
            ImportCommand = new RelayCommand(ExecuteImport);
            CancelCommand = new RelayCommand(ExecuteCancel);

            _columnTypes = new List<string>
            {
                "Point Number", "Easting (X)", "Northing (Y)", "Elevation (Z)", "Tag/Note", "Ignore"
            };
            _selectedColumnTypes = new ObservableCollection<string>
            {
                "Ignore", "Ignore", "Ignore", "Ignore", "Ignore"
            };
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                    if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                        LoadFilePreview();
                }
            }
        }

        public string FilePreview
        {
            get => _filePreview;
            set { if (_filePreview != value) { _filePreview = value; OnPropertyChanged(); } }
        }

        public double ScaleX  { get => _scaleX;  set { _scaleX  = value; OnPropertyChanged(); } }
        public double ScaleY  { get => _scaleY;  set { _scaleY  = value; OnPropertyChanged(); } }
        public double ScaleZ  { get => _scaleZ;  set { _scaleZ  = value; OnPropertyChanged(); } }
        public double OffsetX { get => _offsetX; set { _offsetX = value; OnPropertyChanged(); } }
        public double OffsetY { get => _offsetY; set { _offsetY = value; OnPropertyChanged(); } }
        public double OffsetZ { get => _offsetZ; set { _offsetZ = value; OnPropertyChanged(); } }

        public IList<string> ColumnTypes
        {
            get => _columnTypes;
            set { _columnTypes = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> SelectedColumnTypes
        {
            get => _selectedColumnTypes;
            set { _selectedColumnTypes = value; OnPropertyChanged(); }
        }

        public IList<HCPoint> ImportedPoints => _importedPoints;

        public void LoadFile(string path)
        {
            FilePath = path;
        }

        private void LoadFilePreview()
        {
            try
            {
                var lines = File.ReadLines(_filePath).Take(20);
                FilePreview = string.Join(Environment.NewLine, lines);
                _fileData = File.ReadAllLines(_filePath)
                                .Select(l => l.Split(new[] { ' ', ',', '\t', ';' },
                                                     StringSplitOptions.RemoveEmptyEntries))
                                .Where(parts => parts.Length > 0)
                                .ToList();
            }
            catch (Exception ex)
            {
                FilePreview = $"Error reading file: {ex.Message}";
            }
        }

        private void ExecuteBrowseFile() => BrowseFileRequested?.Invoke(this, EventArgs.Empty);

        private void ExecuteImport()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                ShowMessageRequested?.Invoke(this, new MessageEventArgs("Please select a valid file."));
                return;
            }

            var colMap = GetColumnMapping();
            if (!colMap.ContainsKey("Easting (X)") || !colMap.ContainsKey("Northing (Y)"))
            {
                ShowMessageRequested?.Invoke(this, new MessageEventArgs("Please map at least X and Y columns."));
                return;
            }

            _importedPoints = ParsePoints(colMap);
            if (_importedPoints.Count == 0)
            {
                ShowMessageRequested?.Invoke(this, new MessageEventArgs("No valid points found in file."));
                return;
            }

            CloseDialogRequested?.Invoke(this, true);
        }

        private void ExecuteCancel() => CloseDialogRequested?.Invoke(this, false);

        private Dictionary<string, int> GetColumnMapping()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < _selectedColumnTypes.Count; i++)
            {
                string type = _selectedColumnTypes[i];
                if (type != "Ignore" && !map.ContainsKey(type))
                    map[type] = i;
            }
            return map;
        }

        private IList<HCPoint> ParsePoints(Dictionary<string, int> colMap)
        {
            var points = new List<HCPoint>();
            int startNum = 1;
            IEnumerable<string[]> rows = _hasHeader && _fileData?.Count > 0
                ? _fileData.Skip(1) : _fileData ?? Enumerable.Empty<string[]>();

            foreach (var parts in rows)
            {
                try
                {
                    int colX = colMap["Easting (X)"];
                    int colY = colMap["Northing (Y)"];
                    if (colX >= parts.Length || colY >= parts.Length) continue;

                    double x = ParseDouble(parts[colX]) * _scaleX + _offsetX;
                    double y = ParseDouble(parts[colY]) * _scaleY + _offsetY;
                    double z = colMap.TryGetValue("Elevation (Z)", out int colZ) && colZ < parts.Length
                               ? ParseDouble(parts[colZ]) * _scaleZ + _offsetZ : 0.0;

                    int num = colMap.TryGetValue("Point Number", out int colN) && colN < parts.Length
                              ? int.TryParse(parts[colN], out int n) ? n : startNum++ : startNum++;
                    string tag = colMap.TryGetValue("Tag/Note", out int colT) && colT < parts.Length
                                 ? parts[colT] : "";

                    points.Add(new HCPoint(new Point3d(x, y, z), num, "0", HC_BOD.BASIC, tag));
                }
                catch { /* skip malformed rows */ }
            }
            return points;
        }

        private static double ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0.0;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
