using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandsPD4I;
using System.ComponentModel;

namespace MultiFishTouchResponse
{
    public class NanotecSet
    {
        // Set relative positioning mode
        // positionType = 1 entspricht relativ; abhängig vom Operationsmodus
        // positionType = 2 entspricht absolut; abhängig vom Operationsmodus
        // positionType = 3 entspricht interner Referenzfahrt;
        // positionType = 4 entspricht externer Referenzfahrt;
        // direction = 0 entspricht links
        // direction = 1 entspricht rechts
        public int PositionType { get; set; }
        public int Steps { get; set; }
        public int Direction { get; set; }
        public int StartSpeed { get; set; }
        public int FinalSpeed { get; set; }

        //Hz/ms = ( (3000.0 / sqrt((float)<parameter>)) - 11.7 )
        //3000/(Hz/ms+11.7)=parameter
        //Bsp: sqrt(3000/(4+11.7))=36513
        private int accRamp;
        public int AccRamp
        {
            get { return accRamp; }
            set { accRamp = Convert.ToInt32(3000 / (value + 11.7)); }
        }
        public int BrakeRamp { get; set; }
        public int Pause { get; set; }
        public int Repetitions { get; set; }
        public int NextSet { get; set; }
        public int RampType { get; set; }
    }

    public class NanotecController : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public ComMotorCommands motor = new ComMotorCommands();
        int motorposition;
        int Motoradresse;
        public int Motorposition
        {
            get { return motorposition; }
            set
            {
                motorposition = Math.Abs(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Motorposition"));
            }
        }

        public NanotecSet ReadSet(int SetNo)
        {
            NanotecSet set = new NanotecSet();
            //set commands
            set.AccRamp = motor.GetRamp(SetNo);
            set.BrakeRamp = motor.GetBrakeRamp(SetNo);
            set.Direction = motor.GetDirection(SetNo);
            set.FinalSpeed = motor.GetMaxFrequency(SetNo);
            set.NextSet = motor.GetNextOperation(SetNo);
            set.Pause = Convert.ToInt16(motor.GetBreak(SetNo));
            set.PositionType = motor.GetPositionType(SetNo);
            set.RampType = motor.GetRampType(SetNo);
            set.Repetitions = motor.GetRepeat(SetNo);
            set.StartSpeed = Convert.ToInt32(motor.GetStartFrequency(SetNo));
            set.Steps = motor.GetSteps(SetNo);

            return set;
        }

        public void SaveSet(NanotecSet set, int SetNo)
        {
            //set commands
            motor.SetPositionType(set.PositionType);
            motor.SetSteps(set.Steps);
            motor.SetDirection(set.Direction);
            motor.SetStartFrequency(set.StartSpeed);
            motor.SetMaxFrequency(set.FinalSpeed);
            motor.SetRamp(set.AccRamp);
            motor.SetBrakeRamp(set.BrakeRamp);
            motor.SetBreak(set.Pause);
            motor.SetRepeat(set.Repetitions);
            motor.SetNextOperation(set.NextSet);
            motor.SetRampType(set.RampType);

            //save as record
            motor.SetRecord(SetNo);
        }

        public NanotecController(int motoradresse)
        {
            motor.SelectedPort = "COM3";
            motor.Baudrate = 115200;
            motor.MotorAddresse = motoradresse;
            Motoradresse = motoradresse;

            Task.Delay(200).Wait();
            System.Timers.Timer PollMotors = new System.Timers.Timer();
            PollMotors.AutoReset = true;
            PollMotors.Interval = 100;
            PollMotors.Elapsed += (sender, e) =>
            {
                Motorposition = GetPosition();
            };
            PollMotors.Start();
        }

        public void Stop()
        {
            motor.QuickStopTravelProfile();
        }

        public void Move(int setNo)
        {
            motor.ChooseRecord(setNo);

            //actually move
            bool success = motor.StartTravelProfile();
            if (success == false)
                motor.ResetPositionError(true, Motorposition);
        }

        public void Move(int Steps, int direction)
        {
            //actually move
            motor.SetPositionType(2);
            motor.SetNextOperation(0);
            motor.SetSteps(Steps);
            motor.SetDirection(direction);
            bool success = motor.StartTravelProfile();
            if (success == false)
                motor.ResetPositionError(true, Motorposition);
        }

        public int GetPosition()
        {
            if (Motoradresse != 4)
                return motor.GetPosition();
            else
                return motor.GetEncoderRotary();

        }
    }
}
