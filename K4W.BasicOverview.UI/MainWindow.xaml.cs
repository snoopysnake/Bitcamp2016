using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Drawing;

using Microsoft.Kinect;

namespace K4W.BasicOverview.UI
{
    public partial class MainWindow : Window
    {

        private Dictionary<JointType, bool> jointMap = new Dictionary<JointType, bool>();

        /// <summary>
        /// Size fo the RGB pixel in bitmap
        /// </summary>
        private readonly int _bytePerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        /// <summary>
        /// Representation of the Kinect Sensor
        /// </summary>
        private KinectSensor _kinect = null;

        /// <summary>
        /// FrameReader for our coloroutput
        /// </summary>
        private ColorFrameReader _colorReader = null;

        /// <summary>
        /// FrameReader for our depth output
        /// </summary>
        private DepthFrameReader _depthReader = null;

        /// <summary>
        /// FrameReader for our infrared output
        /// </summary>
        private InfraredFrameReader _infraReader = null;

        /// <summary>
        /// FrameReader for our body output
        /// </summary>
        private BodyFrameReader _bodyReader = null;

        /// <summary>
        /// Array of color pixels
        /// </summary>
        private byte[] _colorPixels = null;

        /// <summary>
        /// Array of depth pixels used for the output
        /// </summary>
        private byte[] _depthPixels = null;

        /// <summary>
        /// Array of infrared pixels used for the output
        /// </summary>
        private byte[] _infraPixels = null;

        /// <summary>
        /// Array of depth values
        /// </summary>
        private ushort[] _depthData = null;

        /// <summary>
        /// Array of infrared data
        /// </summary>
        private ushort[] _infraData = null;

        /// <summary>
        /// All tracked bodies
        /// </summary>
        private Body[] _bodies = null;

        /// <summary>
        /// Color WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _colorBitmap = null;

        /// <summary>
        /// Color WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _depthBitmap = null;

        /// <summary>
        /// Infrared WriteableBitmap linked to our UI
        /// </summary>
        private WriteableBitmap _infraBitmap = null;

        private Window testWindow;
        private Canvas myCanvas;
        private Boolean trackingBarPath = false;

        /// <summary>
        /// Default CTOR
        /// </summary>
        public MainWindow()
        {
            // Default the feet and hands to true
            jointMap.Add(JointType.FootLeft, true);
            jointMap.Add(JointType.FootRight, true);

            InitializeComponent();

            // Initialize Kinect
            InitializeKinect();

            // Close Kinect when closing app
            Closing += OnClosing;
        }

        /// <summary>
        /// Close Kinect & Kinect Service
        /// </summary>
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close Kinect
            if (_kinect != null) _kinect.Close();
        }


        #region INITIALISATION
        /// <summary>
        /// Initialize Kinect Sensor
        /// </summary>
        private void InitializeKinect()
        {
            // Get first Kinect
            _kinect = KinectSensor.GetDefault();

            if (_kinect == null) return;

            // Open connection
            _kinect.Open();

            // Initialize Camera
            InitializeCamera();

            // Initialize Depth
            InitializeDepth();

            // Initialize Infrared
            InitializeInfrared();

            // Initialize Body
            IntializeBody();
        }

        /// <summary>
        /// Initialize Kinect Camera
        /// </summary>
        private void InitializeCamera()
        {
            if (_kinect == null) return;

            // Get frame description for the color output
            FrameDescription desc = _kinect.ColorFrameSource.FrameDescription;

            // Get the framereader for Color
            _colorReader = _kinect.ColorFrameSource.OpenReader();

            // Allocate pixel array
            _colorPixels = new byte[desc.Width * desc.Height * _bytePerPixel];

            // Create new WriteableBitmap
            _colorBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);

            // Link WBMP to UI
            CameraImage.Source = _colorBitmap;

            // Hook-up event
            _colorReader.FrameArrived += OnColorFrameArrived;
        }

        /// <summary>
        /// Initialize Kinect Depth
        /// </summary>
        private void InitializeDepth()
        {
            if (_kinect == null) return;

            // Get frame description for the color output
            FrameDescription desc = _kinect.DepthFrameSource.FrameDescription;

            // Get the framereader for Color
            _depthReader = _kinect.DepthFrameSource.OpenReader();

            // Allocate pixel array
            _depthData = new ushort[desc.Width * desc.Height];
            _depthPixels = new byte[desc.Width * desc.Height * _bytePerPixel];

            // Create new WriteableBitmap
            _depthBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);

            // Link WBMP to UI
            DepthImage.Source = _depthBitmap;

            // Hook-up event
            _depthReader.FrameArrived += OnDepthFrameArrived;
        }

        /// <summary>
        /// Initialize Kinect Infrared
        /// </summary>
        private void InitializeInfrared()
        {
            if (_kinect == null) return;

            // Get frame description for the color output
            FrameDescription desc = _kinect.InfraredFrameSource.FrameDescription;

            // Get the framereader for Color
            _infraReader = _kinect.InfraredFrameSource.OpenReader();

            // Allocate pixel array
            _infraData = new ushort[desc.Width * desc.Height];
            _infraPixels = new byte[desc.Width * desc.Height * _bytePerPixel];

            // Create new WriteableBitmap
            _infraBitmap = new WriteableBitmap(desc.Width, desc.Height, 96, 96, PixelFormats.Bgr32, null);

            // Link WBMP to UI
            InfraredImage.Source = _infraBitmap;

            // Hook-up event
            _infraReader.FrameArrived += OnInfraredFrameArrived;
        }

        /// <summary>
        /// Initialize Body Tracking
        /// </summary>
        private void IntializeBody()
        {
            if (_kinect == null) return;

            // Allocate Bodies array
            _bodies = new Body[_kinect.BodyFrameSource.BodyCount];

            // Open reader
            _bodyReader = _kinect.BodyFrameSource.OpenReader();

            // Hook-up event
            _bodyReader.FrameArrived += OnBodyFrameArrived;
        }
        #endregion INITIALISATION


        #region FRAME PROCESSING
        /// <summary>
        /// Process color frames & show in UI
        /// </summary>
        private void OnColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // Get the reference to the color frame
            ColorFrameReference colorRef = e.FrameReference;

            if (colorRef == null) return;

            // Acquire frame for specific reference
            ColorFrame frame = colorRef.AcquireFrame();

            // It's possible that we skipped a frame or it is already gone
            if (frame == null) return;

            using (frame)
            {
                // Get frame description
                FrameDescription frameDesc = frame.FrameDescription;

                // Check if width/height matches
                if (frameDesc.Width == _colorBitmap.PixelWidth && frameDesc.Height == _colorBitmap.PixelHeight)
                {
                    // Copy data to array based on image format
                    if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        frame.CopyRawFrameDataToArray(_colorPixels);
                    }
                    else frame.CopyConvertedFrameDataToArray(_colorPixels, ColorImageFormat.Bgra);

                    // Copy output to bitmap
                    _colorBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _colorPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the depth frames and update UI
        /// </summary>
        private void OnDepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            DepthFrameReference refer = e.FrameReference;

            if (refer == null) return;

            DepthFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            using (frame)
            {
                FrameDescription frameDesc = frame.FrameDescription;

                if (((frameDesc.Width * frameDesc.Height) == _depthData.Length) && (frameDesc.Width == _depthBitmap.PixelWidth) && (frameDesc.Height == _depthBitmap.PixelHeight))
                {
                    // Copy depth frames
                    frame.CopyFrameDataToArray(_depthData);

                    // Get min & max depth
                    ushort minDepth = frame.DepthMinReliableDistance;
                    ushort maxDepth = frame.DepthMaxReliableDistance;

                    // Adjust visualisation
                    int colorPixelIndex = 0;
                    for (int i = 0; i < _depthData.Length; ++i)
                    {
                        // Get depth value
                        ushort depth = _depthData[i];

                        if (depth == 0)
                        {
                            _depthPixels[colorPixelIndex++] = 41;
                            _depthPixels[colorPixelIndex++] = 239;
                            _depthPixels[colorPixelIndex++] = 242;
                        }
                        else if (depth < minDepth || depth > maxDepth)
                        {
                            _depthPixels[colorPixelIndex++] = 25;
                            _depthPixels[colorPixelIndex++] = 0;
                            _depthPixels[colorPixelIndex++] = 255;
                        }
                        else
                        {
                            double gray = (Math.Floor((double)depth / 250) * 12.75);

                            _depthPixels[colorPixelIndex++] = (byte)gray;
                            _depthPixels[colorPixelIndex++] = (byte)gray;
                            _depthPixels[colorPixelIndex++] = (byte)gray;
                        }

                        // Increment
                        ++colorPixelIndex;
                    }

                    // Copy output to bitmap
                    _depthBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _depthPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the infrared frames and update UI
        /// </summary>
        private void OnInfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            // Reference to infrared frame
            InfraredFrameReference refer = e.FrameReference;

            if (refer == null) return;

            // Get infrared frame
            InfraredFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            // Process it
            using (frame)
            {
                // Get the description
                FrameDescription frameDesc = frame.FrameDescription;

                if (((frameDesc.Width * frameDesc.Height) == _infraData.Length) && (frameDesc.Width == _infraBitmap.PixelWidth) && (frameDesc.Height == _infraBitmap.PixelHeight))
                {
                    // Copy data
                    frame.CopyFrameDataToArray(_infraData);

                    int colorPixelIndex = 0;

                    for (int i = 0; i < _infraData.Length; ++i)
                    {
                        // Get infrared value
                        ushort ir = _infraData[i];

                        // Bitshift
                        byte intensity = (byte)(ir >> 8);

                        // Assign infrared intensity
                        _infraPixels[colorPixelIndex++] = intensity;
                        _infraPixels[colorPixelIndex++] = intensity;
                        _infraPixels[colorPixelIndex++] = intensity;

                        ++colorPixelIndex;
                    }

                    // Copy output to bitmap
                    _infraBitmap.WritePixels(
                            new Int32Rect(0, 0, frameDesc.Width, frameDesc.Height),
                            _infraPixels,
                            frameDesc.Width * _bytePerPixel,
                            0);
                }
            }
        }

        /// <summary>
        /// Process the body-frames and draw joints
        /// </summary>
        private void OnBodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            // Get frame reference
            BodyFrameReference refer = e.FrameReference;

            if (refer == null) return;

            // Get body frame
            BodyFrame frame = refer.AcquireFrame();

            if (frame == null) return;

            using (frame)
            {
                // Aquire body data
                frame.GetAndRefreshBodyData(_bodies);

                // Clear Skeleton Canvas
                SkeletonCanvas.Children.Clear();

                // Loop all bodies
                foreach (Body body in _bodies)
                {
                    // Only process tracked bodies
                    if (body.IsTracked)
                    {
                        DrawBody(body);
                    }
                }
            }
        }

        /// <summary>
        /// Visualize the body
        /// </summary>
        /// <param name="body">Tracked body</param>
        private void DrawBody(Body body)
        {
            int largestRadius = 20;
            int bigRadius = 10;
            int smallRadius = 7;
            // Draw points
            foreach (JointType type in body.Joints.Keys)
            {
                if (jointMap.ContainsKey(type) && jointMap[type])
                {
                    // Draw all the body joints
                    switch (type)
                    {
                        //case JointType.Head:
                        //    DrawJoint(body.Joints[type], 20, Brushes.Yellow, 2, Brushes.White);
                        //    Console.WriteLine(body.Joints[type].Position.Z);
                        //    break;
                        case JointType.FootLeft:
                        case JointType.FootRight:
                            DrawJoint(body.Joints[type], largestRadius, Brushes.Yellow, 2, Brushes.Yellow);
                            break;
                        //    DrawJoint(body.Joints[type], 20, Brushes.Yellow, 2, Brushes.White);
                        //    break;
                        case JointType.ShoulderLeft:
                        case JointType.ShoulderRight:
                            DrawJoint(body.Joints[type], bigRadius, Brushes.Purple, 2, Brushes.Purple);
                            break;
                        case JointType.HipLeft:
                        case JointType.HipRight:
                            DrawJoint(body.Joints[type], smallRadius, Brushes.Violet, 2, Brushes.Violet);
                            break;
                        //    DrawJoint(body.Joints[type], 20, Brushes.YellowGreen, 2, Brushes.White);
                        //    break;
                        //case JointType.ElbowLeft:
                        //case JointType.ElbowRight:
                        case JointType.KneeLeft:
                        case JointType.KneeRight:
                            DrawJoint(body.Joints[type], smallRadius, Brushes.LawnGreen, 2, Brushes.White);
                            break;
                        case JointType.HandLeft:
                        case JointType.HandRight:
                            DrawHandJoint(body.Joints[type], body.HandRightState, largestRadius, 2, Brushes.Red);
                            break;
                            //case JointType.SpineBase:
                            //    DrawJoint(body.Joints[type], 15, Brushes.Blue, 2, Brushes.Blue);
                            //    break;
                            //case JointType.SpineMid:
                            //    DrawJoint(body.Joints[type], 15, Brushes.SlateBlue, 2, Brushes.SlateBlue);
                            //    break;
                            //case JointType.SpineShoulder:
                            //    DrawJoint(body.Joints[type], 15, Brushes.SkyBlue, 2, Brushes.SkyBlue);
                            //    break;
                            //default:
                            //    DrawJoint(body.Joints[type], 15, Brushes.RoyalBlue, 2, Brushes.White);
                            //    break;
                    }
                }
                
            }
        }


        /// <summary>
        /// Draws a body joint
        /// </summary>
        /// <param name="joint">Joint of the body</param>
        /// <param name="radius">Circle radius</param>
        /// <param name="fill">Fill color</param>
        /// <param name="borderWidth">Thickness of the border</param>
        /// <param name="border">Color of the boder</param>
        private void DrawJoint(Joint joint, double radius, SolidColorBrush fill, double borderWidth, SolidColorBrush border)
        {
            if (joint.TrackingState != TrackingState.Tracked) return;
            
            // Map the CameraPoint to ColorSpace so they match
            ColorSpacePoint colorPoint = _kinect.CoordinateMapper.MapCameraPointToColorSpace(joint.Position);

            // Create the UI element based on the parameters
            Ellipse el = new Ellipse();
            el.Fill = fill;
            el.Stroke = border;
            el.StrokeThickness = borderWidth;
            el.Width = el.Height = radius;


            Ellipse cb = new Ellipse();
            cb.Fill = fill;
            cb.Stroke = border;
            el.StrokeThickness = borderWidth;
            cb.Height = radius;
            cb.Width = radius;

            //hand
            Ellipse te = new Ellipse();
            te.Fill = fill;
            te.Stroke = border;
            te.Height = radius;
            te.Width = radius;
            te.Opacity = 0.25f;

            TextBlock tb = new TextBlock();
            tb.Text = joint.Position.Y.ToString();
            tb.FontSize = 34;
            tb.Width = cb.Width;
            tb.Height = cb.Height;

            //bar path
            if (trackingBarPath)
            {
                //if (joint.JointType.Equals(JointType.HandLeft) || joint.JointType.Equals(JointType.HandRight)) {
                Canvas.SetLeft(te, colorPoint.X / 2 - radius / 2);
                Canvas.SetTop(te, colorPoint.Y / 2 - radius / 2);
                myCanvas.Children.Add(te);

                Button button = new Button { Content = "Button", Width = 100, Height = 25 };
                button.Click += closeButton;
                Canvas.SetBottom(button, 50);
                myCanvas.Children.Add(button);

                testWindow.Content = myCanvas;
                testWindow.Show();
                //}
            }

            // Add the Ellipse to the canvas
            //SkeletonCanvas.Children.Add(el);
            SkeletonCanvas.Children.Add(cb);
            //SkeletonCanvas.Children.Add(tb);

            // Avoid exceptions based on bad tracking
            if (float.IsInfinity(colorPoint.X) || float.IsInfinity(colorPoint.X)) return;

            // Allign ellipse on canvas (Divide by 2 because image is only 50% of original size)
            //Canvas.SetLeft(el, colorPoint.X / 2);
            //Canvas.SetTop(el, colorPoint.Y / 2);

            
            Canvas.SetLeft(cb, colorPoint.X / 2 - radius/2);
            Canvas.SetTop(cb, colorPoint.Y / 2 - radius/2);
        }

        void closeButton(Object sender, EventArgs e)
        {
            //exports and closes
            trackingBarPath = false;
            testWindow.Close();
        }

        private void HandleCheckboxChange(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;

            if ((bool) checkBox.IsChecked)
            {
                switch (checkBox.Name)
                {
                    case "FeetCheckbox":
                        if (!jointMap.ContainsKey(JointType.FootRight))
                        {
                            jointMap.Add(JointType.FootRight, true);
                        }
                        if (!jointMap.ContainsKey(JointType.FootLeft))
                        {
                            jointMap.Add(JointType.FootLeft, true);
                        }

                        jointMap[JointType.FootLeft] = true;
                        jointMap[JointType.FootRight] = true;
                        break;
                    case "HandsCheckbox":
                        if (!jointMap.ContainsKey(JointType.HandRight))
                        {
                            jointMap.Add(JointType.HandRight, true);
                        }
                        if (!jointMap.ContainsKey(JointType.HandLeft))
                        {
                            jointMap.Add(JointType.HandLeft, true);
                        }

                        jointMap[JointType.HandLeft] = true;
                        jointMap[JointType.HandRight] = true;
                        break;
                    case "ShouldersCheckbox":
                        if (!jointMap.ContainsKey(JointType.ShoulderRight))
                        {
                            jointMap.Add(JointType.ShoulderRight, true);
                        }
                        if (!jointMap.ContainsKey(JointType.ShoulderLeft))
                        {
                            jointMap.Add(JointType.ShoulderLeft, true);
                        }

                        jointMap[JointType.ShoulderLeft] = true;
                        jointMap[JointType.ShoulderRight] = true;
                        break;
                    case "HipsCheckbox":
                        if (!jointMap.ContainsKey(JointType.HipRight))
                        {
                            jointMap.Add(JointType.HipRight, true);
                        }
                        if (!jointMap.ContainsKey(JointType.HipLeft))
                        {
                            jointMap.Add(JointType.HipLeft, true);
                        }

                        jointMap[JointType.HipLeft] = true;
                        jointMap[JointType.HipRight] = true;
                        break;
                    case "KneesCheckbox":
                        if (!jointMap.ContainsKey(JointType.KneeRight))
                        {
                            jointMap.Add(JointType.KneeRight, true);
                        }
                        if (!jointMap.ContainsKey(JointType.KneeLeft))
                        {
                            jointMap.Add(JointType.KneeLeft, true);
                        }

                        jointMap[JointType.KneeLeft] = true;
                        jointMap[JointType.KneeRight] = true;
                        break;
                    default:
                        break;
                }
            } else
            {
                switch (checkBox.Name)
                {
                    case "FeetCheckbox":
                        jointMap[JointType.FootLeft] = false;
                        jointMap[JointType.FootRight] = false;
                        break;
                    case "HandsCheckbox":
                        jointMap[JointType.HandRight] = false;
                        jointMap[JointType.HandLeft] = false;
                        break;
                    case "ShouldersCheckbox":
                        jointMap[JointType.ShoulderRight] = false;
                        jointMap[JointType.ShoulderLeft] = false;
                        break;
                    case "HipsCheckbox":
                        jointMap[JointType.HipRight] = false;
                        jointMap[JointType.HipLeft] = false;
                        break;
                    case "KneesCheckbox":
                        jointMap[JointType.KneeLeft] = false;
                        jointMap[JointType.KneeRight] = false;
                        break;
                    default:
                        break;
                }
            }

        }

        /// <summary>
        /// Draw a body joint for a hand and assigns a specific color based on the handstate
        /// </summary>
        /// <param name="joint">Joint representing a hand</param>
        /// <param name="handState">State of the hand</param>
        private void DrawHandJoint(Joint joint, HandState handState, double radius, double borderWidth, SolidColorBrush border)
        {
            switch (handState)
            {
                //case HandState.Lasso:
                //    DrawJoint(joint, radius, Brushes.Cyan, borderWidth, border);
                //    break;
                //case HandState.Open:
                //    DrawJoint(joint, radius, Brushes.Green, borderWidth, border);
                //    break;
                case HandState.Closed:
                    DrawJoint(joint, radius, Brushes.Red, borderWidth, border);
                    break;
                default:
                    //DrawJoint(joint, radius, Brushes.Yellow, borderWidth, border);
                    break;
            }
        }
        #endregion FRAME PROCESSING

        #region UI Methods
        private void OnToggleCamera(object sender, RoutedEventArgs e)
        {
            ChangeVisualMode("Camera");
        }

        private void OnToggleDepth(object sender, RoutedEventArgs e)
        {
            ChangeVisualMode("Depth");
        }

        private void OnToggleInfrared(object sender, RoutedEventArgs e)
        {
            ChangeVisualMode("Infrared");
        }

        private void OnToggleTrack(object sender, RoutedEventArgs e)
        {
            trackingBarPath = true;
            testWindow = new Window();
            myCanvas = new Canvas();
            myCanvas.Background = Brushes.Transparent;
            testWindow.Title = "Canvas Sample";
            testWindow.Content = myCanvas;
            testWindow.Show();
        }

        /// <summary>
        /// Change the UI based on the mode
        /// </summary>
        /// <param name="mode">New UI mode</param>
        private void ChangeVisualMode(string mode)
        {
            // Invis all
            CameraGrid.Visibility = Visibility.Collapsed;
            DepthGrid.Visibility = Visibility.Collapsed;
            InfraredGrid.Visibility = Visibility.Collapsed;

            switch (mode)
            {
                case "Camera":
                    CameraGrid.Visibility = Visibility.Visible;
                    break;
                case "Depth":
                    DepthGrid.Visibility = Visibility.Visible;
                    break;
                case "Infrared":
                    InfraredGrid.Visibility = Visibility.Visible;
                    break;
            }
        }
        #endregion UI Methods
    }
}

