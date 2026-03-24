using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows;
using System.Globalization;
using System.Windows.Input;

using RailCAD.CadInterface;
using RailCAD.Models.Geometry;

namespace RailCAD.ViewModels
{
    /// <summary>
    /// ViewModel for the Import Points dialog
    /// </summary>
    internal class ImportPointsViewModel : INotifyPropertyChanged
    {
        private string _filePath;
        private string _filePreview;
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        private double _scaleZ = 1.0;
        private double _offsetX = 0.0;
        private double _offsetY = 0.0;
        private double _offsetZ = 0.0;
        private IList<string> _columnTypes;
        private ObservableCollection<string> _selectedColumnTypes;
        private List<string[]> _fileData;
        private bool _hasHeader = false;
        private char _delimiter = ' ';
        private ICadModel cad;
        private IList<RCPoint> _importedPoints;

        // Commands
        public ICommand BrowseFileCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }

        // Events for View communication
        public event EventHandler<bool?> CloseDialogRequested;
        public event EventHandler BrowseFileRequested;
        public event EventHandler<MessageEventArgs> ShowMessageRequested;
        public event EventHandler<PromptEventArgs> ShowPromptRequested;

        // Constructor
        internal ImportPointsViewModel(ICadModel cad)
        {
            this.cad = cad;
            _importedPoints = new List<RCPoint>();

            InitializeCommands();
            InitializeColumnTypes();
        }

        private void InitializeCommands()
        {
            BrowseFileCommand = new RelayCommand(ExecuteBrowseFile);
            ImportCommand = new RelayCommand(ExecuteImport);
            CancelCommand = new RelayCommand(ExecuteCancel);
            HelpCommand = new RelayCommand(ExecuteHelp);
        }

        private void InitializeColumnTypes()
        {
            // Initialize column types
            ColumnTypes = new List<string>
            {
                Properties.Resources.PointTools_ImportDialogPointNumber,
                Properties.Resources.PointTools_ImportDialogCoordinateX,
                Properties.Resources.PointTools_ImportDialogCoordinateY,
                Properties.Resources.PointTools_ImportDialogCoordinateZ,
                Properties.Resources.PointTools_ImportDialogTag,
                Properties.Resources.PointTools_ImportDialogIgnore
            };

            // Initialize selected column types
            SelectedColumnTypes = new ObservableCollection<string>
            {
                Properties.Resources.PointTools_ImportDialogIgnore,
                Properties.Resources.PointTools_ImportDialogIgnore,
                Properties.Resources.PointTools_ImportDialogIgnore,
                Properties.Resources.PointTools_ImportDialogIgnore,
                Properties.Resources.PointTools_ImportDialogIgnore
            };
        }

        #region Properties

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();

                    // Load file preview when path changes
                    if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                    {
                        LoadFilePreview();
                    }
                }
            }
        }

        public string FilePreview
        {
            get => _filePreview;
            set
            {
                if (_filePreview != value)
                {
                    _filePreview = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ScaleX
        {
            get => _scaleX;
            set
            {
                if (_scaleX != value)
                {
                    _scaleX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ScaleY
        {
            get => _scaleY;
            set
            {
                if (_scaleY != value)
                {
                    _scaleY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ScaleZ
        {
            get => _scaleZ;
            set
            {
                if (_scaleZ != value)
                {
                    _scaleZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public double OffsetX
        {
            get => _offsetX;
            set
            {
                if (_offsetX != value)
                {
                    _offsetX = value;
                    OnPropertyChanged();
                }
            }
        }

        public double OffsetY
        {
            get => _offsetY;
            set
            {
                if (_offsetY != value)
                {
                    _offsetY = value;
                    OnPropertyChanged();
                }
            }
        }

        public double OffsetZ
        {
            get => _offsetZ;
            set
            {
                if (_offsetZ != value)
                {
                    _offsetZ = value;
                    OnPropertyChanged();
                }
            }
        }

        public IList<string> ColumnTypes
        {
            get => _columnTypes;
            set
            {
                if (_columnTypes != value)
                {
                    _columnTypes = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> SelectedColumnTypes
        {
            get => _selectedColumnTypes;
            set
            {
                if (_selectedColumnTypes != value)
                {
                    _selectedColumnTypes = value;
                    OnPropertyChanged();
                }
            }
        }

        public IList<RCPoint> ImportedPoints => _importedPoints;

        #endregion

        #region Command Handlers

        private void ExecuteBrowseFile()
        {
            BrowseFileRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteImport()
        {
            try
            {
                // Validate the file path
                if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                {
                    ShowMessage(Properties.Resources.FileHandling_PleaseSelectValidFile,
                               Properties.Resources.Error,
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                    return;
                }

                // Get column mappings
                var columnMappings = GetColumnMappings();

                // Check if X and Y coordinates are mapped
                if (!columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogCoordinateX) ||
                    !columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogCoordinateY))
                {
                    ShowMessage(Properties.Resources.PointTools_ImportDialogDetermineColumnsForCoordinatesXY,
                               Properties.Resources.Error,
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                    return;
                }

                // Check if point number is missing
                int startNumber = 1;
                string defaultTag = "";

                if (!columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogPointNumber))
                {
                    // Prompt for starting point number
                    var promptArgs = new PromptEventArgs
                    {
                        Message = Properties.Resources.PointTools_ImportDialogEnterNumberOf1stPoint,
                        DefaultValue = "1"
                    };

                    ShowPromptRequested?.Invoke(this, promptArgs);

                    if (promptArgs.Result == true)
                    {
                        if (!int.TryParse(promptArgs.ResponseText, out startNumber) || startNumber <= 0)
                        {
                            ShowMessage(Properties.Resources.InputCheck_NumberMustBePositiveInteger,
                                       Properties.Resources.Error,
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        return; // User canceled
                    }
                }

                // Check if tag (note) is missing
                if (!columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogTag))
                {
                    // Prompt for default tag
                    var promptArgs = new PromptEventArgs
                    {
                        Message = Properties.Resources.PointTools_ImportDialogEnterDefaultPointTag,
                        DefaultValue = ""
                    };

                    ShowPromptRequested?.Invoke(this, promptArgs);

                    if (promptArgs.Result == true)
                    {
                        defaultTag = promptArgs.ResponseText;
                    }
                    // No validation needed for tag
                }

                // Read all data from file
                var data = ReadAllData();
                if (data.Count == 0)
                {
                    ShowMessage(Properties.Resources.FileHandling_FileContainsNoValidData,
                               Properties.Resources.Error,
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                    return;
                }

                // Import the points
                ImportPoints(data, columnMappings, startNumber, defaultTag);

                // Close dialog with success result
                CloseDialogRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                cad.WriteMessageNoDebug(Properties.Resources.PointTools_ErrorWhileImportingPoints);
                cad.WriteMessage(ex.Message);
            }
        }

        private void ExecuteCancel()
        {
            CloseDialogRequested?.Invoke(this, false);
        }

        private void ExecuteHelp()
        {
            ShowMessage(
                Properties.Resources.PointTools_ImportDialogHelpText,
                Properties.Resources.PointTools_ImportDialogHelpTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion

        #region Helper Methods

        private void ShowMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            var args = new MessageEventArgs
            {
                Message = message,
                Title = title,
                Button = button,
                Icon = icon
            };
            ShowMessageRequested?.Invoke(this, args);
        }

        // Import points to drawing
        private void ImportPoints(List<string[]> data, Dictionary<string, int> columnMappings, int startNumber, string defaultTag)
        {
            // Prepare progress reporter
            int total = data.Count;
            cad.WriteMessageNoDebug(Properties.Resources.PointTools_ImportDialogStartingPointsImport);

            // Prepare list of points
            List<RCPoint> points = new List<RCPoint>();
            int currentNumber = startNumber;

            // Process point numbers to handle large values
            List<string> pointNumberStrings = new List<string>();
            string commonPrefix = string.Empty;

            // Collect all point number strings if point numbers column exists
            if (columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogPointNumber))
            {
                int numIdx = columnMappings[Properties.Resources.PointTools_ImportDialogPointNumber];

                foreach (var row in data)
                {
                    if (numIdx < row.Length && !string.IsNullOrWhiteSpace(row[numIdx]))
                    {
                        pointNumberStrings.Add(row[numIdx].Trim());
                    }
                }

                // Find common prefix if numbers are too large
                if (pointNumberStrings.Count > 0)
                {
                    // Check if numbers might be too large for int32
                    bool hasLargeNumbers = pointNumberStrings.Any(s => s.Length > 9);

                    if (hasLargeNumbers)
                    {
                        // Find common prefix
                        commonPrefix = FindCommonPrefix(pointNumberStrings);
                        if (commonPrefix.Length > 0)
                        {
                            cad.WriteMessageNoDebug("\n" + Properties.Resources.PointTools_ImportDialogInfoCommonPrefixFound + commonPrefix);
                        }
                    }
                }
            }

            // Process each line of data
            for (int i = 0; i < data.Count; i++)
            {
                var row = data[i];

                // Get column indices
                int xIdx = columnMappings[Properties.Resources.PointTools_ImportDialogCoordinateX];
                int yIdx = columnMappings[Properties.Resources.PointTools_ImportDialogCoordinateY];
                int zIdx = columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogCoordinateZ) ? columnMappings[Properties.Resources.PointTools_ImportDialogCoordinateZ] : -1;
                int numIdx = columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogPointNumber) ? columnMappings[Properties.Resources.PointTools_ImportDialogPointNumber] : -1;
                int tagIdx = columnMappings.ContainsKey(Properties.Resources.PointTools_ImportDialogTag) ? columnMappings[Properties.Resources.PointTools_ImportDialogTag] : -1;

                // Skip if X or Y columns are out of range
                if (xIdx >= row.Length || yIdx >= row.Length)
                    continue;

                // Parse coordinates
                double x, y, z = 0;
                if (!ParseDouble(row[xIdx], out x) || !ParseDouble(row[yIdx], out y))
                    continue;

                // Parse Z coordinate if available
                if (zIdx >= 0 && zIdx < row.Length)
                {
                    if (!ParseDouble(row[zIdx], out z))
                        z = 0;
                }

                // Apply transformation
                x = x * ScaleX + OffsetX;
                y = y * ScaleY + OffsetY;
                z = z * ScaleZ + OffsetZ;

                // Get point number
                int number;
                if (numIdx >= 0 && numIdx < row.Length && !string.IsNullOrWhiteSpace(row[numIdx]))
                {
                    string pointNumberStr = row[numIdx].Trim();

                    // Remove common prefix if exists
                    if (!string.IsNullOrEmpty(commonPrefix) && pointNumberStr.StartsWith(commonPrefix))
                    {
                        pointNumberStr = pointNumberStr.Substring(commonPrefix.Length);
                    }

                    // Try parse the modified number
                    if (int.TryParse(pointNumberStr, out number))
                    {
                        // Use number from file
                    }
                    else
                    {
                        // Use auto-incremented number as fallback
                        number = currentNumber++;
                    }
                }
                else
                {
                    // Use auto-incremented number
                    number = currentNumber++;
                }

                // Get tag
                string tag;
                if (tagIdx >= 0 && tagIdx < row.Length)
                {
                    tag = row[tagIdx];
                }
                else
                {
                    tag = defaultTag;
                }

                // Create point
                RCPoint point = new RCPoint(new Point3d(x, y, z), number, "0", RC_BOD.ZAKLAD, tag);
                points.Add(point);
            }

            _importedPoints = points;

            // Final report
            cad.WriteMessageNoDebug(string.Format(Properties.Resources.PointTools_ImportDialogReadingOfPointsHasFinished, points.Count));
        }

        // Find common prefix in a list of strings
        private string FindCommonPrefix(List<string> strings)
        {
            if (strings == null || strings.Count == 0)
                return string.Empty;

            // Start with the first string
            string prefix = strings[0];

            // Maximum acceptable prefix length (leave at least 3 significant digits)
            int maxPrefixLength = Math.Max(0, strings.Min(s => s.Length) - 3);

            // Iteratively shorten the prefix until it matches all strings
            for (int i = 1; i < strings.Count; i++)
            {
                string current = strings[i];
                int j = 0;

                // Find common characters
                while (j < prefix.Length && j < current.Length && prefix[j] == current[j])
                {
                    j++;
                }

                // Shorten prefix to match
                prefix = prefix.Substring(0, j);

                // Stop if prefix becomes empty
                if (string.IsNullOrEmpty(prefix) || prefix.Length > maxPrefixLength)
                    break;
            }

            // Only trim if we can safely remove a significant part (at least 3 characters)
            return prefix.Length >= 3 ? prefix : string.Empty;
        }

        // Parse double value handling both dot and comma as decimal separator
        private bool ParseDouble(string value, out double result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return false;
            }

            // Firstly we replace comma with dot so the number parsing is working correctly
            string normalizedValue = value.Replace(',', '.');

            // Try parsing with invariant culture (dot as delimiter)
            if (double.TryParse(normalizedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return true;

            // If that does not work, try one more time with current culture
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
                return true;

            // Try to clean the value from unprintable characters
            string cleanedValue = new string(normalizedValue.Where(c => !char.IsControl(c)).ToArray());
            if (double.TryParse(cleanedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return true;

            result = 0;
            return false;
        }

        // Load preview of the file content
        private void LoadFilePreview()
        {
            try
            {
                // Reset data
                _fileData = new List<string[]>();

                // Read file content for preview (first few lines)
                const int previewLines = 20;
                var allLines = ReadTextFileWithEncoding(_filePath);
                var previewLinesCount = Math.Min(previewLines, allLines.Length);

                // Detect delimiter (tab or space)
                _delimiter = DetectDelimiter(allLines);

                // Parse data for preview
                for (int i = 0; i < previewLinesCount; i++)
                {
                    var line = allLines[i];
                    var values = SplitLine(line, _delimiter);

                    // if the last column is tag join the rest of the line together
                    int tagIndex = SelectedColumnTypes.ToList().FindIndex(t => t == Properties.Resources.PointTools_ImportDialogTag);
                    if (tagIndex >= 0 && tagIndex < values.Length - 1)
                    {
                        string note = string.Join(" ", values.Skip(tagIndex));
                        var fixedValues = values.Take(tagIndex).ToList();
                        fixedValues.Add(note);
                        values = fixedValues.ToArray();
                    }

                    _fileData.Add(values);
                }

                // Generate preview text
                StringBuilder sb = new StringBuilder();

                // Calculate column widths for nice formatting
                int[] colWidths = CalculateColumnWidths(_fileData);

                // Build header row if detected
                if (_hasHeader)
                {
                    sb.AppendLine(Properties.Resources.PointTools_ImportDialogHeaderDetected);
                    sb.AppendLine(FormatRow(_fileData[0], colWidths));

                    // Create separator line with improved visibility for columns
                    StringBuilder separatorLine = new StringBuilder();
                    int pos = 0;
                    for (int i = 0; i < _fileData[0].Length && i < colWidths.Length; i++)
                    {
                        // Add column separator for all except first column
                        if (i > 0)
                        {
                            separatorLine.Append("┼");
                            pos++;
                            separatorLine.Append("─");
                            pos++;
                        }

                        // Add dashes for column width
                        separatorLine.Append(new string('─', colWidths[i] + 2));
                        pos += colWidths[i] + 2;
                    }

                    sb.AppendLine(separatorLine.ToString());
                }

                // Build data rows
                int startRow = _hasHeader ? 1 : 0;
                for (int i = startRow; i < _fileData.Count; i++)
                {
                    sb.AppendLine(FormatRow(_fileData[i], colWidths));
                }

                // Set automatic column mappings based on detected content
                AutoDetectColumnTypes(_fileData, startRow);

                // Update preview
                FilePreview = sb.ToString();
            }
            catch (Exception ex)
            {
                FilePreview = Properties.Resources.FileHandling_ErrorWhileReadingFile;
                cad.WriteMessage(ex.Message);
                _fileData = new List<string[]>();
            }
        }

        // Detect delimiter used in the file (tab or space)
        private char DetectDelimiter(string[] lines)
        {
            // Use a few sample lines to detect the delimiter
            int sampleSize = Math.Min(5, lines.Length);
            int tabCount = 0;
            int spaceCount = 0;
            int commaCount = 0;
            int semicolonCount = 0;

            for (int i = 0; i < sampleSize; i++)
            {
                if (lines[i].Contains('\t'))
                {
                    tabCount++;
                }

                if (lines[i].Contains(',') && !Regex.IsMatch(lines[i], @"-?\d+,\d+"))
                {
                    commaCount++; // Comma as delimiter, not as decimal separator
                }

                if (lines[i].Contains(';'))
                {
                    semicolonCount++;
                }

                // Check for multiple spaces as delimiter
                if (Regex.IsMatch(lines[i], @"\s{2,}"))
                {
                    spaceCount++;
                }
            }

            // Determine the most likely delimiter
            if (tabCount > spaceCount && tabCount > commaCount && tabCount > semicolonCount)
                return '\t';
            if (commaCount > spaceCount && commaCount > tabCount && commaCount > semicolonCount)
                return ',';
            if (semicolonCount > spaceCount && semicolonCount > tabCount && semicolonCount > commaCount)
                return ';';

            return ' '; // Default to space
        }

        // Splits a line by delimiter handling multiple spaces
        private string[] SplitLine(string line, char delimiter)
        {
            if (string.IsNullOrWhiteSpace(line))
                return new string[0];

            if (delimiter == '\t')
            {
                return line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (delimiter == ',')
            {
                // Handle comma delimiter carefully to not split decimal numbers with commas
                // This is tricky because we need to distinguish between commas as delimiters
                // and commas as decimal separators

                // Simple approach: split by comma and then check if parts look like decimal numbers
                var parts = line.Split(',');
                IList<string> result = new List<string>();

                for (int i = 0; i < parts.Length; i++)
                {
                    string current = parts[i].Trim();

                    // Check if current part might be the integer part of a decimal number
                    if (i < parts.Length - 1 && Regex.IsMatch(current, @"-?\d+$") &&
                        Regex.IsMatch(parts[i + 1], @"^\d+"))
                    {
                        // This is likely a decimal number with comma separator
                        result.Add(current + "," + parts[i + 1]);
                        i++; // Skip the next part as we've used it
                    }
                    else
                    {
                        result.Add(current);
                    }
                }

                return result.ToArray();
            }
            else if (delimiter == ';')
            {
                return line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim()).ToArray();
            }
            else
            {
                // Handle multiple spaces as a single delimiter
                return Regex.Split(line.Trim(), @"\s+");
            }
        }

        // Calculate column widths for display formatting
        private int[] CalculateColumnWidths(List<string[]> data)
        {
            if (data.Count == 0 || data[0].Length == 0)
                return new int[0];

            int[] widths = new int[data[0].Length];

            foreach (var row in data)
            {
                for (int i = 0; i < row.Length && i < widths.Length; i++)
                {
                    widths[i] = Math.Max(widths[i], row[i].Length);
                }
            }

            return widths;
        }

        // Format a row for display with column separators
        private string FormatRow(string[] row, int[] colWidths)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < row.Length && i < colWidths.Length; i++)
            {
                // Add separator before columns (except the first one)
                if (i > 0)
                {
                    sb.Append("│ ");
                }

                sb.Append(row[i].PadRight(colWidths[i] + 2));
            }

            return sb.ToString();
        }

        // Auto-detect column types based on content
        private void AutoDetectColumnTypes(List<string[]> data, int startRow)
        {
            if (data.Count <= startRow || data[startRow].Length == 0)
                return;

            // Reset selected types
            for (int i = 0; i < SelectedColumnTypes.Count; i++)
            {
                SelectedColumnTypes[i] = Properties.Resources.PointTools_ImportDialogIgnore;
            }

            // Ensure we have enough slots for all columns
            while (SelectedColumnTypes.Count < data[startRow].Length)
            {
                SelectedColumnTypes.Add(Properties.Resources.PointTools_ImportDialogIgnore);
            }

            // Get sample row
            var sampleRow = data[startRow];
            List<int> numberCols = new List<int>();
            List<int> coordCols = new List<int>();
            List<int> textCols = new List<int>();

            // Identify column types
            for (int i = 0; i < sampleRow.Length; i++)
            {
                // Check if column contains numbers
                bool isNumber = true;
                bool hasDecimal = false;
                int validNumbers = 0;
                int totalValues = 0;
                bool hasLargeNumbers = false;

                for (int j = startRow; j < Math.Min(data.Count, startRow + 20); j++) // Check first 20 rows
                {
                    if (i < data[j].Length && !string.IsNullOrWhiteSpace(data[j][i]))
                    {
                        totalValues++;
                        string val = data[j][i].Replace(',', '.');

                        // Remove minus sign for number checks
                        string numericPart = val.TrimStart('-');

                        // Check if this could be a large point number
                        if (numericPart.Length > 9 && numericPart.All(c => char.IsDigit(c)))
                        {
                            hasLargeNumbers = true;
                        }

                        if (!double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                        {
                            isNumber = false;
                        }
                        else
                        {
                            validNumbers++;
                            if (val.Contains('.') || val.Contains(','))
                            {
                                hasDecimal = true;
                            }
                        }
                    }
                }

                // Column is numeric if most of the values are numbers
                isNumber = isNumber || (totalValues > 0 && validNumbers >= totalValues * 0.9);

                if (isNumber)
                {
                    if (hasLargeNumbers)
                    {
                        // Large numbers are likely point numbers
                        numberCols.Add(i);
                    }
                    else if (hasDecimal || i >= 1 && i <= 3) // Assume columns 1-3 are coordinates if they are numbers
                    {
                        coordCols.Add(i);
                    }
                    else
                    {
                        numberCols.Add(i);
                    }
                }
                else
                {
                    textCols.Add(i);
                }
            }

            // Special case for files with only coordinates (like XYZ files)
            if (data[startRow].Length == 3 && coordCols.Count == 3 && numberCols.Count == 0 && textCols.Count == 0)
            {
                SelectedColumnTypes[0] = Properties.Resources.PointTools_ImportDialogCoordinateX;
                SelectedColumnTypes[1] = Properties.Resources.PointTools_ImportDialogCoordinateY;
                SelectedColumnTypes[2] = Properties.Resources.PointTools_ImportDialogCoordinateZ;
                return;
            }

            // For files with 4 columns that are all numbers (like RTT medlánky)
            if (data[startRow].Length == 4 && coordCols.Count + numberCols.Count == 4 && textCols.Count == 0)
            {
                // First column is likely point number
                SelectedColumnTypes[0] = Properties.Resources.PointTools_ImportDialogPointNumber;
                // Next 3 columns are XYZ coordinates
                SelectedColumnTypes[1] = Properties.Resources.PointTools_ImportDialogCoordinateX;
                SelectedColumnTypes[2] = Properties.Resources.PointTools_ImportDialogCoordinateY;
                SelectedColumnTypes[3] = Properties.Resources.PointTools_ImportDialogCoordinateZ;
                return;
            }

            // For geo-survey files (Czech standard) where format is typically Number, X, Y, Z, Note
            if (data[startRow].Length >= 4 && coordCols.Count >= 3 &&
                (numberCols.Count > 0 || _hasHeader && startRow > 0 && data[0][0].Contains("bod")))
            {
                int pointNumIndex = numberCols.Count > 0 ? numberCols[0] : 0;
                SelectedColumnTypes[pointNumIndex] = Properties.Resources.PointTools_ImportDialogPointNumber;

                // YXZ format (common in Czech geo-survey)
                if (data[startRow].Length >= 4 && _hasHeader &&
                    (data[0][1].Contains("X") || data[0][1].Contains("x")))
                {
                    SelectedColumnTypes[1] = Properties.Resources.PointTools_ImportDialogCoordinateX;
                    SelectedColumnTypes[2] = Properties.Resources.PointTools_ImportDialogCoordinateY;
                    SelectedColumnTypes[3] = Properties.Resources.PointTools_ImportDialogCoordinateZ;

                    // If there's a 5th column, it's usually a note/code
                    if (data[startRow].Length >= 5)
                    {
                        SelectedColumnTypes[4] = Properties.Resources.PointTools_ImportDialogTag;
                    }
                    return;
                }
            }

            // Default detection method for other file formats

            // First, try to identify point number
            if (numberCols.Count > 0)
            {
                SelectedColumnTypes[numberCols[0]] = Properties.Resources.PointTools_ImportDialogPointNumber;
                numberCols.RemoveAt(0);
            }

            // Then, identify coordinates 
            if (coordCols.Count >= 2)
            {
                // First coordinate is X, second is Y, third is Z
                SelectedColumnTypes[coordCols[0]] = Properties.Resources.PointTools_ImportDialogCoordinateX;
                SelectedColumnTypes[coordCols[1]] = Properties.Resources.PointTools_ImportDialogCoordinateY;

                if (coordCols.Count >= 3)
                {
                    SelectedColumnTypes[coordCols[2]] = Properties.Resources.PointTools_ImportDialogCoordinateZ;
                }
            }

            // Finally, identify tag/note
            if (textCols.Count > 0)
            {
                SelectedColumnTypes[textCols[0]] = Properties.Resources.PointTools_ImportDialogTag;
            }

            // Analyze if point numbers are too large
            int pointNumIdx = -1;

            // Find column that was identified as point number
            for (int i = 0; i < SelectedColumnTypes.Count; i++)
            {
                if (SelectedColumnTypes[i] == Properties.Resources.PointTools_ImportDialogPointNumber)
                {
                    pointNumIdx = i;
                    break;
                }
            }

            if (pointNumIdx >= 0)
            {
                List<string> pointNumbers = new List<string>();

                // Collect sample point numbers
                for (int j = startRow; j < Math.Min(data.Count, startRow + 50); j++)
                {
                    if (pointNumIdx < data[j].Length && !string.IsNullOrWhiteSpace(data[j][pointNumIdx]))
                    {
                        pointNumbers.Add(data[j][pointNumIdx]);
                    }
                }

                // Check for common prefix
                if (pointNumbers.Count > 0 && pointNumbers.Any(pn => pn.Length > 9))
                {
                    string commonPrefix = FindCommonPrefix(pointNumbers);
                    if (!string.IsNullOrEmpty(commonPrefix) && commonPrefix.Length >= 3)
                    {
                        // Add information to the preview
                        _filePreview = Properties.Resources.Warning + Properties.Resources.PointTools_ImportDialogInfoCommonPrefixFound + $"({commonPrefix}).\n" +
                                       Properties.Resources.PointTools_ImportDialogInfoThisPrefixWillBeDeleted + "\n\n" +
                                      _filePreview;
                    }
                }
            }
        }

        // Get the columns indices mapped to each data type
        private Dictionary<string, int> GetColumnMappings()
        {
            var result = new Dictionary<string, int>();

            for (int i = 0; i < SelectedColumnTypes.Count; i++)
            {
                string columnType = SelectedColumnTypes[i];
                if (columnType != Properties.Resources.PointTools_ImportDialogIgnore && !result.ContainsKey(columnType))
                {
                    result[columnType] = i;
                }
            }

            return result;
        }

        // Read all data from the file
        private List<string[]> ReadAllData()
        {
            List<string[]> result = new List<string[]>();

            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return result;

            var allLines = ReadTextFileWithEncoding(_filePath);
            int startLine = _hasHeader ? 1 : 0;

            for (int i = startLine; i < allLines.Length; i++)
            {
                var line = allLines[i].Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    var values = SplitLine(line, _delimiter);

                    // if the last column is tag join rest of the line together
                    int tagIndex = SelectedColumnTypes.ToList().FindIndex(t => t == Properties.Resources.PointTools_ImportDialogTag);
                    if (tagIndex >= 0 && tagIndex < values.Length - 1)
                    {
                        string note = string.Join(" ", values.Skip(tagIndex));
                        var fixedValues = values.Take(tagIndex).ToList();
                        fixedValues.Add(note);
                        values = fixedValues.ToArray();
                    }

                    result.Add(values);
                }
            }

            return result;
        }

        // Read text file with appropriate encoding detection
        private string[] ReadTextFileWithEncoding(string filePath)
        {
            try
            {
                // Try to detect the encoding
                Encoding encoding = DetectTextFileEncoding(filePath);

                // If detection failed, try common encodings
                if (encoding == null)
                {
                    try
                    {
                        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                        return PreprocessLines(lines);
                    }
                    catch
                    {
                        try
                        {
                            var lines = File.ReadAllLines(filePath, Encoding.GetEncoding(1250)); // Central European (Windows)
                            return PreprocessLines(lines);
                        }
                        catch
                        {
                            try
                            {
                                var lines = File.ReadAllLines(filePath, Encoding.GetEncoding(852)); // Central European (DOS)
                                return PreprocessLines(lines);
                            }
                            catch
                            {
                                // Fall back to the default encoding
                                var lines = File.ReadAllLines(filePath);
                                return PreprocessLines(lines);
                            }
                        }
                    }
                }
                else
                {
                    var lines = File.ReadAllLines(filePath, encoding);
                    return PreprocessLines(lines);
                }
            }
            catch (Exception ex)
            {
                // Log error and return empty array
                cad.WriteMessage(ex.Message);
                return new string[] { Properties.Resources.FileHandling_ErrorWhileLoadingFile };
            }
        }

        // Preprocess lines to handle special cases
        private string[] PreprocessLines(string[] lines)
        {
            if (lines.Length == 0)
                return lines;

            List<string> processedLines = new List<string>();

            int headerCount = GetHeaderLineCount(lines);
            cad.WriteMessage("\nDetected header rows: " + headerCount);
            if (headerCount > 0)
            {
                _hasHeader = true;
                headerCount -= 1;
            }

            for (int i = headerCount; i < lines.Length; i++)
            {
                var line = lines[i];
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                processedLines.Add(line);
            }

            return processedLines.ToArray();
        }

        /// <summary>
        /// Returns a count of header lines which do not contain data but a text or they are empty.
        /// </summary>
        private static int GetHeaderLineCount(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0;

            int count = 0;
            foreach (var line in lines)
            {
                // if the line is empty it is part of a header
                if (string.IsNullOrWhiteSpace(line))
                {
                    count++;
                    continue;
                }

                // split by whitespace or tab
                var parts = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                // there must be at least 3 columns otherwise it is a header
                if (parts.Length < 3)
                {
                    count++;
                    continue;
                }

                // try to convert first 3 parts to numbers
                bool isNumericRow =
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                    double.TryParse(parts[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                    double.TryParse(parts[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

                if (isNumericRow)
                {
                    // first data line -> end of header
                    break;
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        // Detect encoding of text file
        private Encoding DetectTextFileEncoding(string filePath)
        {
            // Read the BOM
            var buffer = new byte[5];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                if (file.Length >= 5)
                {
                    file.Read(buffer, 0, 5);
                }
                else if (file.Length > 0)
                {
                    file.Read(buffer, 0, (int)file.Length);
                }
                else
                {
                    return null;
                }
            }

            // Analyze the BOM
            if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
                return Encoding.UTF8;
            if (buffer[0] == 0xff && buffer[1] == 0xfe)
                return Encoding.Unicode; // UTF-16 LE
            if (buffer[0] == 0xfe && buffer[1] == 0xff)
                return Encoding.BigEndianUnicode; // UTF-16 BE
            if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
                return Encoding.UTF32;

            // No BOM, try to detect by analyzing content
            using (var reader = new StreamReader(filePath, Encoding.Default, true))
            {
                string content = reader.ReadToEnd();

                // Check for Central European (Windows) specific characters
                if (content.Contains("ě") || content.Contains("č") || content.Contains("ř") || content.Contains("ý") || content.Contains("í") || content.Contains("ů") ||
                    content.Contains("š") || content.Contains("ž") || content.Contains("ý") || content.Contains("á") || content.Contains("é") || content.Contains("ú"))
                {
                    return Encoding.GetEncoding(1250);
                }

                // Return null to let the caller try different encodings
                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}