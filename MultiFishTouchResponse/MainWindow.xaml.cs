using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenCvSharp;

using xiApi.NET;

namespace MultiFishTouchResponse
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        List<NanotecController> Motors = new List<MultiFishTouchResponse.NanotecController>(5);
        ViewModel viewModel = new ViewModel();
        public bool LockCamera = false;
        bool InitialClicked = false;
        List<int[]> Wellpositions;
        DebugView DebugWindow;
        NanotecSet setUp = new NanotecSet();
        NanotecSet setDown = new NanotecSet();
        NanotecSet setReset = new NanotecSet();
        NanotecSet setKeyUp = new NanotecSet();
        NanotecSet setKeyDown = new NanotecSet();
        //ImageAnalysis imageAnalysis;
        ImageProcessing imageProcessor;
        public List<Line> Lines = new List<Line>();
        public int StepMode = 16;
        SettingUp Initialization = new SettingUp();
        Thickness imageThickness = new Thickness();
        public int wellNumberRow = 0; //number of wells in each row
        public int wellNumberCol = 0; //number of wells in each col
        public double wellDiameter = 0;
        public double wellDistance = 0; //the distance between wells

        public int WELLROWS = 0; //which row is selected
        public int WELLCOLS = 0; //which col is selected

        public double linefactor = 0;
        public float lineFactor = 0;
        public int[] firstPosition = { 0 };
        public List<CheckBox> checkbowellcol = new List<CheckBox>();
        public List<CheckBox> checkbowellrow = new List<CheckBox>();

        public bool all_stop = false;

        List<int[]> wellNumbers = new List<int[]>() {
            new int[] { 2, 3 },
            new int[] { 3, 4 },
            new int[] { 4, 6 },
            new int[] { 6, 8 },
            new int[] { 8, 12 }
        };
        List<double> wellDistances = new List<double>() {
            39.2,
            26.0,
            19.3,
            13.0,
            9.0
        };
        List<int[]> firstPositions = new List<int[]>() {
            new int[] { 89, 4994 }, // { 53, 5045 },
            new int[] { 65, 4715 },//{ 29, 4766 },
            new int[] { 1890, 4249 },
            new int[] { 1890, 4249 },
            new int[] { 1890, 4249 }
        };
        List<double> wellDiameters = new List<double>() {
            35.77,
            22.90,
            16.20,
            10.70,
            6.80
        };
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Ximea.StopCamera = true;
            Application.Current.Shutdown();
        }

        public MainWindow()
        {
            
            Initialization.show();


            wellNumberRow = wellNumbers[wellInformation.wellTpyeIndex][0]; //wellInformation.wellNumberRow; // 2;// 
            wellNumberCol = wellNumbers[wellInformation.wellTpyeIndex][1]; //wellInformation.wellNumberCol; // 3;// 
            wellDiameter = wellDiameters[wellInformation.wellTpyeIndex];  //wellInformation.wellDiameter; // 35.77;//
            wellDistance = wellDistances[wellInformation.wellTpyeIndex]; //wellInformation.wellDistance;
            firstPosition = firstPositions[wellInformation.wellTpyeIndex];
            double imageRatio = wellDiameter / 35.77;

            InitializeComponent();

            checkBox2List();
            /*
            public List<CheckBox> checkbowellcol = new List<CheckBox>()
            {
                checkBox_WellCol1,
                checkBox_WellCol2,
                checkBox_WellCol3,
                checkBox_WellCol4,
                checkBox_WellCol5,
                checkBox_WellCol6,
                checkBox_WellCol7,
                checkBox_WellCol8,
                checkBox_WellCol9,
                checkBox_WellCol10,
                checkBox_WellCol11,
                checkBox_WellCol12
            };
            */
            for (int r = wellNumberRow; r < 8; r++)
            {
                checkbowellrow[r].IsEnabled = false;
            }
            for (int c = wellNumberCol; c < 12; c++)
            {
                checkbowellcol[c].IsEnabled = false;
            }


            for (int i = 0; i < 5; i++)
            {
                //x,y,z, camera x, camera y
                Motors.Add(new NanotecController(i + 1));
                Motors[i].motor.SetStepMode(StepMode);
            }

            viewModel.motor1 = Motors[0];
            viewModel.motor2 = Motors[1];
            viewModel.motor3 = Motors[2];
            viewModel.motor4 = Motors[3];
            viewModel.motor5 = Motors[4];

            PrepareSets();
            SetAllMotors(setReset, 1, Motors);
            SetAllMotors(setUp, 2, Motors);
            SetAllMotors(setDown, 3, Motors);
            SetAllMotors(setKeyUp, 4, Motors);
            SetAllMotors(setKeyDown, 5, Motors);

            DataContext = viewModel;
            Ximea.viewModel = viewModel;
            DebugWindow = new DebugView(viewModel);
            imageProcessor = new ImageProcessing(viewModel, DebugWindow);
            viewModel.Lines = Lines;
            viewModel.Border = Border;
            viewModel.StepMode = StepMode;
            viewModel.Videoname = "WT";
            viewModel.imageratio = imageRatio;
            viewModel.WellRows = "00";
            viewModel.WellCols = "00";
            viewModel.fishSelectedPart = wellInformation.fishPartIndex;
            viewModel.debug = true;

            Wellpositions = TxtFileReader("WellPositions.txt");

            linefactor = 0.20375;//0.294 / 200 * viewModel.StepMode;

            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = true;

            if (dlg.ShowDialog().Value == true)
            {
                Ximea.Path = dlg.SelectedPath;
            }
            Ximea.StartCamera();

            PerformReset();

            DebugWindow.Show();
            BeginImageAnalysis();
        }

        private void BeginImageAnalysis()
        {
            
            var b1 = new Binding("AnalysedImage");
            b1.Delay = 30;
            BindingOperations.SetBinding(image, Image.SourceProperty, b1);
            ImageRotate.Angle = 0;
            ImageFlip.ScaleY = 1;
            imageProcessor.run();
        }

        private void SetAllMotors(NanotecSet Set, int SetNo, List<NanotecController> Motors)
        {

            for (int i = 0; i < Motors.Count; i++)
            {
                int speed = Set.FinalSpeed;
                if (i == 2)
                {
                    Set.FinalSpeed = 12 * speed;
                }
                Motors[i].SaveSet(Set, SetNo);
                Set.FinalSpeed = speed;
            }
        }

        private void checkBox2List()
        {
            checkbowellcol.Add(checkBox_WellCol1);
            checkbowellcol.Add(checkBox_WellCol2);
            checkbowellcol.Add(checkBox_WellCol3);
            checkbowellcol.Add(checkBox_WellCol4);
            checkbowellcol.Add(checkBox_WellCol5);
            checkbowellcol.Add(checkBox_WellCol6);
            checkbowellcol.Add(checkBox_WellCol7);
            checkbowellcol.Add(checkBox_WellCol8);
            checkbowellcol.Add(checkBox_WellCol9);
            checkbowellcol.Add(checkBox_WellCol10);
            checkbowellcol.Add(checkBox_WellCol11);
            checkbowellcol.Add(checkBox_WellCol12);

            checkbowellrow.Add(checkBox_WellRowA);
            checkbowellrow.Add(checkBox_WellRowB);
            checkbowellrow.Add(checkBox_WellRowC);
            checkbowellrow.Add(checkBox_WellRowD);
            checkbowellrow.Add(checkBox_WellRowE);
            checkbowellrow.Add(checkBox_WellRowF);
            checkbowellrow.Add(checkBox_WellRowG);
            checkbowellrow.Add(checkBox_WellRowH);
        }
        public List<int[]> TxtFileReader(string filename)
        {
            string[] textlines = System.IO.File.ReadAllLines("WellPositions.txt");
            List<int[]> WellPositions = new List<int[]>();
            foreach (string line in textlines)
            {
                int x = Convert.ToInt16(line.Split(',')[0]);
                int y = Convert.ToInt16(line.Split(',')[1]);
                int[] p = new int[] { x, y };
                WellPositions.Add(p);
            }
            return WellPositions;
        }

        private void PrepareSets()
        {
            //setUp
            setUp.Steps = 100 * StepMode;
            setUp.Direction = 0;
            setUp.StartSpeed = 10 * StepMode;
            setUp.FinalSpeed = 25 * StepMode;
            setUp.PositionType = 1;
            setUp.AccRamp = 1 * StepMode;
            setUp.BrakeRamp = 0;
            setUp.Pause = 0;
            setUp.Repetitions = 1;
            setUp.NextSet = 0;
            setUp.RampType = 0;

            //setDown
            setDown.Steps = 100 * StepMode;
            setDown.Direction = 1;
            setDown.StartSpeed = 10 * StepMode;
            setDown.FinalSpeed = 25 * StepMode;
            setDown.PositionType = 1;
            setDown.AccRamp = 1 * StepMode;
            setDown.BrakeRamp = 0;
            setDown.Pause = 0;
            setDown.Repetitions = 1;
            setDown.NextSet = 0;
            setDown.RampType = 0;

            //setKeyUp
            setKeyUp.Steps = 5000 * StepMode;
            setKeyUp.Direction = 0;
            setKeyUp.StartSpeed = 10 * StepMode;
            setKeyUp.FinalSpeed = 50 * StepMode;
            setKeyUp.PositionType = 1;
            setKeyUp.AccRamp = 1 * StepMode;
            setKeyUp.BrakeRamp = 0;
            setKeyUp.Pause = 0;
            setKeyUp.Repetitions = 1;
            setKeyUp.NextSet = 0;
            setKeyUp.RampType = 0;

            //setKeyDown
            setKeyDown.Steps = 5000 * StepMode;
            setKeyDown.Direction = 1;
            setKeyDown.StartSpeed = 10 * StepMode;
            setKeyDown.FinalSpeed = 50 * StepMode;
            setKeyDown.PositionType = 1;
            setKeyDown.AccRamp = 1 * StepMode;
            setKeyDown.BrakeRamp = 0;
            setKeyDown.Pause = 0;
            setKeyDown.Repetitions = 1;
            setKeyDown.NextSet = 0;
            setKeyDown.RampType = 0;

            //setReset
            setReset.Direction = 0;
            setReset.StartSpeed = 50 * StepMode;
            setReset.FinalSpeed = 100 * StepMode;
            setReset.PositionType = 4;
            setReset.AccRamp = 1 * StepMode;
            setReset.BrakeRamp = 0;
            setReset.Pause = 0;
            setReset.Repetitions = 1;
            setReset.NextSet = 0;
            setReset.RampType = 0;

        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            checkBox_UseAnalysedImage.IsChecked = false;
            if (viewModel.Lines.Count == 0)
            {
                Recording();
            }
            else
                DebugWindow.MoveLine();
        }

        private void Recording()
        {
            if (Ximea.Recording == true)
            {
                //button_Recording.Content  = "Start Recording";
                Ximea.Recording = false;
            }
            else
            {
                //button_Recording.Content = "Stop Recording";
                Ximea.Recording = true;
            }
        }

        private void MotorStop()
        {
            viewModel.CannyChecked = false;
            Motors[0].Stop();
            Motors[1].Stop();
            Motors[2].Stop();
            Motors[3].Stop();
            Motors[4].Stop();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MotorStop();
        }

        private void button_up_Click(object sender, RoutedEventArgs e)
        {
            Motors[4].Move(2);
            Motors[1].Move(2);
        }

        private void button_left_Click(object sender, RoutedEventArgs e)
        {
            Motors[0].Move(2);
            Motors[3].Move(2);
        }

        private void button_right_Click(object sender, RoutedEventArgs e)
        {
            Motors[0].Move(3);
            Motors[3].Move(3);
        }

        private void button_down_Click(object sender, RoutedEventArgs e)
        {
            Motors[4].Move(3);
            Motors[1].Move(3);
        }

        private void button_zup_Click(object sender, RoutedEventArgs e)
        {
            Motors[2].Move(2);
        }

        private void button_zdown_Click(object sender, RoutedEventArgs e)
        {
            PutNeedleDown();
        }

        private void PutNeedleDown()
        {
            int setcounter = 8;
            NanotecSet NeedleSetDown = new NanotecSet();
            NanotecSet NeedleSetUp = new NanotecSet();
            NeedleSetDown = setReset;
            NeedleSetDown.FinalSpeed = 4 * 100 * StepMode;
            NeedleSetDown.Direction = 1;
            NeedleSetDown.NextSet = setcounter + 1;
            NeedleSetUp = setUp;
            NeedleSetUp.Steps = 150 * StepMode;
            Motors[2].SaveSet(NeedleSetDown, setcounter);
            Motors[2].SaveSet(NeedleSetUp, setcounter + 1);

            Motors[2].Move(setcounter);
        }

        private void PutNeedleUp()
        {
            Motors[2].Move(1000, 0);
        }

        private void PerformReset()
        {
            all_stop = true;
            viewModel.CannyChecked = false;
            Motors[2].Move(1);
            Task.Run(() =>
            {
                while (viewModel.motor3.motor.IsMotorReady() != true)
                {
                    if (viewModel.motor3.motor.HasPositionError() == true)
                        viewModel.motor3.motor.ResetPositionError(false, 0);
                    Task.Delay(100).Wait();
                }
                Motors[0].Move(1);
                Motors[1].Move(1);
                Motors[3].Move(1);
                Motors[4].Move(1);
            });
        }

        private void button_reset_Click(object sender, RoutedEventArgs e)
        {
            PerformReset();
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (textBox_Videoname.IsKeyboardFocused == true)
            {
                //normal
            }
            else
            {
                e.Handled = true;
                if (e.IsRepeat == true)
                    return;
                if (e.Key == Key.Left)
                {
                    Motors[0].Move(2);
                    if (LockCamera == false)
                        Motors[3].Move(2);
                }
                if (e.Key == Key.Right)
                {
                    Motors[0].Move(3);
                    if (LockCamera == false)
                        Motors[3].Move(3);
                }
                if (e.Key == Key.Up)
                {
                    if (LockCamera == false)
                        Motors[4].Move(2);
                    Motors[1].Move(2);
                }
                if (e.Key == Key.Down)
                {
                    if (LockCamera == false)
                        Motors[4].Move(3);
                    Motors[1].Move(3);
                }
                if (e.Key == Key.W)
                {
                    Motors[2].Move(4);
                }
                if (e.Key == Key.S)
                {
                    Motors[2].Move(5);
                }
                if (e.Key == Key.R)
                {
                    Recording();
                }
                switch (e.Key)
                {
                    case Key.D1:

                        break;
                    case Key.D2:

                        break;
                    case Key.D3:

                        break;
                    case Key.D4:

                        break;
                }
            }
        }

        private void OnKeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right)
            { Motors[0].Stop(); Motors[3].Stop(); }
            if (e.Key == Key.Up || e.Key == Key.Down)
            { Motors[1].Stop(); Motors[4].Stop(); }
            if (e.Key == Key.W || e.Key == Key.S)
            { Motors[2].Stop(); }
        }

        private void checkBox_CameraLock_Checked(object sender, RoutedEventArgs e)
        {
            LockCamera = true;
        }

        private void checkBox_CameraLock_Unchecked(object sender, RoutedEventArgs e)
        {
            LockCamera = false;
            viewModel.ActiveWellColor = new SolidColorBrush(Colors.White);
        }

        private void checkBox_WellCol1_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0001;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol1_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111e;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol2_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0002;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol2_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111d;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol3_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0004;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol3_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111b;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol4_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0008;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol4_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1117;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol5_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0010;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol5_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11e1;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol6_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0020;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol6_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11d1;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol7_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0040;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol7_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11b1;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol8_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0080;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol8_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1171;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol9_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0100;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol9_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1e11;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol10_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0200;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol10_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1d11;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol11_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0400;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol11_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1b11;
            WELLROWS = WELLROWS & FLAG;
        }
        private void checkBox_WellCol12_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0800;
            WELLROWS = WELLROWS | FLAG;
        }
        private void checkBox_WellCol12_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1711;
            WELLROWS = WELLROWS & FLAG;
        }

        private void checkBox_WellRowA_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0001;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowA_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111e;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowB_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0002;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowB_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111d;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowC_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0004;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowC_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x111b;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowD_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0008;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowD_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1117;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowE_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0010;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowE_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11e1;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowF_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0020;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowF_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11d1;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowG_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0040;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowG_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x11b1;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void checkBox_WellRowH_Checked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x0080;
            WELLCOLS = WELLCOLS | FLAG;
        }
        private void checkBox_WellRowH_Unchecked(object sender, RoutedEventArgs e)
        {
            int FLAG = 0x1171;
            WELLCOLS = WELLCOLS & FLAG;
        }
        private void image_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                int count = Lines.Count - 1;
                if (InitialClicked == false)
                {
                    Lines.Add(new Line());
                    count = Lines.Count - 1;
                    Lines[count].Visibility = Visibility.Visible;
                    if (checkBox_UseAnalysedImage.IsChecked == true)
                        Lines[count].Stroke = Brushes.White;
                    else
                        Lines[count].Stroke = Brushes.Black;
                    Lines[count].StrokeThickness = 1;
                    ImageGrid.Children.Add(Lines[count]);
                    if (count > 0)
                    {
                        Lines[count].X1 = Lines[count - 1].X2;
                        Lines[count].Y1 = Lines[count - 1].Y2;
                    }
                    else
                    {
                        Lines[count].X1 = Mouse.GetPosition(ImageGrid).X;
                        Lines[count].Y1 = Mouse.GetPosition(ImageGrid).Y;
                    }
                    viewModel.Line = Lines[count];
                    InitialClicked = true;
                }
                Lines[count].X2 = Mouse.GetPosition(ImageGrid).X;
                Lines[count].Y2 = Mouse.GetPosition(ImageGrid).Y;
            }
            else
                InitialClicked = false;
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Lines.Clear();
                viewModel.Line = null;
                ImageGrid.Children.OfType<Line>().ToList().ForEach(line => ImageGrid.Children.Remove(line));
            }
        }

        /*
        private async void button_begin_Click(object sender, MouseButtonEventArgs e)
        {
            checkBox_CameraLock.SetValue(CheckBox.IsCheckedProperty, true);
            var point = Mouse.GetPosition(WellplateGrid);

            int row = wellRow;
            int col = wellCol;

            // row and col now correspond Grid's RowDefinition and ColumnDefinition mouse was 
            // over when double clicked!

            WellplateActiveWell.SetValue(Grid.RowProperty, row);
            WellplateActiveWell.SetValue(Grid.ColumnProperty, col);
            viewModel.ActiveWellColor = new SolidColorBrush(Colors.Yellow);
            int motorx = Wellpositions[row * 4 + col][0];
            int motory = Wellpositions[row * 4 + col][1];

            if (Motors[2].Motorposition != 0)
            {
                Motors[2].Move(1);
                await Task.Run(() =>
                {
                    while (!Motors[2].motor.IsMotorReady())
                    {
                        Task.Delay(100).Wait();
                        if (Motors[2].motor.HasPositionError() == true)
                            Motors[2].motor.ResetPositionError(false, 0);
                    }
                });
            }
            Motors[0].motor.SetMaxFrequency(100 * StepMode);
            Motors[1].motor.SetMaxFrequency(100 * StepMode);
            int camera_needle_offset_x = 4920 - 6089;
            int camera_needle_offset_y = 8330 - 7083;
            Motors[0].Move(motorx - camera_needle_offset_x, 1);
            Motors[1].Move(motory - camera_needle_offset_y, 1);
            Motors[3].Move(motorx, 1);
            Motors[4].Move(motory, 1);

            await Task.Run(() =>
            {
                int motor = 0;
                if (Math.Abs(motory - Motors[1].GetPosition()) > Math.Abs(motorx - Motors[0].GetPosition()))
                    motor = 1;
                while (!Motors[motor].motor.IsMotorReady())
                {
                    Task.Delay(100).Wait();
                }
            });
            viewModel.ActiveWellColor = new SolidColorBrush(Colors.Green);
            PutNeedleDown();

        }
        */

        private List<int> FindWellPosition(int a, int bit)
        {
            List<int> bitPositions = new List<int>();
            for (int i = 0; i < bit; i++)
            {
                int a_bat = a >> i;
                int comparer = 1;
                if ((a_bat & comparer) == 1)
                    bitPositions.Add(i + 1);
            }
            return bitPositions;
        }
        private async void WellplateGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            all_stop = false;
            viewModel.CannyChecked = false;
            checkBox_CameraLock.SetValue(CheckBox.IsCheckedProperty, true);
            var point = Mouse.GetPosition(WellplateGrid);
            List<int> pisitionCols = FindWellPosition(WELLCOLS, 8);
            List<int> pisitionRows = FindWellPosition(WELLROWS, 12);
            if ((pisitionRows.Count == 0) | (pisitionCols.Count == 0))
            {
                MessageBox.Show("Too less wells");
            }
            else
            {
                for (int r = 0; r < pisitionRows.Count; r++)
                {
                    for (int c = 0; c < pisitionCols.Count; c++)
                    {
                        int row = pisitionRows[r] - 1; // Convert.ToInt32(viewModel.WellRows);
                        int col = pisitionCols[c] - 1; // Convert.ToInt32(viewModel.WellCols);
                        if ((row > wellNumberRow) | (col > wellNumberCol))
                        {
                            MessageBox.Show("Invalid wells");
                        }
                        /*
                        double accumulatedHeight = 0.0;
                        double accumulatedWidth = 0.0;

                        // calc row mouse was over
                        foreach (var rowDefinition in WellplateGrid.RowDefinitions)
                        {
                            accumulatedHeight += rowDefinition.ActualHeight;
                            if (accumulatedHeight >= point.Y)
                                break;
                            row++;
                        }

                        // calc col mouse was over
                        foreach (var columnDefinition in WellplateGrid.ColumnDefinitions)
                        {
                            accumulatedWidth += columnDefinition.ActualWidth;
                            if (accumulatedWidth >= point.X)
                                break;
                            col++;
                        }
                        */
                        // row and col now correspond Grid's RowDefinition and ColumnDefinition mouse was 
                        // over when double clicked!
                        else
                        {
                            if (viewModel.debug)
                            {
                                string this_time = System.DateTime.Now.ToString("HHmmss");
                                Console.WriteLine("move to next well begin" + this_time);
                            }
                            WellplateActiveWell.SetValue(Grid.RowProperty, row);
                            WellplateActiveWell.SetValue(Grid.ColumnProperty, col);
                            viewModel.ActiveWellColor = new SolidColorBrush(Colors.Yellow);
                            int offsetx = firstPosition[0];
                            int offsety = firstPosition[1];
                            //int motorx = Wellpositions[row * wellNumberCol + col][0];
                            //int motory = Wellpositions[row * wellNumberCol + col][1];
                            int motorx = offsetx + (int)(row * wellDistance * 10.8 / linefactor);
                            int motory = offsety + (int)(col * wellDistance * 10.8 / linefactor);

                            if (Motors[2].Motorposition != 0)
                            {
                                Motors[2].Move(1);
                                await Task.Run(() =>
                                {
                                    while (!Motors[2].motor.IsMotorReady())
                                    {
                                        Task.Delay(100).Wait();
                                        if (Motors[2].motor.HasPositionError() == true)
                                            Motors[2].motor.ResetPositionError(false, 0);
                                    }
                                });
                            }
                            Motors[0].motor.SetMaxFrequency(100 * StepMode);
                            Motors[1].motor.SetMaxFrequency(100 * StepMode);
                            int camera_needle_offset_x = 4222 + 36 - 6154;
                            int camera_needle_offset_y = 7088 - 51 - 6984 + 500;
                            Motors[0].Move(motorx - camera_needle_offset_x, 1);
                            Motors[1].Move(motory - camera_needle_offset_y, 1);
                            Motors[3].Move(motorx, 1);
                            Motors[4].Move(motory, 1);

                            await Task.Run(() =>
                            {
                                int motor = 0;
                                if (Math.Abs(motory - Motors[1].GetPosition()) > Math.Abs(motorx - Motors[0].GetPosition()))
                                    motor = 1;
                                while (!Motors[motor].motor.IsMotorReady())
                                {
                                    Task.Delay(100).Wait();
                                }
                            });

                            PutNeedleDown();
                            await Task.Run(() =>
                            {
                                while (!Motors[2].motor.IsMotorReady())
                                {
                                    Task.Delay(100).Wait();
                                }
                            });

                            viewModel.moveToFishFinished = false;
                            Ximea.Recording = false;
                            Ximea.Recordingsaving = true;
                            viewModel.CannyChecked = false;


                            viewModel.ActiveWellColor = new SolidColorBrush(Colors.Green);
                            viewModel.movePoints.Clear();
                            viewModel.Lines.Clear();
                            Task.Delay(2000).Wait();
                            if (viewModel.debug)
                            {
                                string this_time = System.DateTime.Now.ToString("HHmmss");
                                Console.WriteLine("move to next well end" + this_time);
                            }
                            /*
                            await Task.Run(() =>
                            {
                                while (imageAnalysis.bothDetected == false)
                                {
                                    Task.Delay(100).Wait();
                                }
                            });
                            */


                            //imageAnalysis = new ImageAnalysis(Ximea.CameraImageQueue, viewModel, DebugWindow);
                            imageProcessor.DetectionINIT = true;
                            imageProcessor.detection_failed = false;
                            //viewModel.CannyChecked = true;
                            //imageAnalysis.image_not_saving = true;
                            //imageAnalysis.Begin();
                            //imageProcessor.run();
                            //var b1 = new Binding("AnalysedImage");
                            //b1.Delay = 30;
                            //BindingOperations.SetBinding(image, Image.SourceProperty, b1);
                            //ImageRotate.Angle = 0;
                            //ImageFlip.ScaleY = 1;
                            //imageAnalysis.needleAdded = false;

                            await Task.Run(() =>
                            {
                                // stop when already waited for 20 seconds, or when the detection dailed
                                int waiting_cnt = 0;
                                while ((waiting_cnt < 200) && (imageProcessor.DetectionINIT))
                                {
                                    Task.Delay(100).Wait();
                                    waiting_cnt++;
                                }
                            });


                            //
                            if ((imageProcessor.detected_larva_num > 0) && (!imageProcessor.detection_failed))
                            {
                                while (imageProcessor.touched_larva_cnt < imageProcessor.detected_larva_num)
                                {
                                    imageProcessor.DetectionINIT = false;
                                    imageProcessor.got_next_fished = false;
                                    
                                    await Task.Run(() =>
                                    {
                                        imageProcessor.goToNestLarva();
                                        while (!imageProcessor.got_next_fished)
                                        {
                                            Task.Delay(100).Wait();
                                        }
                                    });
                                    
                                    if(viewModel.Lines.Count() >= 2){
                                        while (!Ximea.Recordingsaving)
                                        {
                                            Task.Delay(100).Wait();
                                        }
                                        Ximea.Recordingsaving = false;
                                        moveToFish();
                                        await Task.Run(() =>
                                        {
                                            while ((viewModel.moveToFishFinished == false) && (all_stop != true))
                                            {
                                                Task.Delay(100).Wait();
                                            }
                                        });
                                    }
                                    viewModel.moveToFishFinished = false;
                                }
                            }
                            else
                            {
                                Ximea.Recording = true;
                                Task.Delay(2000).Wait();
                            }


                            //imageAnalysis.image_not_saving = false;
                            //imageAnalysis.Dispose();
                            imageProcessor.detection_failed = false;
                            imageProcessor.old_larva_blobs = null;
                            imageProcessor.detected_larva_num = 0;
                            imageProcessor.touched_larva_cnt = 0;
                            Ximea.Recordingsaving = true;
                            Ximea.Recording = false;
                            //viewModel.CannyChecked = false;
                            imageProcessor.DetectionINIT = false;
                            viewModel.Lines.Clear();
                            //if((r == pisitionRows.Count-1)&&(c == pisitionCols.Count-1))
                            //{
                            //r = 0;
                            //c = -1;
                            //}
                            if (all_stop == true)
                            {
                                r = pisitionRows.Count;
                                c = pisitionCols.Count;
                            }
                        }
                    }
                }
            }
        }

        private void moveToFish()
        {
            DebugWindow.MoveLine2();
        }

        private void checkBox_UseAnalysedImage_Checked(object sender, RoutedEventArgs e)
        {
            //List<WriteableBitmap> RecordingQueueBuffer = new List<WriteableBitmap>(Ximea.CameraImageQueue);
            //imageAnalysis = new ImageAnalysis(Ximea.CameraImageQueue, viewModel, DebugWindow);
            //var b1 = new Binding("AnalysedImage");
            //b1.Delay = 30;
            //BindingOperations.SetBinding(image, Image.SourceProperty, b1);
            //ImageRotate.Angle = 0;
            //ImageFlip.ScaleY = 1;
            imageProcessor.Begin();
            //imageProcessor.run();
        }

        private void checkBox_UseAnalysedImage_Unchecked(object sender, RoutedEventArgs e)
        {
            /*
            imageAnalysis.Dispose();
            var b1 = new Binding("CurrentCameraImage");
            b1.Delay = 30;
            BindingOperations.SetBinding(image, Image.SourceProperty, b1);
            ImageRotate.Angle = 90;
            ImageFlip.ScaleY = -1;
            */
            imageProcessor.Dispose();
        }

        private void textBox_Videoname_TextChanged(object sender, TextChangedEventArgs e)
        {
            viewModel.Videoname = textBox_Videoname.Text;
        }

    }
}
