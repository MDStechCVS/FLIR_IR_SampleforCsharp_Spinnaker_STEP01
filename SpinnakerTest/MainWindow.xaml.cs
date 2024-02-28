using SpinnakerNET;
using SpinnakerNET.GenApi;
using SpinnakerNET.GUI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SpinnakerNET.GUI.WPFControls;
using System.Threading;
using System.Drawing;
using Pen = System.Drawing.Pen;
using System.Collections.Generic;

namespace SpinnakerTest
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        #region PRIVATE

        PropertyGridControl gridControl;

        Thread thMainView = null;

        CameraSelectionWindow camSelControl;

        // Min, Max icon
        MeasureSpotValue maxSpot;
        MeasureSpotValue minSpot;

        // ROI 
        MeasureBoxValue roiBox;

        // 카메라 해상도 
        private const int int640480 = 640 * 480;
        private const int int464348 = 464 * 348;
        private const int int320256 = 320 * 256;
        private const int int320240 = 320 * 240;

        private int stIntCamFrameArray = int320256;
        private int mCurWidth = 320;
        private int mCurHeight = 256;
   
        private bool bProcessing = false;

        // Offset Value  
        private const float mOffsetVal_001 = 0.01f;
        private const float mOffsetVal_01 = 0.1f;
        private const float mOffsetVal_004 = 0.04f;
        private const float mOffsetVal_04 = 0.4f;

        private float mConvertOffsetVal = mOffsetVal_001;

        private double minBox = 0;
        private double maxBox = 0;

        private Bitmap bmp = null;
        private int step = 256 / 4; // 총 4개의 간격으로 나눔

        private IManagedCamera connectcam = null; // 연결 된 카메라 객체 
        private string CamDevice = null;  //  연결 된 카메라 기종 

        // 카메라 온도 Range 설정 
        private int TempRangeVal = 0;
        private List<Int16> RangeIndexData = new List<Int16>();

        /// <summary>
        /// Data 추출 Thread 구동 여부
        /// </summary>
        private bool isRunning = false;

        #endregion

        #region PUBLIC
        public class MeasureSpotValue
        {
            int mPointIdx;
            ushort mTempValue;

            Pen mPen = new Pen(System.Drawing.Color.AliceBlue);

            public MeasureSpotValue(System.Drawing.Color cl)
            {
                mPen.Color = cl;
            }

            public void SetXY(Graphics gr, int nX, int nY)
            {
                gr.DrawLine(mPen, nX - 10, nY, nX + 10, nY);  // 수평
                gr.DrawLine(mPen, nX, nY - 10, nX, nY + 10);  // 수직
            }

            public void SetPointIndex(int nIndex)
            {
                mPointIdx = nIndex;
            }

            public int GetPointIndex()
            {
                return mPointIdx;
            }

            public void SetTempVal(ushort usTempValue)
            {
                mTempValue = usTempValue;
            }
        }

        // 측정 영역 Box
        public class MeasureBoxValue
        {
            int mX;
            int mY;
            int mWidth;
            int mHeight;
            int mPointIdx;
            ushort mTempValue;
            bool mIsVisible = false;

            // Box 영역 내의 최대 최소 위치
            int mMax_X;
            int mMax_Y;
            int mMin_X;
            int mMin_Y;

            // Box 영역 내의 최대, 최소 온도값
            ushort mMax = 0;
            ushort mMin = 65535;

            Pen mPen = new Pen(System.Drawing.Color.AliceBlue);
            Pen mPenMax = new Pen(System.Drawing.Color.Red);
            Pen mPenMin = new Pen(System.Drawing.Color.Blue);

            public MeasureBoxValue(System.Drawing.Color cl, int nX, int nY, int nWidth, int nHeight)
            {
                mPen.Color = cl;

                mX = nX;
                mY = nY;
                mWidth = nWidth;
                mHeight = nHeight;
            }

            public void ResetMinMax()
            {
                mMax_X = 0;
                mMax_Y = 0;
                mMin_X = 0;
                mMin_Y = 0;

                mMax = 0;
                mMin = 65535;
            }

            public void SetXYWH(Graphics gr)
            {
                gr.DrawRectangle(mPen, mX, mY, mWidth, mHeight);  // Box
            }

            public void SetMax(Graphics gr)
            {
                gr.DrawRectangle(mPenMax, mMax_X - 3, mMax_Y - 3, 6, 6);  // Box Max
            }

            public void SetMin(Graphics gr)
            {
                gr.DrawRectangle(mPenMin, mMin_X - 3, mMin_Y - 3, 6, 6);  // Box Min
            }

            public void GetMinMax(out ushort usMin, out ushort usMax)
            {
                usMin = mMin;
                usMax = mMax;
            }

            public void SetPointIndex(int nIndex)
            {
                mPointIdx = nIndex;
            }

            public int GetPointIndex()
            {
                return mPointIdx;
            }

            public void SetTempVal(ushort usTempValue)
            {
                mTempValue = usTempValue;
            }

            public bool GetIsVisible()
            {
                return mIsVisible;
            }

            public void SetIsVisible(bool bVal)
            {
                mIsVisible = bVal;
            }

            public bool CheckXYinBox(int nX, int nY, ushort tempVal)
            {
                bool rValue = false;

                if ((mX <= nX) && ((mX + mWidth) >= nX))   // X 좌표가 범위 내에 있는지
                {
                    if ((mY <= nY) && ((mY + mHeight) >= nY))   // Y 좌표가 범위 내에 있는지
                    {
                        rValue = true;

                        // 최대 최소 온도 체크 후 백업
                        if (mMin >= tempVal)
                        {
                            mMin = tempVal;
                            mMin_X = nX;
                            mMin_Y = nY;
                        }
                        else if (mMax < tempVal)
                        {
                            mMax = tempVal;
                            mMax_X = nX;
                            mMax_Y = nY;
                        }
                    }
                }
                return rValue;
            }
        }
        #endregion

        #region STEP1 - 00. START / END 
        public MainWindow()
        {
            InitializeComponent();

            gridControl = new PropertyGridControl();
            Grid.SetRow(gridControl, 1);

            maxSpot = new MeasureSpotValue(System.Drawing.Color.White);
            minSpot = new MeasureSpotValue(System.Drawing.Color.Yellow);

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            
            if (connectcam != null)
            {
                connectcam.Dispose();
            }

            if (thMainView != null)
            {
                thMainView.Abort();
            }
        }
        #endregion

        #region STEP1 - 01. CAMERA SELECT / EVENT
        private void ConnectSpinnaker_Click(object sender, RoutedEventArgs e)
        {
            // Spinnaker 연결 창 전시 
            camSelControl = new CameraSelectionWindow();
            camSelControl.Width = 550;
            camSelControl.Height = 300;

            // Spinnaker 연결 창 클릭 이벤트 등록 
            camSelControl.OnDeviceClicked += ConnectControls;

            // Spinnaker 연결 창 전시 
            camSelControl.ShowModal(true);
        }

        void ConnectControls(object sender, CameraSelectionWindow.DeviceEventArgs args)
        {
            // Check whether an Interface is selected
            if (args.IsCamera == false && args.Interface != null)
            {
                ResetWindowControl(); // Disconnect previous device
            }
            // Check whether a System is selected
            else if (args.IsSystem == true && args.System != null)
            {
                ResetWindowControl(); // Disconnect previous device
            }
            else // Connect a camera (Previous device was interface || Camera is first device connected)
            {
                SetCamera(args.Camera, false, args.Interface);
                connectcam = args.Camera;
            }

            camSelControl.Close();
        }

        private void ResetWindowControl()
        {
            try
            {
                if (connectcam == null)
                {
                    return;
                }

                // Disconnect controls
                CameraDisconnect(connectcam);
                
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
        }
        #endregion

        #region STEP1 - 02. CAMERA CONNECT
        /// <summary>
        /// Connect ImageDrawingControl and PropertyGridControl with IManagedCamera
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="startStreaming">Boolean indicating whether to start streaming</param>
        /// <param name="parentInterface">Parent interface </param>
        void SetCamera(IManagedCamera cam, bool startStreaming = false, IManagedInterface parentInterface = null)
        {
            try
            {
                cam.Init();
               
                // 카메라 연결 
                camPlayOne(cam);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("There was a problem connecting to IManagedCamera.\n{0}", ex.Message));
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        void camPlayOne(IManagedCamera cam)
        {
            try
            {
                // 카메라 파라미터 설정 
                ConfigurationCam(cam);

                // Begin acquiring images
                cam.BeginAcquisition();

                isRunning = true;

                Console.Write("\tDevice {0} ", 0);

                Thread.Sleep(1000);

                thMainView = new Thread(() => threadProc(cam));
                thMainView.Start();

            }
            catch (SpinnakerException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        void ConfigurationCam(IManagedCamera cam)
        {
            try
            {
                INodeMap nodeMap = cam.GetNodeMap();

                StringReg iModelName = nodeMap.GetNode<StringReg>("DeviceModelName");
                string modelname = iModelName.ToString();

                // 연결된 카메라의 기종에 따라 Width, Height, parameter 값 설정 
                if (modelname.Contains("AX5")) // Ax5
                {
                    stIntCamFrameArray = int320256;
                    mCurWidth = 320;
                    mCurHeight = 256;

                    bmp = new Bitmap(mCurWidth, mCurHeight);

                    // pixelformat 설정 
                    IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
                    if (iPixelFormat != null && iPixelFormat.IsWritable)
                    {
                        IEnumEntry iPixelFormatMono14 = iPixelFormat.GetEntryByName("Mono14");
                        iPixelFormat.Value = iPixelFormatMono14.Value;
                        Console.WriteLine("iPixelFormatMono14 : " + nodeMap.GetNode<IEnum>("PixelFormat").ToString());
                    }

                    // digital output 설정 
                    IEnum iDigitalOutput = nodeMap.GetNode<IEnum>("DigitalOutput");
                    if (iDigitalOutput != null && iDigitalOutput.IsWritable)
                    {
                        IEnumEntry iDigitalOutput14bit = iDigitalOutput.GetEntryByName("bit14bit");
                        iDigitalOutput.Value = iDigitalOutput14bit.Value;
                        Console.WriteLine("iDigitalOutput14bit : " + nodeMap.GetNode<IEnum>("DigitalOutput").ToString());
                    }

                    // TemperatureLinearMode 설정 - Offset 값 설정 
                    IEnum iTemperatureLinearMode = nodeMap.GetNode<IEnum>("TemperatureLinearMode");
                    if (iTemperatureLinearMode != null && iTemperatureLinearMode.IsWritable)
                    {
                        IEnumEntry iTemperatureLinearModeOn = iTemperatureLinearMode.GetEntryByName("On");
                        iTemperatureLinearMode.Value = iTemperatureLinearModeOn.Value;

                        IEnum iTemperatureLinearResolution = nodeMap.GetNode<IEnum>("TemperatureLinearResolution");
                        if (iTemperatureLinearResolution != null && iTemperatureLinearResolution.IsWritable)
                        {
                            IEnumEntry iTemperatureLinearResolutionHigh = iTemperatureLinearResolution.GetEntryByName("High");
                            iTemperatureLinearResolution.Value = iTemperatureLinearResolutionHigh.Value;
                            mConvertOffsetVal = mOffsetVal_004;

                            Console.WriteLine("iTemperatureLinearModeOn : " + nodeMap.GetNode<IEnum>("TemperatureLinearMode").ToString());
                            Console.WriteLine("iTemperatureLinearResolution : " + nodeMap.GetNode<IEnum>("TemperatureLinearResolution").ToString());
                        }
                    }

                    CamDevice = "AX5";
                }
                else if (modelname.Contains("PT1000")) // FLIR Axx
                {
                    stIntCamFrameArray = int640480;
                    mCurWidth = 640;
                    mCurHeight = 480;

                    bmp = new Bitmap(mCurWidth, mCurHeight);

                    // pixelformat 설정 
                    IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
                    if (iPixelFormat != null && iPixelFormat.IsWritable)
                    {
                        IEnumEntry iPixelFormatMono16 = iPixelFormat.GetEntryByName("Mono16");
                        iPixelFormat.Value = iPixelFormatMono16.Value;
                        Console.WriteLine("iPixelFormatMono16 : " + nodeMap.GetNode<IEnum>("PixelFormat").ToString());
                    }

                    // TemperatureLinearMode 설정 및 offset 값 설정 
                    IEnum iTemperatureLinearMode = nodeMap.GetNode<IEnum>("IRFormat");
                    if (iTemperatureLinearMode != null && iTemperatureLinearMode.IsWritable)
                    {
                        IEnumEntry iTemperatureLinearMode100mk = iTemperatureLinearMode.GetEntryByName("TemperatureLinear100mK");
                        iTemperatureLinearMode.Value = iTemperatureLinearMode100mk.Value;

                        mConvertOffsetVal = mOffsetVal_01;

                        Console.WriteLine("iTemperatureLinearMode 100mk : " + nodeMap.GetNode<IEnum>("IRFormat").ToString());
                    }

                    CamDevice = "PT1000";

                }
                else if (modelname.Contains("A50")) // A50, A500
                {
                    stIntCamFrameArray = int464348;
                    mCurWidth = 464;
                    mCurHeight = 348;

                    bmp = new Bitmap(mCurWidth, mCurHeight);

                    // pixelformat 설정 
                    IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
                    if (iPixelFormat != null && iPixelFormat.IsWritable)
                    {
                        IEnumEntry iPixelFormatMono16 = iPixelFormat.GetEntryByName("Mono16");
                        iPixelFormat.Value = iPixelFormatMono16.Value;
                        Console.WriteLine("iPixelFormatMono16 : " + nodeMap.GetNode<IEnum>("PixelFormat").ToString());
                    }

                    // TemperatureLinearMode 설정 및 Offset 값 설정 
                    IEnum iTemperatureLinearMode = nodeMap.GetNode<IEnum>("IRFormat");
                    if (iTemperatureLinearMode != null && iTemperatureLinearMode.IsWritable)
                    {
                        IEnumEntry iTemperatureLinearMode100mk = iTemperatureLinearMode.GetEntryByName("TemperatureLinear100mK");
                        iTemperatureLinearMode.Value = iTemperatureLinearMode100mk.Value;

                        mConvertOffsetVal = mOffsetVal_01;

                        Console.WriteLine("iTemperatureLinearMode 100mk : " + nodeMap.GetNode<IEnum>("IRFormat").ToString());
                    }

                    CamDevice = "A50";
                }
                else if (modelname.Contains("A70")) // A70, A700
                {
                    stIntCamFrameArray = int640480;
                    mCurWidth = 640;
                    mCurHeight = 480;

                    bmp = new Bitmap(mCurWidth, mCurHeight);

                    // PixelFormat 설정 
                    IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
                    if (iPixelFormat != null && iPixelFormat.IsWritable)
                    {
                        IEnumEntry iPixelFormatMono16 = iPixelFormat.GetEntryByName("Mono16");
                        iPixelFormat.Value = iPixelFormatMono16.Value;
                        Console.WriteLine("iPixelFormatMono16 : " + nodeMap.GetNode<IEnum>("PixelFormat").ToString());
                    }

                    // TemperatureLinearMode 설정 및 Offset값 설정 
                    IEnum iTemperatureLinearMode = nodeMap.GetNode<IEnum>("IRFormat");
                    if (iTemperatureLinearMode != null && iTemperatureLinearMode.IsWritable)
                    {
                        IEnumEntry iTemperatureLinearMode100mk = iTemperatureLinearMode.GetEntryByName("TemperatureLinear100mK");
                        iTemperatureLinearMode.Value = iTemperatureLinearMode100mk.Value;

                        mConvertOffsetVal = mOffsetVal_01;

                        Console.WriteLine("iTemperatureLinearMode 100mk : " + nodeMap.GetNode<IEnum>("IRFormat").ToString());
                    }

                    CamDevice = "A70";
                }
                else if (modelname.Contains("A400")) // A400
                {
                    stIntCamFrameArray = int320240;
                    mCurWidth = 320;
                    mCurHeight = 240;

                    bmp = new Bitmap(mCurWidth, mCurHeight);

                    // PixelFormat 설정 
                    IEnum iPixelFormat = nodeMap.GetNode<IEnum>("PixelFormat");
                    if (iPixelFormat != null && iPixelFormat.IsWritable)
                    {
                        IEnumEntry iPixelFormatMono16 = iPixelFormat.GetEntryByName("Mono16");
                        iPixelFormat.Value = iPixelFormatMono16.Value;
                        Console.WriteLine("iPixelFormatMono16 : " + nodeMap.GetNode<IEnum>("PixelFormat").ToString());
                    }

                    // TemperatureLinearMode 설정 및 Offset 값 설정 
                    IEnum iTemperatureLinearMode = nodeMap.GetNode<IEnum>("IRFormat");
                    if (iTemperatureLinearMode != null && iTemperatureLinearMode.IsWritable)
                    {
                        IEnumEntry iTemperatureLinearMode100mk = iTemperatureLinearMode.GetEntryByName("TemperatureLinear100mK");
                        iTemperatureLinearMode.Value = iTemperatureLinearMode100mk.Value;

                        mConvertOffsetVal = mOffsetVal_01;

                        Console.WriteLine("iTemperatureLinearMode 100mk : " + nodeMap.GetNode<IEnum>("IRFormat").ToString());
                    }

                    CamDevice = "A400";

                }
                
                // 카메라 별 측정 온도 값 구성 및 설정 
                TempRangeConf(nodeMap);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }
        
        private void CameraDisconnect(IManagedCamera cam)
        {
            try
            {
                isRunning = false;
                if (cam.IsStreaming())
                {
                    cam.EndAcquisition();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        private void DisConnectSpinnaker_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (connectcam == null)
                {
                    return;
                }
                CameraDisconnect(connectcam);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }
        #endregion

        #region STEP1 - 03. CAMERA DATA RECEIVE / SHOW RESULT
        /// <summary>
        /// MdsSdkControl 데이터 Receiver delegate function
        /// </summary>
        /// <param name="data">수신 데이터</param>
        /// <param name="w">width</param>
        /// <param name="h">height</param>
        /// <param name="minval">minimum value</param>
        /// <param name="maxval">maximum value</param>
        delegate void DelegateCtrlData_Receiver(UInt16[] data, int w, int h, ushort minval, ushort maxval);
        /// <summary>
        /// MdsSdkControl 데이터 Receiver
        /// 열화상 카메라로 부터 받은 데이터로 화면을 구성
        /// </summary>
        /// <param name="data">수신 데이터</param>
        /// <param name="w">width</param>
        /// <param name="h">height</param>
        /// <param name="minval">minimum value</param>
        /// <param name="maxval">maximum value</param>
        void CtrlData_Receiver(UInt16[] data, int w, int h, ushort minval, ushort maxval)
        {
            if (data == null)
                return;

            lock (this)
            {
                //SetImage를 수행중이면 리턴.(화면 갱신 skip)
                if (bProcessing)
                {
                    return;
                }
            }

            if (!this.CheckAccess())
            {
                this.Dispatcher.Invoke(new DelegateCtrlData_Receiver(CtrlData_Receiver), new object[] { data, w, h, minval, maxval });
                return;
            }

            try
            {
                lock (this)
                {
                    bProcessing = true;
                }

                System.Drawing.Color col;

                IntPtr hBitmap = IntPtr.Zero;

                //x 는 image의 width
                //y 는 image의 hediht
                int x, y;

                // Box 내 영역의 최대 최소 온도값 초기화
                if (roiBox != null && roiBox.GetIsVisible())
                {
                    roiBox.ResetMinMax();
                }

                // Rainbow colors
                for (int a = 0; a < data.Length; a++)
                {
                    getXY(a, mCurWidth, out x, out y);

                    int tempdiff = maxval - minval;
                    if (tempdiff == 0)
                    {
                        tempdiff = 1;
                    }

                    int rVal = (int)((data[a] - minval) * 255 / tempdiff);

                    if (rVal < step) //Blue to Cyan
                    {
                        col = Color.FromArgb(0, rVal * 4, 255);
                    }
                    else if (rVal < step * 2) //Cyan to Green
                    {
                        col = Color.FromArgb(0, 255, 255 - (rVal - step) * 4);
                    }
                    else if (rVal < step * 3) //Green to Yellow
                    {
                        col = Color.FromArgb((rVal - step * 2) * 4, 255, 0);
                    }
                    else //Yellow to Red
                    {
                        col = Color.FromArgb(255, 255 - (rVal - step * 3) * 4, 0);
                    }
                   
                    bmp.SetPixel(x, y, col);

                    // Box 내 영역의 최대 최소 온도값 체크
                    if (roiBox != null && roiBox.GetIsVisible())
                    {
                        roiBox.CheckXYinBox(x, y, data[a]);
                    }
                }



                Graphics gr = Graphics.FromImage(bmp);

                int maxX = 0;
                int maxY = 0;
                int minX = 0;
                int minY = 0;

                // max spot get x, y;
                getXY(maxSpot.GetPointIndex(), mCurWidth, out maxX, out maxY);
                getXY(minSpot.GetPointIndex(), mCurWidth, out minX, out minY);

                maxSpot.SetXY(gr, maxX, maxY);
                minSpot.SetXY(gr, minY, minY);

                // ROI Box
                if (roiBox != null && roiBox.GetIsVisible())
                {
                    roiBox.SetXYWH(gr);
                    roiBox.SetMax(gr);
                    roiBox.SetMin(gr);

                    ushort usMin = 0;
                    ushort usMax = 0;

                    roiBox.GetMinMax(out usMin, out usMax);

                    minBox = (((float)(usMin) * mConvertOffsetVal) - 273.15f);
                    maxBox = (((float)(usMax) * mConvertOffsetVal) - 273.15f);

                }


                // Bitmap is ready - update image control
                hBitmap = bmp.GetHbitmap();
                BitmapSource bmpSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                if (bmpSrc.CanFreeze)
                    bmpSrc.Freeze();

                this.backgroundImageBrush.ImageSource = bmpSrc;

                DeleteObject(hBitmap);

            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("[CtrlEvent] " + e.ToString());
            }
            lock (this)
                bProcessing = false;

            return;
        }


        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);



        void threadProc(IManagedCamera cam)
        {
            while (true)
            {
                if (((Thread.CurrentThread.ThreadState & ThreadState.SuspendRequested) == ThreadState.SuspendRequested) ||
                ((Thread.CurrentThread.ThreadState & ThreadState.Suspended) == ThreadState.Suspended))
                {
                    break;
                }

                try
                {
                    if (!isRunning)
                    {
                        return; 
                    }

                    if (cam.IsValid() != true && cam != null)
                    {
                        Console.WriteLine("cam is not valid");
                        break;
                    }

                    // Retrieve next received image and ensure image completion
                    using (IManagedImage rawImage = cam.GetNextImage())
                    {
                        if (rawImage.IsIncomplete)
                        {
                            Console.WriteLine("Image incomplete with status {0}...", rawImage.ImageStatus);
                            rawImage.Release();

                            Thread.Sleep(10);
                        }
                        else
                        {
                            // 최고, 최저 온도 값을 섭씨 온도로 계산하여 저장  
                            double minValue = 0;
                            double maxValue = 0;

                            int uint16Count = 0;
                            ushort max16 = 0;
                            ushort min16 = 65535;

                            UInt16[] imgArray = new UInt16[stIntCamFrameArray];

                            for (int a = 0; a < stIntCamFrameArray * 2; a += 2)
                            {
                                if (a >= rawImage.ManagedData.Length)
                                {
                                    // 온도 값이 들어오지 않는 경우 / 카메라 변경 시 예외처리 
                                    return;
                                }

                                ushort sample = BitConverter.ToUInt16(rawImage.ManagedData, a);

                                // 최고, 최저 온도 값 계산 및 좌표 계산 
                                if (min16 >= sample)
                                {
                                    minValue = ((float)(sample) * mConvertOffsetVal) - 273.15;
                                    min16 = sample;
                                    minSpot.SetPointIndex(a / 2);
                                    minSpot.SetTempVal(sample);

                                }
                                else if (max16 < sample)
                                {
                                    maxValue = ((float)(sample) * mConvertOffsetVal) - 273.15;
                                    max16 = sample;
                                    maxSpot.SetPointIndex(a / 2);
                                    maxSpot.SetTempVal(sample);

                                }

                                imgArray[a/2] = sample;
                                uint16Count++;

                            }

                            CtrlData_Receiver(imgArray, mCurWidth, mCurHeight, min16, max16);

                            CompositionTarget_Rendering(minValue, maxValue, minBox, maxBox);
                            rawImage.Release();
                        }
                    }
                }
                catch (ThreadInterruptedException e)
                {
                    Console.WriteLine(e);
                    break;
                }
                catch (SpinnakerException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                }

                Thread.Sleep(1);

            }

        }

        // Point index에서 X, Y, 좌표를 알아낸다.
        private void getXY(int sourceIndex, int sourceWidth, out int x, out int y)
        {
            y = sourceIndex / sourceWidth;
            x = sourceIndex % sourceWidth;
        }

        private void SetROIBox_Click(object sender, RoutedEventArgs e)
        {
            // 측정 영역 박스 좌표, 크기 설정
            roiBox = new MeasureBoxValue(System.Drawing.Color.Yellow, 100, 100, 100, 100);
            roiBox.SetIsVisible(true);

        }
        
        delegate void DelegateCompositionTarget_Rendering(double minval, double maxval, double measurePoint, double measurePoint2);
        void CompositionTarget_Rendering(double minval, double maxval, double roiminval, double roimaxval)
        {
            if (!this.CheckAccess())
            {
                this.Dispatcher.Invoke(new DelegateCompositionTarget_Rendering(CompositionTarget_Rendering), new object[] { minval, maxval, roiminval, roimaxval });
                return;
            }

            // 전체 화면 온도 값 표시 
            MinTemp.Content = string.Format("{0:F1}", minval);
            MaxTemp.Content = string.Format("{0:F1}", maxval);

            // ROI 영역 온도 값 표시 
            ROIMinTemp.Content = string.Format("{0:F1}", roiminval);
            ROIMaxTemp.Content = string.Format("{0:F1}", roimaxval);

        }
        #endregion

        #region STEP1 - 04. CAMERA TEMPERATURE RANGE CONFIGURATION 
        private void TempRangeConf(INodeMap nodeMap)
        {
            try
            {
                // 카메라가 연결되지 않은 경우 
                if (CamDevice == null) 
                {
                    Console.WriteLine("No Connected Camera!");
                    return;
                }

                // 온도 range 항목 제거  
                comboRanges.Items.Clear();

                string[] retValue = null;
                
                // Ax5
                if (CamDevice.Contains("AX5"))
                {
                    IEnum SGM = nodeMap.GetNode<IEnum>("SensorGainMode");

                    if (SGM != null)
                    {
                        EnumEntry[] ee = SGM.Entries;

                        int countValue = ee.Length;

                        retValue = new string[countValue];

                        for (int a = 0; a < countValue; a++)
                        {
                            retValue[a] = ee[a].DisplayName;
                            RangeIndexData.Add((short)a); 
                            Console.WriteLine("GainMode[" + a + "] : " + retValue[a]);
                        }
                    }
                }
                else
                {
                    int numCases = (int)nodeMap.GetNode<Integer>("NumCases");
                    IInteger QC = nodeMap.GetNode<IInteger>("QueryCase");
                    IInteger CC = nodeMap.GetNode<IInteger>("CurrentCase");
                    Float QCLL = nodeMap.GetNode<Float>("QueryCaseLowLimit");
                    Float QCHL = nodeMap.GetNode<Float>("QueryCaseHighLimit");
                    BoolNode QCE = nodeMap.GetNode<BoolNode>("QueryCaseEnabled");

                    double lo, hi;
                    long i;
                    bool enabled;
                    retValue = new string[numCases];
                    int index = 0; 
                    for (i = 0; i < numCases; i++)
                    {
                        // Set case selector                        
                        QC.Value = i;

                        lo = QCLL.Value;
                        hi = QCHL.Value;
                        enabled = QCE.Value;

                        if (enabled)
                        {
                            string TempRange = string.Format(" {0}°C ~ {1}°C ", (lo - 273.15f).ToString("F0"), (hi - 273.15f).ToString("F0"));

                            //retValue는 온도 범위 저장 
                            retValue[index] = TempRange;
                            index++; 

                            // RangeIndexData에는 CurrentCase 값이 저장 - ex) 1, 2, 6, 7 ..등과 같은 
                            RangeIndexData.Add((short)i);
                        }
                    }
                    // 기본 설정은 0번 인덱스에 저장된 온도 범위로 설정
                    CC.Value = RangeIndexData[0]; 
                }

                for (int j = 0; j < retValue.Length; j++)
                {
                    // 온도 범위를 ComboBoxitem으로 추가 
                    comboRanges.Items.Add(retValue[j]);
                }

                comboRanges.SelectedIndex = 0; 
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TempRangeConf]: {0}", ex.Message);
            }
        }


        /// <summary>
        /// 카메라 온도 범위 설정 변경 시 
        /// </summary>
        private void comboRanges_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComboBox combobox = sender as ComboBox;
                if (connectcam == null)
                {
                    return;
                }


                if (!CamDevice.Contains("AX5"))
                {
                    INodeMap nodeMap = connectcam.GetNodeMap();

                    IInteger QueryCase = nodeMap.GetNode<IInteger>("QueryCase");
                    IInteger CurrentCase = nodeMap.GetNode<IInteger>("CurrentCase");
                    IBool bQueryCaseEnabled = nodeMap.GetNode<IBool>("QueryCaseEnabled");
                    IFloat dQueryCaseLowLimit = nodeMap.GetNode<IFloat>("QueryCaseLowLimit");
                    IFloat dQueryCaseHighLimit = nodeMap.GetNode<IFloat>("QueryCaseHighLimit");
                    double dLow = 0, dHigh = 0;

                    TempRangeVal = RangeIndexData[combobox.SelectedIndex];
                    QueryCase.Value = TempRangeVal;

                    if (bQueryCaseEnabled.Value == true)
                    {
                        if (QueryCase != null)
                        {
                            dLow = dQueryCaseLowLimit.Value;
                            dHigh = dQueryCaseHighLimit.Value;
                            CurrentCase.Value = RangeIndexData[combobox.SelectedIndex];
                        }
                    }
                }
                else
                {
                    INodeMap nodeMap = connectcam.GetNodeMap();
                    IEnum SGM = nodeMap.GetNode<IEnum>("SensorGainMode");

                    if (SGM != null)
                    {
                        SGM.Value = RangeIndexData[combobox.SelectedIndex];

                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TempRangeConf]: {0}", ex.Message);
            }
        }

        #endregion

       
    }
}
