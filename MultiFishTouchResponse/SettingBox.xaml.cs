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

namespace MultiFishTouchResponse
{
    /// <summary>
    /// Interaction logic for SettingBox.xaml
    /// </summary>
    public partial class SettingBox : Window
    {
        public int WellNumber = 0;
        public int others = 0;
        public bool Saving_done = false;

        public SettingBox()
        {
            InitializeComponent();
        }


        private void SettingSave_Click(object sender, RoutedEventArgs e)
        {
            WellNumber = Convert.ToInt32(textBox_WellNumber.Text);
            Saving_done = true;
        }

        private void textBox_WellNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            WellNumber = Convert.ToInt32(textBox_WellNumber.Text);
        }
        private void textBox_Others_TextChanged(object sender, TextChangedEventArgs e)
        {
            others = Convert.ToInt32(textBox_Others.Text);
        }
    }
}
