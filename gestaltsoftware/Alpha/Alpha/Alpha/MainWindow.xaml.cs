using System;
using System.Diagnostics;
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

using System.Runtime.InteropServices;


using Microsoft.Kinect;
using LightBuzz.Vitruvius;





namespace Alpha
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern int ShowCursor(bool bShow);

        private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);
            return placement;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
            IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public ShowWindowCommands showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        internal enum ShowWindowCommands : int
        {
            Hide = 0,
            Normal = 1,
            Minimized = 2,
            Maximized = 3,
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        MultiSourceFrameReader _reader;
        GestureController _gestureController;
        KinectSensor sensor;
        bool inPresentMode = false;

        void GestureController_GestureRecognized1(object sender, GestureEventArgs e)
        {
            // Simply for documentation purposes, pushing the below changes 
            // which attempted to replace kinect swipeleft/right with 
            // that of vitruvius'. Can delete/override as you see fit. GY 3/10/15 3:21pm 
            var gesture = e.GestureType;
            switch (gesture)
            {
                case(GestureType.SwipeRight):
                        MouseControl.ArrowRight();
                        break;
                default:
                    break;
            }
            Console.WriteLine("Gesture Recognized: " + e.GestureType.ToString());
            // minimize application switcher
            if (e.GestureType.ToString() == "ZoomIn")
            {
                WindowState = WindowState.Minimized;
            }
            // pull up application switcher
            else if (e.GestureType.ToString() == "ZoomOut")
            {
                WindowState = WindowState.Maximized;
            }
            // Maximize current window
            else if (e.GestureType.ToString() == "SwipeUp")
            {
                var placement = GetPlacement(GetForegroundWindow());
                var appMd = kinectCtrl.getAppMode();
                if (placement.showCmd == ShowWindowCommands.Maximized && appMd == "powerpoint")
                {
                    if (inPresentMode == false)
                    {
                        MouseControl.PressF5();
                        inPresentMode = true;
                    }
                    else
                    {
                        MouseControl.PressESC();
                        inPresentMode = false;
                    }

                }
                else
                {
                    ShowWindow(GetForegroundWindow(), 3);
                }
            }
            // Minimize current window
            else if (e.GestureType.ToString() == "SwipeDown")
            {
                ShowWindow(GetForegroundWindow(), 6);
            }
            else if (e.GestureType.ToString() == "SwipeLeft")
            {
                //MouseControl.ArrowLeft();
            }
            else if (e.GestureType.ToString() == "SwipeRight")
            {
                //MouseControl.ArrowRight();
            }

        }
        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();
            // Body
            using (var frame = reference.BodyFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    Body body = null;
                    float nearest = 1000.0f;
                    foreach (Body body_i in frame.Bodies())
                    {
                        if (body_i.IsTracked)
                        {
                            float dist = Math.Abs(body_i.Joints[JointType.SpineBase].Position.X);
                            if (dist < nearest)
                            {
                                nearest = dist;
                                body = body_i;
                            }
                        }
                    }
                    //Body body = frame.Bodies().Closest();
                    if (body != null)
                    {
                        _gestureController.Update(body);
                    }
                }
            }
        }

        KinectControl kinectCtrl = new KinectControl();

        public MainWindow()
        {
            InitializeComponent();

            sensor = kinectCtrl.getSensor();

            _reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
            _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

            _gestureController = new GestureController();
            // subscribe GestureController_GestureREcognized evt handler to GestureRecognized evt
            _gestureController.GestureRecognized += GestureController_GestureRecognized1;

            WindowState = WindowState.Minimized;

            ShowCursor(true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("loaded");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectCtrl.Close();
            ShowCursor(false);
        }

        public bool IsProcessOpen(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                Console.WriteLine(clsProcess.ProcessName);
            }
            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }

        public static Process GetExplorerProcess()
        {
            var all = Process.GetProcessesByName("explorer");
            Process process = null;
            foreach (var p in all)
                if (process == null || p.StartTime > process.StartTime)
                    process = p;
            return process;
        }

        public bool SwitchToProcess(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.Contains(name))
                {
                    var arrProcesses = Process.GetProcessesByName(clsProcess.ProcessName);
                    if (arrProcesses.Length >= 1)
                    {
                        for (int i = 0; i < arrProcesses.Length; i++) // loops for multi-thread apps like chrome
                        {
                            ShowWindow(arrProcesses[i].MainWindowHandle, 3);
                            ShowWindow(arrProcesses[i].MainWindowHandle, 5);//SW_SHOW  // Make the window visible if it was hidden
                            //ShowWindow(arrProcesses[i].MainWindowHandle, 9);//SW_RESTORE
                            //SwitchToThisWindow(arrProcesses[i].MainWindowHandle, false);
                            SetForegroundWindow(arrProcesses[i].MainWindowHandle);
                        }
                    }
                }
            }
            return true;
        }

        private void startMaximized(string appPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(appPath);
            startInfo.WindowStyle = ProcessWindowStyle.Maximized;
            Process.Start(startInfo);
        }

        private void Powerpoint_Click(object sender, RoutedEventArgs e)
        {
            if (IsProcessOpen("POWERPNT"))
            {
                SwitchToProcess("POWERPNT");
            }
            else
            {
                startMaximized("C:/Program Files (x86)/Microsoft Office/root/Office16/POWERPNT.EXE");
            }
        }

        private void Browser_Click(object sender, RoutedEventArgs e)
        {
            if (IsProcessOpen("chrome"))
            {
                SwitchToProcess("chrome");
            }
            else
            {
                startMaximized("C:/Program Files (x86)/Google/Chrome/Application/chrome.exe");
            }
        }

        private void Files_Click(object sender, RoutedEventArgs e)
        {
            if (IsProcessOpen("wmplayer"))
            {
                SwitchToProcess("wmplayer");
            }
            else
            {
                startMaximized("C:/Program Files (x86)/Windows Media Player/wmplayer.exe");
            }
        }

        private void PDF_Click(object sender, RoutedEventArgs e)
        {
            if (IsProcessOpen("AcroRd32"))
            {
                SwitchToProcess("AcroRd32");
            }
            else
            {
                startMaximized("C:/Program Files (x86)/Adobe/Acrobat Reader DC/Reader/AcroRd32.exe");
            }
        }

    }
}
