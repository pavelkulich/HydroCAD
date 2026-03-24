using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HydroCAD.Models.Network;
using HydroCAD.Profile;

namespace HydroCAD.ViewModels
{
    internal class DrawProfileViewModel : INotifyPropertyChanged
    {
        private double _diameter = 300;
        private double _minCover = 0.8;
        private double _minGradient = 0.003;
        private double _startInvertLevel = double.NaN;
        private bool _useFixedGradient;
        private double _fixedGradient = 0.005;
        private double _samplingInterval = 5.0;
        private double _horizontalScale = 500;
        private double _verticalScale = 100;
        private PipeType _pipeType = PipeType.GravitySewer;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public event System.EventHandler<bool?> CloseDialogRequested;
        public event PropertyChangedEventHandler PropertyChanged;

        public DrawProfileViewModel()
        {
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        public double Diameter
        {
            get => _diameter;
            set { _diameter = value; OnPropertyChanged(); }
        }

        public double MinCoverDepth
        {
            get => _minCover;
            set { _minCover = value; OnPropertyChanged(); }
        }

        public double MinGradient
        {
            get => _minGradient;
            set { _minGradient = value; OnPropertyChanged(); }
        }

        public bool UseStartInvertLevel { get; set; }

        public double StartInvertLevel
        {
            get => _startInvertLevel;
            set { _startInvertLevel = value; OnPropertyChanged(); }
        }

        public bool UseFixedGradient
        {
            get => _useFixedGradient;
            set { _useFixedGradient = value; OnPropertyChanged(); }
        }

        public double FixedGradient
        {
            get => _fixedGradient;
            set { _fixedGradient = value; OnPropertyChanged(); }
        }

        public double SamplingInterval
        {
            get => _samplingInterval;
            set { _samplingInterval = value; OnPropertyChanged(); }
        }

        public double HorizontalScale
        {
            get => _horizontalScale;
            set { _horizontalScale = value; OnPropertyChanged(); }
        }

        public double VerticalScale
        {
            get => _verticalScale;
            set { _verticalScale = value; OnPropertyChanged(); }
        }

        public PipeType PipeType
        {
            get => _pipeType;
            set { _pipeType = value; OnPropertyChanged(); }
        }

        public ProfileSettings GetSettings() => new ProfileSettings
        {
            PipeDiameter      = Diameter,
            MinCoverDepth     = MinCoverDepth,
            MinGradient       = MinGradient,
            StartInvertLevel  = UseStartInvertLevel ? StartInvertLevel : double.NaN,
            Gradient          = UseFixedGradient ? FixedGradient : double.NaN,
            SamplingInterval  = SamplingInterval,
            HorizontalScale   = HorizontalScale,
            VerticalScale     = VerticalScale,
            PipeType          = PipeType,
        };

        private void ExecuteOk() => CloseDialogRequested?.Invoke(this, true);
        private void ExecuteCancel() => CloseDialogRequested?.Invoke(this, false);

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
