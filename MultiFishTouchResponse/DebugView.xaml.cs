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
using System.Windows.Shapes;
using System.IO;

namespace MultiFishTouchResponse
{
    /// <summary>
    /// Interaction logic for DebugView.xaml
    /// </summary>
    public partial class DebugView : Window
    {
        ViewModel viewModel;

        public DebugView(ViewModel _viewModel)
        {
            InitializeComponent();
            viewModel = _viewModel;
            DataContext = viewModel;
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            label_fps.Content = Ximea.Framerate;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            NanotecSet set = new NanotecSet();
            set.AccRamp = 1;
            set.BrakeRamp = 0;
            set.Direction = 0;
            set.FinalSpeed = 10 * viewModel.StepMode;
            set.NextSet = 0;
            set.Pause = 0;
            set.PositionType = 2;
            set.RampType = 0;
            set.Repetitions = 1;
            set.StartSpeed = 1;


            set.Steps = Int32.Parse(textBox_motor1.Text);
            viewModel.motor1.SaveSet(set, 6);
            set.Steps = Int32.Parse(textBox_motor2.Text);
            viewModel.motor2.SaveSet(set, 6);
            set.Steps = Int32.Parse(textBox_motor4.Text);
            viewModel.motor4.SaveSet(set, 6);
            set.Steps = Int32.Parse(textBox_motor5.Text);
            viewModel.motor5.SaveSet(set, 6);
            set.Steps = Int32.Parse(textBox_motor3.Text);
            set.FinalSpeed = 1000 * viewModel.StepMode;
            viewModel.motor3.SaveSet(set, 6);

            if (Int32.Parse(textBox_motor1.Text) != 0)
                viewModel.motor1.Move(6);
            if (Int32.Parse(textBox_motor2.Text) != 0)
                viewModel.motor2.Move(6);
            if (Int32.Parse(textBox_motor3.Text) != 0)
                viewModel.motor3.Move(6);
            if (Int32.Parse(textBox_motor4.Text) != 0)
                viewModel.motor4.Move(6);
            if (Int32.Parse(textBox_motor5.Text) != 0)
                viewModel.motor5.Move(6);
        }

        private void button_SaveCameraImage_Click(object sender, RoutedEventArgs e)
        {
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            BitmapFrame outputFrame = BitmapFrame.Create(viewModel.CurrentCameraImage);
            encoder.Frames.Add(outputFrame);
            encoder.QualityLevel = 100;

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            if (saveFileDialog.ShowDialog() == true)
                using (FileStream file = File.OpenWrite(saveFileDialog.FileName))
                {
                    encoder.Save(file);
                }
        }

        private void button_MoveLine_Click(object sender, RoutedEventArgs e)
        {
            MoveLine2();
        }
        public async void MoveToPoint(System.Drawing.Point startPoint, System.Drawing.Point endPoint)
        {
            double linefactor = 0.294 / viewModel.Border.ActualWidth * 200 * viewModel.StepMode; //300 was pixel width of image when value was established with MQ003 - 257 with MQ013 camera - changed resolution with agar ring leads to factor 
            int speedx = 0;
            int speedy = 0;
            int speed_resulting = 5 * viewModel.StepMode;
            int speed_resulting_lastsegment = viewModel.final_speed_factor * viewModel.StepMode;
            int setCounterStart = 10;
            int setCounter = setCounterStart;


            NanotecSet setx = new NanotecSet();
            NanotecSet sety = new NanotecSet();
            double distancex = 0, distancey = 0;

            distancex = endPoint.X - startPoint.X + 0.001;
            distancey = endPoint.Y - startPoint.Y + 0.001;
            var distancet = Math.Sqrt(Math.Pow(distancex, 2) + Math.Pow(distancey, 2));

            //at the last line segment adjust speed
            //if (lineCnt + 1 == pointNum)
            //speed_resulting = speed_resulting_lastsegment;

            //velocity vector calculation with simple geometry
            speedx = Math.Abs(Convert.ToInt32(distancex * speed_resulting / distancet));
            speedy = Math.Abs(Convert.ToInt32(distancey * speed_resulting / distancet));

            //if (Math.Abs(distancex) > Math.Abs(distancey))
            //{
            //    //velocity vector calculation with simple geometry
            //    speedx = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancey / distancex, 2)))));
            //    speedy = Convert.ToInt32(Math.Abs(distancey / distancex) * speedx);
            //}
            //else
            //{
            //    speedy = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancex / distancey, 2)))));
            //    speedx = Convert.ToInt32(Math.Abs(distancex / distancey) * speedy);
            //}

            setx.StartSpeed = speedx - 1;
            setx.FinalSpeed = speedx;
            setx.PositionType = 1;
            setx.RampType = 0;
            setx.AccRamp = 65500;
            setx.BrakeRamp = 0;
            setx.Direction = 1;
            setx.Repetitions = 1;
            int stepsx = Convert.ToInt32(distancex * linefactor);
            setx.Steps = stepsx;

            sety.StartSpeed = speedy - 1;
            sety.FinalSpeed = speedy;
            sety.PositionType = 1;
            sety.RampType = 0;
            sety.AccRamp = 65500;
            sety.BrakeRamp = 0;
            sety.Direction = 1;
            sety.Repetitions = 1;
            int stepsy = Convert.ToInt32(distancey * linefactor);
            sety.Steps = stepsy;

            stepsy = Math.Abs(stepsy);
            stepsx = Math.Abs(stepsx);
            if (speedx == 0)
                setx.Pause = Convert.ToInt32(stepsy * 1.0 / speedy * 1000.0);
            else if (speedy == 0)
                sety.Pause = Convert.ToInt32(stepsx * 1.0 / speedx * 1000.0);
            else if (stepsx * 1.0 / speedx > stepsy * 1.0 / speedy)
                sety.Pause = Convert.ToInt32(Math.Abs(stepsx * 1.0 / speedx * 1000.0 - stepsy * 1.0 / speedy * 1000.0));
            else if (stepsx * 1.0 / speedx < stepsy * 1.0 / speedy)
                setx.Pause = Convert.ToInt32(Math.Abs(stepsy * 1.0 / speedy * 1000.0 - stepsx * 1.0 / speedx * 1000.0));
            /*
            if (lineCnt == viewModel.movePoints.Count - 1)
            {
                setx.NextSet = 0;
                sety.NextSet = 0;
            }
            else
            {
                if (lineCnt == viewModel.movePoints.Count - 2)
                {
                    setx.Pause = 500 + setx.Pause;
                    sety.Pause = 500 + sety.Pause;
                }
                setx.NextSet = setCounter + 1;
                sety.NextSet = setCounter + 1;
            }
            */

            await Task.Run(() =>
            {
                viewModel.motor1.SaveSet(setx, setCounter);
                viewModel.motor2.SaveSet(sety, setCounter);
            });
            //GC.Collect();
            setCounter++;

            viewModel.motor1.Move(setCounterStart);
            viewModel.motor2.Move(setCounterStart);

            Ximea.Recording = true;
            await Task.Run(() =>
            {
                while (viewModel.motor2.motor.IsMotorReady() == false || viewModel.motor1.motor.IsMotorReady() == false)
                {
                    Task.Delay(10).Wait();
                }
            });

        }
        public async void MoveLine()
        {
            double linefactor = 0.294 / viewModel.Border.ActualWidth * 200 * viewModel.StepMode; //300 was pixel width of image when value was established with MQ003 - 257 with MQ013 camera - changed resolution with agar ring leads to factor 
            int speedx = 0;
            int speedy = 0;
            int speed_resulting = 5 * viewModel.StepMode;
            int speed_resulting_lastsegment = viewModel.final_speed_factor * viewModel.StepMode;
            int setCounterStart = 10;
            int setCounter = setCounterStart;

            int lineCnt = 0;
            int pointNum = 0;
            while (lineCnt + 1 < pointNum)
            {
                NanotecSet setx = new NanotecSet();
                NanotecSet sety = new NanotecSet();
                double distancex = 0, distancey = 0;

                distancex = viewModel.movePoints[lineCnt + 1].X - viewModel.movePoints[lineCnt].X;
                distancey = viewModel.movePoints[lineCnt + 1].Y - viewModel.movePoints[lineCnt].Y;
                var distancet = Math.Sqrt(Math.Pow(distancex, 2) + Math.Pow(distancey, 2));

                //at the last line segment adjust speed
                if (lineCnt + 1 == pointNum)
                    speed_resulting = speed_resulting_lastsegment;

                //velocity vector calculation with simple geometry
                speedx = Math.Abs(Convert.ToInt32(distancex * speed_resulting / distancet));
                speedy = Math.Abs(Convert.ToInt32(distancey * speed_resulting / distancet));

                //if (Math.Abs(distancex) > Math.Abs(distancey))
                //{
                //    //velocity vector calculation with simple geometry
                //    speedx = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancey / distancex, 2)))));
                //    speedy = Convert.ToInt32(Math.Abs(distancey / distancex) * speedx);
                //}
                //else
                //{
                //    speedy = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancex / distancey, 2)))));
                //    speedx = Convert.ToInt32(Math.Abs(distancex / distancey) * speedy);
                //}

                setx.StartSpeed = speedx - 1;
                setx.FinalSpeed = speedx;
                setx.PositionType = 1;
                setx.RampType = 0;
                setx.AccRamp = 65500;
                setx.BrakeRamp = 0;
                setx.Direction = 1;
                setx.Repetitions = 1;
                int stepsx = Convert.ToInt32(distancex * linefactor);
                setx.Steps = stepsx;

                sety.StartSpeed = speedy - 1;
                sety.FinalSpeed = speedy;
                sety.PositionType = 1;
                sety.RampType = 0;
                sety.AccRamp = 65500;
                sety.BrakeRamp = 0;
                sety.Direction = 1;
                sety.Repetitions = 1;
                int stepsy = Convert.ToInt32(distancey * linefactor);
                sety.Steps = stepsy;

                stepsy = Math.Abs(stepsy);
                stepsx = Math.Abs(stepsx);
                if (speedx == 0)
                    setx.Pause = Convert.ToInt32(stepsy * 1.0 / speedy * 1000.0);
                else if (speedy == 0)
                    sety.Pause = Convert.ToInt32(stepsx * 1.0 / speedx * 1000.0);
                else if (stepsx * 1.0 / speedx > stepsy * 1.0 / speedy)
                    sety.Pause = Convert.ToInt32(Math.Abs(stepsx * 1.0 / speedx * 1000.0 - stepsy * 1.0 / speedy * 1000.0));
                else if (stepsx * 1.0 / speedx < stepsy * 1.0 / speedy)
                    setx.Pause = Convert.ToInt32(Math.Abs(stepsy * 1.0 / speedy * 1000.0 - stepsx * 1.0 / speedx * 1000.0));

                if (lineCnt == viewModel.movePoints.Count - 1)
                {
                    setx.NextSet = 0;
                    sety.NextSet = 0;
                }
                else
                {
                    if (lineCnt == viewModel.movePoints.Count - 2)
                    {
                        setx.Pause = 500 + setx.Pause;
                        sety.Pause = 500 + sety.Pause;
                    }
                    setx.NextSet = setCounter + 1;
                    sety.NextSet = setCounter + 1;
                }


                await Task.Run(() =>
                {
                    viewModel.motor1.SaveSet(setx, setCounter);
                    viewModel.motor2.SaveSet(sety, setCounter);
                });
                //GC.Collect();
                setCounter++;
                // add tje code ofmovving from one point to another point
                lineCnt++;
                pointNum = viewModel.Lines.Count;
            }
            try
            {
                for (int i = 0; i < viewModel.Lines.Count; i++)
                {
                    NanotecSet setx = new NanotecSet();
                    NanotecSet sety = new NanotecSet();
                    double distancex = 0, distancey = 0;

                    distancex = viewModel.Lines[i].X2 - viewModel.Lines[i].X1;
                    distancey = viewModel.Lines[i].Y2 - viewModel.Lines[i].Y1;
                    var distancet = Math.Sqrt(Math.Pow(distancex, 2) + Math.Pow(distancey, 2));

                    //at the last line segment adjust speed
                    if (i == viewModel.Lines.Count - 1)
                        speed_resulting = speed_resulting_lastsegment;

                    //velocity vector calculation with simple geometry
                    speedx = Math.Abs(Convert.ToInt32(distancex * speed_resulting / distancet));
                    speedy = Math.Abs(Convert.ToInt32(distancey * speed_resulting / distancet));

                    //if (Math.Abs(distancex) > Math.Abs(distancey))
                    //{
                    //    //velocity vector calculation with simple geometry
                    //    speedx = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancey / distancex, 2)))));
                    //    speedy = Convert.ToInt32(Math.Abs(distancey / distancex) * speedx);
                    //}
                    //else
                    //{
                    //    speedy = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancex / distancey, 2)))));
                    //    speedx = Convert.ToInt32(Math.Abs(distancex / distancey) * speedy);
                    //}

                    setx.StartSpeed = speedx - 1;
                    setx.FinalSpeed = speedx;
                    setx.PositionType = 1;
                    setx.RampType = 0;
                    setx.AccRamp = 65500;
                    setx.BrakeRamp = 0;
                    setx.Direction = 1;
                    setx.Repetitions = 1;
                    int stepsx = Convert.ToInt32(distancex * linefactor);
                    setx.Steps = stepsx;

                    sety.StartSpeed = speedy - 1;
                    sety.FinalSpeed = speedy;
                    sety.PositionType = 1;
                    sety.RampType = 0;
                    sety.AccRamp = 65500;
                    sety.BrakeRamp = 0;
                    sety.Direction = 1;
                    sety.Repetitions = 1;
                    int stepsy = Convert.ToInt32(distancey * linefactor);
                    sety.Steps = stepsy;

                    stepsy = Math.Abs(stepsy);
                    stepsx = Math.Abs(stepsx);
                    if (speedx == 0)
                        setx.Pause = Convert.ToInt32(stepsy * 1.0 / speedy * 1000.0);
                    else if (speedy == 0)
                        sety.Pause = Convert.ToInt32(stepsx * 1.0 / speedx * 1000.0);
                    else if (stepsx * 1.0 / speedx > stepsy * 1.0 / speedy)
                        sety.Pause = Convert.ToInt32(Math.Abs(stepsx * 1.0 / speedx * 1000.0 - stepsy * 1.0 / speedy * 1000.0));
                    else if (stepsx * 1.0 / speedx < stepsy * 1.0 / speedy)
                        setx.Pause = Convert.ToInt32(Math.Abs(stepsy * 1.0 / speedy * 1000.0 - stepsx * 1.0 / speedx * 1000.0));

                    if (i == viewModel.Lines.Count - 1)
                    {
                        setx.NextSet = 0;
                        sety.NextSet = 0;
                    }
                    else
                    {
                        if (i == viewModel.Lines.Count - 2)
                        {
                            setx.Pause = 500 + setx.Pause;
                            sety.Pause = 500 + sety.Pause;
                        }
                        setx.NextSet = setCounter + 1;
                        sety.NextSet = setCounter + 1;
                    }


                    await Task.Run(() =>
                    {
                        viewModel.motor1.SaveSet(setx, setCounter);
                        viewModel.motor2.SaveSet(sety, setCounter);
                    });
                    //GC.Collect();
                    setCounter++;
                }


                viewModel.motor1.Move(setCounterStart);
                viewModel.motor2.Move(setCounterStart);

                Ximea.Recording = true;
                await Task.Run(() =>
                {
                    while (viewModel.motor2.motor.IsMotorReady() == false || viewModel.motor1.motor.IsMotorReady() == false)
                    {
                        Task.Delay(100).Wait();
                    }
                    //Task.Delay(3000).Wait();
                    //Ximea.Recording = false;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public async void MoveLine2()
        {
            double linefactor = 0.294 / viewModel.Border.ActualWidth * 480 * viewModel.StepMode;
            // 1 / 0.204 / viewModel.Border.ActualWidth * 480; 
            // 0.294 / viewModel.Border.ActualWidth * 480 * viewModel.StepMode; 
            //300 was pixel width of image when value was established with MQ003 - 257 with MQ013 camera - changed resolution with agar ring leads to factor 
            int speedx = 0;
            int speedy = 0;
            int speed_resulting = 5 * viewModel.StepMode;
            int speed_resulting_lastsegment = viewModel.final_speed_factor * viewModel.StepMode;
            int setCounterStart = 10;
            int setCounter = setCounterStart;
            List<Line> twoLines = new List<Line>(2);

            try
            {
                if (viewModel.Lines.Count > 1)
                {
                    twoLines.Add(viewModel.Lines[viewModel.Lines.Count - 2]);
                    twoLines.Add(viewModel.Lines[viewModel.Lines.Count - 1]);
                    for (int i = 0; i < 2; i++)
                    {
                        NanotecSet setx = new NanotecSet();
                        NanotecSet sety = new NanotecSet();
                        double distancex = 0, distancey = 0;

                        distancex = viewModel.Lines[i].X2 - viewModel.Lines[i].X1;
                        distancey = viewModel.Lines[i].Y2 - viewModel.Lines[i].Y1;
                        var distancet = Math.Sqrt(Math.Pow(distancex, 2) + Math.Pow(distancey, 2));

                        //at the last line segment adjust speed
                        if (i == viewModel.Lines.Count - 1)
                            speed_resulting = speed_resulting_lastsegment;

                        //velocity vector calculation with simple geometry
                        speedx = Math.Abs(Convert.ToInt32(distancex * speed_resulting / distancet));
                        speedy = Math.Abs(Convert.ToInt32(distancey * speed_resulting / distancet));

                        //if (Math.Abs(distancex) > Math.Abs(distancey))
                        //{
                        //    //velocity vector calculation with simple geometry
                        //    speedx = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancey / distancex, 2)))));
                        //    speedy = Convert.ToInt32(Math.Abs(distancey / distancex) * speedx);
                        //}
                        //else
                        //{
                        //    speedy = Convert.ToInt32(Math.Sqrt((Math.Pow(speed_resulting, 2) / (1 + Math.Pow(distancex / distancey, 2)))));
                        //    speedx = Convert.ToInt32(Math.Abs(distancex / distancey) * speedy);
                        //}

                        setx.StartSpeed = speedx - 1;
                        setx.FinalSpeed = speedx;
                        setx.PositionType = 1;
                        setx.RampType = 0;
                        setx.AccRamp = 65500;
                        setx.BrakeRamp = 0;
                        setx.Direction = 1;
                        setx.Repetitions = 1;
                        int stepsx = Convert.ToInt32(distancex * linefactor);
                        setx.Steps = stepsx;

                        sety.StartSpeed = speedy - 1;
                        sety.FinalSpeed = speedy;
                        sety.PositionType = 1;
                        sety.RampType = 0;
                        sety.AccRamp = 65500;
                        sety.BrakeRamp = 0;
                        sety.Direction = 1;
                        sety.Repetitions = 1;
                        int stepsy = Convert.ToInt32(distancey * linefactor);
                        sety.Steps = stepsy;

                        stepsy = Math.Abs(stepsy);
                        stepsx = Math.Abs(stepsx);
                        if (speedx == 0)
                            setx.Pause = Convert.ToInt32(stepsy * 1.0 / speedy * 1000.0);
                        else if (speedy == 0)
                            sety.Pause = Convert.ToInt32(stepsx * 1.0 / speedx * 1000.0);
                        else if (stepsx * 1.0 / speedx > stepsy * 1.0 / speedy)
                            sety.Pause = Convert.ToInt32(Math.Abs(stepsx * 1.0 / speedx * 1000.0 - stepsy * 1.0 / speedy * 1000.0));
                        else if (stepsx * 1.0 / speedx < stepsy * 1.0 / speedy)
                            setx.Pause = Convert.ToInt32(Math.Abs(stepsy * 1.0 / speedy * 1000.0 - stepsx * 1.0 / speedx * 1000.0));

                        if (i == viewModel.Lines.Count - 1)
                        {
                            setx.NextSet = 0;
                            sety.NextSet = 0;
                        }
                        else
                        {
                            if (i == viewModel.Lines.Count - 2)
                            {
                                setx.Pause = 500 + setx.Pause;
                                sety.Pause = 500 + sety.Pause;
                            }
                            setx.NextSet = setCounter + 1;
                            sety.NextSet = setCounter + 1;
                        }


                        await Task.Run(() =>
                        {
                            viewModel.motor1.SaveSet(setx, setCounter);
                            viewModel.motor2.SaveSet(sety, setCounter);
                        });
                        //GC.Collect();
                        setCounter++;
                    }


                    viewModel.motor1.Move(setCounterStart);
                    viewModel.motor2.Move(setCounterStart);

                    Ximea.Recording = true;
                    await Task.Run(() =>
                    {
                        while (viewModel.motor2.motor.IsMotorReady() == false || viewModel.motor1.motor.IsMotorReady() == false)
                        {
                            Task.Delay(100).Wait();
                        }
                        Task.Delay(3000).Wait();
                        //Ximea.Recording = false;
                        viewModel.moveToFishFinished = true;
                    });

                }
                viewModel.Lines.Clear();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void textBox_motorSpeed_Copy_TextChanged(object sender, TextChangedEventArgs e)
        {
            viewModel.final_speed_factor = Int32.Parse(textBox_final_motorSpeed.Text);
        }
    }
}
