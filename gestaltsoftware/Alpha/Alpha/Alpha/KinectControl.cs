using System;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Collections.Generic;
using Microsoft.Kinect;

namespace Alpha
{
    class KinectControl
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        enum AppMode { Powerpoint, Desktop, Browser };
        enum ClickStatus { None, LeftClicked, RightClicked };
        enum ArrowStatus { None, LeftPressed, RightPressed };
        enum MoveType { None, Forward, Back, Left, Right, Up, Down };

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero)
            {
                AllocConsole();
            }
            else
            {
                ShowWindow(handle, SW_SHOW);
            }
        }

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();

            ShowWindow(handle, SW_HIDE);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        KinectSensor sensor;
        /// <summary>
        /// Reader for body frames
        /// </summary>
        BodyFrameReader bodyFrameReader;
        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;
        /// <summary>
        /// Screen width and height for determining the exact mouse sensitivity
        /// </summary>
        int screenWidth, screenHeight;
        /// <summary>
        /// Current application control mode
        /// </summary>
        AppMode appMode = AppMode.Desktop;

        /// <summary>
        /// timer for pause-to-click feature
        /// </summary>
        DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        /// How far the cursor move according to your hand's movement
        /// </summary>
        public float mouseSensitivity = MOUSE_SENSITIVITY;

        /// <summary>
        /// Time required as a pause-clicking
        /// </summary>
        public float timeRequired = TIME_REQUIRED;
        /// <summary>
        /// The radius range your hand move inside a circle for [timeRequired] seconds would be regarded as a pause-clicking
        /// </summary>
        public float pauseThresold = PAUSE_THRESOLD;
        /// <summary>
        /// Decide if the user need to do clicks or only move the cursor
        /// </summary>
        public bool doClick = DO_CLICK;
        /// <summary>
        /// Use Grip gesture to click or not
        /// </summary>
        public bool useGripGesture = USE_GRIP_GESTURE;
        /// <summary>
        /// Value 0 - 0.95f, the larger it is, the smoother the cursor would move
        /// </summary>
        public float cursorSmoothing = CURSOR_SMOOTHING;

        public bool relativeMouse = true;

        // Default values
        public const float MOUSE_SENSITIVITY = 3.5f;
        public const float TIME_REQUIRED = 2f;
        public const float PAUSE_THRESOLD = 60f;
        public const bool DO_CLICK = true;
        public const bool USE_GRIP_GESTURE = true;
        public const float CURSOR_SMOOTHING = 0.75f;

        const int ARROW_X_FRAMES_TRACKED = 6;

        const int MAX_MOVE_FRAMES = 15;

        const float FORWARD_THRESH = -0.02f;
        const float BACK_THRESH = 0.02f;
        const float LEFT_THRESH = -0.02f;
        const float RIGHT_THRESH = 0.02f;
        const float DOWN_THRESH = -0.02f;
        const float UP_THRESH = 0.02f;

        /// <summary>
        /// Determine if we have tracked the hand and used it to move the cursor,
        /// If false, meaning the user may not lift their hands, we don't get the last hand position and some actions like pause-to-click won't be executed.
        /// </summary>
        bool alreadyTrackedPos = false;

        /// <summary>
        /// for storing the time passed for pause-to-click
        /// </summary>
        float timeCount = 0;
        /// <summary>
        /// For storing last cursor position
        /// </summary>
        Point lastCurPos = new Point(0, 0);

        bool isRefPos = false;
        Point refOffset = new Point(0, 0);
        
        List<MoveType> lassoMoveHistory;
        MoveType actionState = MoveType.None;
        float prev_x = 0.0f, prev_y = 0.0f, prev_z = 0.0f;

        Queue<float> clickDepthHistory;
        Queue<float> clickXHistory;
        Queue<float> scrollYHistory;

        ArrowStatus arrowPressed = ArrowStatus.None;
        Queue<float> arrowXHistory;

        int framesReceived = 0;
        int timerTicks = 0;

        // constructor
        public KinectControl()
        {
            // get Active Kinect Sensor
            sensor = KinectSensor.GetDefault();
            // open the reader for the body frames
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

            // get screen with and height
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // set up timer, execute every 0.1s
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Tick += new EventHandler(Timer_Tick);
            timer.Start();

            // open the sensor
            sensor.Open();

            // initialize hand tracking state
            lassoMoveHistory = new List<MoveType>();
            clickDepthHistory = new Queue<float>();
            clickXHistory = new Queue<float>();
            scrollYHistory = new Queue<float>();
            arrowXHistory = new Queue<float>();

            ShowConsoleWindow();
        }

        public KinectSensor getSensor(){
            return sensor;
        }

        public string getAppMode()
        {
            if (appMode == AppMode.Powerpoint) return "powerpoint";
            if (appMode == AppMode.Browser) return "browser";
            if (appMode == AppMode.Desktop) return "desktop";
            return "other";
        }

        /// <summary>
        /// Pause to click timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Timer_Tick(object sender, EventArgs e)
        {
            // FPS counter
            timerTicks++;
            if (timerTicks >= 10)
            {
                Console.WriteLine("FPS: " + framesReceived.ToString());
                timerTicks = 0;
                framesReceived = 0;
            }
            // I dare you to find a hackier way of detecting the active application
            string active = GetActiveWindowTitle();
            if (active == null)
            {
                appMode = AppMode.Desktop;
            }
            else if (active.Contains("PowerPoint"))
            {
                appMode = AppMode.Powerpoint;
            }
            else if (active.Contains("Chrome") || active.Contains("Firefox") || active.Contains("Explorer"))
            {
                appMode = AppMode.Browser;
            }
            else
            {
                appMode = AppMode.Desktop;
            }

            if (!doClick || useGripGesture) return;

            if (!alreadyTrackedPos)
            {
                timeCount = 0;
                return;
            }

            Point curPos = MouseControl.GetCursorPosition();

            if ((lastCurPos - curPos).Length < pauseThresold)
            {
                if ((timeCount += 0.1f) > timeRequired)
                {
                    MouseControl.DoMouseClick();
                    timeCount = 0;
                }
            }
            else
            {
                timeCount = 0;
            }

            lastCurPos = curPos;
        }

        bool ListCheckLastN(List<MoveType> list, MoveType val, int n) {
            if (list.Count < n) {
                return false;
            }
            for (int i = list.Count - n; i < list.Count; i++) {
                if (list[i] != val) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Read body frames
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            framesReceived++;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (!dataReceived)
            {
                alreadyTrackedPos = false;
                return;
            }

            // get closest tracked body
            Body body = null;
            float nearest = 1000.0f;
            foreach (Body body_i in this.bodies)
            {
                if (body_i.IsTracked) {
                    float dist = Math.Abs(body_i.Joints[JointType.SpineBase].Position.X);
                    if (dist < nearest)
                    {
                        nearest = dist;
                        body = body_i;
                    }
                }
            }
            if (body != null)
            {
                // get various skeletal positions
                CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
                CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;

                if (handRight.Z < spineBase.Z - 0.15f || handRight.Y > spineBase.Y + 0.35f) // if right hand lifted or forward
                {
                    float x = handRight.X - spineBase.X + 0.05f;
                    float y = spineBase.Y - handRight.Y + 0.51f;
                    // should be used purely relative anyways, so no additive measure needed
                    float z = handRight.Z - spineBase.Z;

                    ProcHand(x, y, z, body.HandRightState, "right");

                    alreadyTrackedPos = true;
                }
                else if (handLeft.Z < spineBase.Z - 0.15f || handLeft.Y > spineBase.Y + 0.35f) // if left hand lifted or forward
                {
                    float x = handLeft.X - spineBase.X + 0.3f;
                    float y = spineBase.Y - handLeft.Y + 0.51f;
                    float z = handLeft.Z - spineBase.Z;
                    ProcHand(x, y, z, body.HandLeftState, "left");
                    alreadyTrackedPos = true;
                }
                else
                {
                    alreadyTrackedPos = false;
                }
            }
        }

        public void Close()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

        private void ProcHand(float x, float y, float z, HandState handState, string which_hand)
        {
            if (handState == HandState.Closed)
            {
                Point curPos = MouseControl.GetCursorPosition();
                float smoothing = 1 - cursorSmoothing;
                int targetX = (int)(x * mouseSensitivity * screenWidth);
                int targetY = (int)((y + 0.25f) * mouseSensitivity * screenHeight);

                if (relativeMouse)
                {
                    if (!isRefPos)
                    {
                        refOffset.X = targetX - curPos.X;
                        refOffset.Y = targetY - curPos.Y;
                        isRefPos = true;
                    }
                    targetX -= (int)refOffset.X;
                    targetY -= (int)refOffset.Y;
                }

                MouseControl.SetCursorPos((int)(curPos.X * cursorSmoothing + targetX * smoothing), (int)(curPos.Y * cursorSmoothing + targetY * smoothing));
            } else {
                isRefPos = false;
            }

            if (handState == HandState.Lasso)
            {
                float delta_x = x - prev_x;
                float delta_y = y - prev_y;
                float delta_z = z - prev_z;
                string dominant;
                if (Math.Abs(delta_x) >= Math.Abs(delta_y) && Math.Abs(delta_x) >= Math.Abs(delta_z)) {
                    dominant = "x";
                } else if (Math.Abs(delta_y) >= Math.Abs(delta_x) && Math.Abs(delta_y) >= Math.Abs(delta_z)) {
                    dominant = "y";
                } else {
                    dominant = "z";
                }

                if (dominant == "x" && delta_z <= LEFT_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Left);
                }
                else if (dominant == "x" && delta_z >= RIGHT_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Right);
                }
                else if (dominant == "y" && delta_y <= DOWN_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Down);
                }
                else if (dominant == "y" && delta_y >= UP_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Up);
                }
                else if (dominant == "z" && delta_z <= FORWARD_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Forward);
                }
                else if (dominant == "z" && delta_z >= BACK_THRESH)
                {
                    lassoMoveHistory.Add(MoveType.Back);
                }
                else
                {
                    lassoMoveHistory.Add(MoveType.None);
                }
                // This makes it act more like a queue
                if (lassoMoveHistory.Count > MAX_MOVE_FRAMES) {
                    lassoMoveHistory.RemoveAt(0);
                }
                // Now for adjudicating actions
                if (actionState != MoveType.None && ListCheckLastN(lassoMoveHistory, MoveType.None, 2))
                {
                    actionState = MoveType.None;
                }
                else if (actionState != MoveType.Forward && ListCheckLastN(lassoMoveHistory, MoveType.Forward, 3))
                {
                    actionState = MoveType.Forward;
                    MouseControl.DoMouseClick();
                }
                else if (actionState != MoveType.Right && ListCheckLastN(lassoMoveHistory, MoveType.Right, 3))
                {
                    actionState = MoveType.Right;
                    MouseControl.DoRightClick();
                }
                else if (ListCheckLastN(lassoMoveHistory, MoveType.Up, 4))
                {
                    actionState = MoveType.Up;
                    MouseControl.ScrollUp();
                }
                else if (ListCheckLastN(lassoMoveHistory, MoveType.Down, 4))
                {
                    actionState = MoveType.Down;
                    MouseControl.ScrollDown();
                }
            }
            else
            {
                lassoMoveHistory.Clear();
                actionState = MoveType.None;
            }

            if (handState == HandState.Open)
            {
                // velocity -> arrows
                arrowXHistory.Enqueue(x);
                if (arrowXHistory.Count > ARROW_X_FRAMES_TRACKED) {
                    float delta_x = x - arrowXHistory.Dequeue();
                    if (arrowPressed == ArrowStatus.None && delta_x < -0.10f && which_hand == "left")
                    {
                        // arrow left
                        arrowPressed = ArrowStatus.LeftPressed;
                        MouseControl.ArrowLeft();
                    }
                    else if (arrowPressed == ArrowStatus.None && delta_x > 0.10f && which_hand == "right")
                    {
                        // arrow right
                        arrowPressed = ArrowStatus.RightPressed;
                        MouseControl.ArrowRight();
                    }
                    else if (arrowPressed == ArrowStatus.LeftPressed && delta_x > 0.0f)
                    {
                        // reset left
                        arrowPressed = ArrowStatus.None;
                    }
                    else if (arrowPressed == ArrowStatus.RightPressed && delta_x < 0.0f)
                    {
                        // reset right
                        arrowPressed = ArrowStatus.None;
                    }
                }
                
            }
            else
            {
                // reset arrow state
                arrowXHistory.Clear();
                arrowPressed = ArrowStatus.None;
            }
            prev_x = x;
            prev_y = y;
            prev_z = z;
        }

    }
}
