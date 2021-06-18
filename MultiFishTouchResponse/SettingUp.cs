using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MultiFishTouchResponse
{
    static class wellInformation
    {
        public static int wellNumberRow;
        public static int wellNumberCol;
        public static float wellDiameter;
        public static float wellDistance;
        public static int wellTpyeIndex;
        public static int fishPartIndex;
    }
    public class SettingUp : Form
    {
        public Button Saving_button;
        public TextBox wellNumberRowTxt;
        public TextBox wellNumberColTxt;
        public TextBox wellDiameterTxt;
        public TextBox wellDistanceTxt;
        public Label wellNumberRowTxtLabel;
        public Label wellNumberColLabel;
        public Label wellDiameterLabel;
        public Label wellDistanceLabel;

        public ComboBox comboBoxWellType;
        public ComboBox comboBoxFishPart;
        public SettingUp()
        {
            InitializeComponent();

            wellNumberRowTxt = new TextBox();
            wellNumberRowTxt.Size = new Size(100, 100);
            wellNumberRowTxt.Location = new Point(190, 60);

            wellNumberRowTxtLabel = new Label();
            wellNumberRowTxtLabel.Size = new Size(150, 50);
            wellNumberRowTxtLabel.Location = new Point(40, 60);
            wellNumberRowTxtLabel.Text = "Well Number";
            //this.Controls.Add(wellNumberRowTxt);
            this.Controls.Add(wellNumberRowTxtLabel);

            wellNumberColTxt = new TextBox();
            wellNumberColTxt.Size = new Size(100, 100);
            wellNumberColTxt.Location = new Point(190, 110);

            wellNumberColLabel = new Label();
            wellNumberColLabel.Size = new Size(150, 50);
            wellNumberColLabel.Location = new Point(40, 110);
            wellNumberColLabel.Text = "Which part to touch";
            //this.Controls.Add(wellNumberColTxt);
            this.Controls.Add(wellNumberColLabel);

            wellDiameterTxt = new TextBox();
            wellDiameterTxt.Size = new Size(100, 100);
            wellDiameterTxt.Location = new Point(190, 160);

            wellDiameterLabel = new Label();
            wellDiameterLabel.Size = new Size(150, 50);
            wellDiameterLabel.Location = new Point(40, 160);
            wellDiameterLabel.Text = "Well Diameter";
            //this.Controls.Add(wellDiameterTxt);
            //this.Controls.Add(wellDiameterLabel);

            wellDistanceTxt = new TextBox();
            wellDistanceTxt.Size = new Size(100, 100);
            wellDistanceTxt.Location = new Point(190, 210);

            wellDistanceLabel = new Label();
            wellDistanceLabel.Size = new Size(150, 50);
            wellDistanceLabel.Location = new Point(40, 210);
            wellDistanceLabel.Text = "Well Diameter";
            //this.Controls.Add(wellDistanceTxt);
            //this.Controls.Add(wellDistanceLabel);

            comboBoxWellType = new ComboBox();
            comboBoxWellType.Anchor = ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right);
            comboBoxWellType.DropDownWidth = 40;
            comboBoxWellType.Items.AddRange(new object[] {
                "6 wells",
                "12 wells",
                "24 wells",
                "48 wells",
                "96 wells"});
            comboBoxWellType.Location = new Point(190, 60);
            comboBoxWellType.Size = new Size(100, 40);
            comboBoxWellType.TabIndex = 7;
            this.Controls.Add(comboBoxWellType);

            comboBoxFishPart = new ComboBox();
            comboBoxFishPart.Anchor = ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right);
            comboBoxFishPart.DropDownWidth = 40;
            comboBoxFishPart.Items.AddRange(new object[] {
                "Head",
                "Body",
                "Tail"});
            comboBoxFishPart.Location = new Point(190, 110);
            comboBoxFishPart.Size = new Size(100, 40);
            comboBoxFishPart.TabIndex = 7;
            this.Controls.Add(comboBoxFishPart);

            Saving_button = new Button();
            Saving_button.Size = new Size(100, 40);
            Saving_button.Location = new Point(150, 260);
            Saving_button.Text = "Save setting";
            this.Controls.Add(Saving_button);

            Saving_button.Click += new EventHandler(Saving_button_Click);
        }

        public void show()
        {

            Application.EnableVisualStyles();
            Application.Run(new SettingUp());
        }

        private void Saving_button_Click(object sender, EventArgs e)
        {
            //wellInformation.wellNumberRow = Convert.ToInt32(wellNumberRowTxt.Text);
            //wellInformation.wellNumberCol = Convert.ToInt32(wellNumberColTxt.Text);
            //wellInformation.wellDiameter = float.Parse(wellDiameterTxt.Text);
            //wellInformation.wellDistance = float.Parse(wellDistanceTxt.Text);

            wellInformation.wellTpyeIndex = comboBoxWellType.SelectedIndex;
            wellInformation.fishPartIndex = comboBoxFishPart.SelectedIndex;

            Object wellTpyeselectedItem = comboBoxWellType.SelectedItem;

            MessageBox.Show("Selected number of wells: " + wellTpyeselectedItem.ToString() + "\n" +
                            "Index: " + wellInformation.wellTpyeIndex.ToString());

            Object fishPartselectedItem = comboBoxFishPart.SelectedItem;

            MessageBox.Show("Selected fish part: " + fishPartselectedItem.ToString() + "\n" +
                            "Index: " + wellInformation.fishPartIndex.ToString());
            this.Close();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // SettingUp
            // 
            this.ClientSize = new System.Drawing.Size(400, 350);
            this.Name = "SettingUp";
            this.Load += new System.EventHandler(this.SettingUp_Load);
            this.ResumeLayout(false);

        }

        private void SettingUp_Load(object sender, EventArgs e)
        {

        }
    }
}
