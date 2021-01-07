using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MultiFishTouchResponse
{
    public class ViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private BitmapSource currentCameraImage;
        public BitmapSource CurrentCameraImage
        {
            get
            {
                return currentCameraImage;
            }
            set
            {
                currentCameraImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentCameraImage"));
            }
        }

        private BitmapSource analysedImage;
        public BitmapSource AnalysedImage
        {
            get { return analysedImage; }
            set
            {
                analysedImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AnalysedImage"));
            }
        }

        private string recordingButtonText = "Recording";
        public string RecordingButtonText
        {
            get { return recordingButtonText; }
            set
            {
                recordingButtonText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RecordingButtonText"));
            }
        }

        private bool isAvailableForRecording = true;
        public bool IsAvailableForRecording
        {
            get { return isAvailableForRecording; }
            set
            {
                isAvailableForRecording = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsAvailableForRecording"));
            }
        }

        public List<Line> Lines { get; set; }
        private Line line;
        public Line Line
        {
            get { return line; }
            set
            {
                line = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Line"));
            }
        }

        public List<System.Drawing.Point> movePoints = new List<System.Drawing.Point>();

        public int StepMode { get; set; }
        public System.Windows.Controls.Border Border { get; set; }

        public NanotecController motor1 { get; set; }
        public NanotecController motor2 { get; set; }
        public NanotecController motor3 { get; set; }
        public NanotecController motor4 { get; set; }
        public NanotecController motor5 { get; set; }

        public System.Windows.Media.SolidColorBrush activeWellColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
        public System.Windows.Media.SolidColorBrush ActiveWellColor
        {
            get { return activeWellColor; }
            set
            {
                activeWellColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ActiveWellColor"));
            }
        }

        private int bwThreshold = 0;
        public int BwThreshold
        {
            get { return bwThreshold; }
            set
            {
                bwThreshold = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BwThreshold"));
            }
        }

        private int exposureTime = 0;
        public int ExposureTime
        {
            get { return exposureTime; }
            set
            {
                exposureTime = value;
                Ximea.ChangeSetting(xiApi.NET.PRM.EXPOSURE, exposureTime);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ExposureTime"));
            }
        }

        private bool cannyChecked = false;
        public bool CannyChecked
        {
            get { return cannyChecked; }
            set
            {
                cannyChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CannyChecked"));
            }
        }

        public int final_speed_factor = 25;
        public string Videoname { get; set; }
        public string WellRows { get; set; }
        public string WellCols { get; set; }

        public bool moveToFishFinished = false;
        public double imageratio = 0.0;
        public int fishSelectedPart = 0;
        public int WELLROWS = 0;
        public int WELLCOLS = 0;
    }
}
