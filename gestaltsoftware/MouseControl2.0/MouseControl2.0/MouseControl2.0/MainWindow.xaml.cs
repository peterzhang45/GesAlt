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
using KinectV2MouseControl; // why is this not necessary in the KinectV@MouseControl-master project? 
                            // because in that project, both the KinectControl class and the MainWindow class were placed in the same namespace - KinectV2MouseControl;

using LightBuzz.Vitruvius;
using Microsoft.Kinect;
using System.Runtime.InteropServices;

namespace MouseControl2._0 
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        KinectControl kinectCtrl = new KinectControl();

        // open another instance of sensor and gesture controller for vitruvius 
        // (in addition to instance instance within KinectControl. This is only for testing.)
        // KinectSensor _sensor;
        // GestureController _gestureController;

        public MainWindow()
        {
            InitializeComponent();

            //_gestureController = new GestureController();
            // subscribes to this MainWindow's instance of vitruvius gestureController an evt handler
            // with access to the window form. 
            //_gestureController.GestureRecognized += GestureController_GestureRecognized2;
           
        }

        // currently not sure how to pass gesture detected value from KinectControl.cs module to MouseControl2._)::tblGestures
        void GestureController_GestureRecognized2(object sender, GestureEventArgs e)
        {
            tblGestures.Text = e.GestureType.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectCtrl.Close();
        }
    }
}
