//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DepthBasics
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Net.Sockets;
    using System.Text;
    using System.Net;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 



    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        private DepthImagePixel[] SavedStatePixels;

        private int[] SavedState;
        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] colorPixels;
        

        //array of masks. 0 - nuber of mask, 1,2 - x,y coordinates, value - number of zone (if 0 not in zone)
     //   private short[,,] CascadeOfMask;

        private Mask[] CascadeMask;
        private SortedList<int, Point> MinimalCoords;

        private int curMaskNum = 0;
        // array of calculated sum of mask zones.  0 - mask number, 1 - zone index;
      //  private int[,] SummOfZones;

      

        private int minDepth = 0;
        private int maxDepth = 0;

        
        private bool running = true;
        private bool showMask = false;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }
            
            if (null != this.sensor)
            {
                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                
                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space for saving current state of depth map
                this.SavedStatePixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];


                //Allocate Space to put snapshot of Depth Map
                this.SavedState = new int[307200];

                // Allocate space to put the mask pixels
                //this.maskPixels = new short[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                
                this.CascadeMask= new Mask[5];
                
                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.Image.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

                this.showMask = new bool();
                showMask = false;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if ((depthFrame != null) && (running))
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);
                    minDepth = depthFrame.MinDepth;
                    maxDepth = depthFrame.MaxDepth;

                    // Get the min and max reliable depth for the current frame
                    WritePixelsOnBitmap();

                  //   this.colorBitmap.WritePixels(
                 //      new Int32Rect(100, 100, this.colorBitmap.PixelWidth / 2, this.colorBitmap.PixelHeight/2),
                 //      this.maskPixels,
                 //      (this.colorBitmap.PixelWidth * sizeof(int)),
                 //      640*100);
                 //   maskSum(this.maskPixels);
                }
            }
        }

        private void WritePixelsOnBitmap()
        {                    
            // Convert the depth to RGB
            int colorPixelIndex = 0;
            
            int xStart=0, yStart=0;
            if (CascadeMask[curMaskNum] != null)
            {
               xStart  = CascadeMask[curMaskNum].xStart;
               yStart  = CascadeMask[curMaskNum].yStart;
            
            }
            
            //int maskPixelIndex = 0;
            for (int a = 0; a < 480; a++)//for (int i = 0; i < this.depthPixels.Length; ++i)
            {
                for (int b = 0; b < 640; b++)
                {
                    int i = b + a * 640;
                    // Get the depth for this pixel
                    short depth = depthPixels[i].Depth;                   
                    this.SavedState[i] = this.depthPixels[i].Depth;
                    
                    // To convert to a byte, we're discarding the most-significant
                    // rather than least-significant bits.
                    // We're preserving detail, although the intensity will "wrap."
                    // Values outside the reliable depth range are mapped to 0 (black).

                    // Note: Using conditionals in this loop could degrade performance.
                    // Consider using a lookup table instead when writing production code.
                    // See the KinectDepthViewer class used by the KinectExplorer sample
                    // for a lookup table example.
                    byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);



                    if (CascadeMask[curMaskNum] != null && CascadeMask[curMaskNum].Visible && a >= yStart && a < yStart + CascadeMask[curMaskNum].Haight && b >= xStart && b < xStart + CascadeMask[curMaskNum].Width)
                    {
                        int y = a - yStart;
                        int x = b - xStart;
                        int sw = this.CascadeMask[curMaskNum].Matrix[y, x] % 3;

                        switch (sw)
                        {
                            case 1:
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = 255;
                                break;
                            case 2:
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = 255;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                break;
                            case 0:
                                this.colorPixels[colorPixelIndex++] = 255;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                break;
                            default:
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                this.colorPixels[colorPixelIndex++] = intensity;
                                break;
                        }
                    }
                    else // if No Visible Masks
                    {
                        // Write out blue byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels[colorPixelIndex++] = intensity;
                    }

                        //this.maskPixels[maskPixelIndex++] = depth;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                }
            }
            this.colorBitmap.WritePixels(
                       new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                       this.colorPixels,
                       this.colorBitmap.PixelWidth * sizeof(int),
                       0);

        }


       
        private int[,] vectorToAr( int inpHeight, int inpWidth)
        {
            int counter = 0;
            int[,] a = new int[inpHeight, inpWidth];
            for (int i = 0; i < inpHeight; i++ )
            {
                for (int j = 0; j < inpWidth; j++)
                {
                    a[i, j] = this.SavedState[counter++];
                }

            }
           // textBox1.Text += a[130, 130].ToString();
            return a;
        }


        private void printArray(short[,] a, int inpHeight, int inpWidth)
        {
            for (int i = 0; i < inpHeight; i++)
            {
                for (int j = 0; j < inpWidth; j++)
                {
                    this.textBox1.Text += a[i, j].ToString() + ";";
                }
                this.textBox1.Text += "\n";
            }
        }

        private void printSumOfMask(int mNumber, int zNumber)
        {
          //  WritePixelsOnBitmap();
          
            this.textBox1.Text += "\n Mask nmbr: " + mNumber + ", Zone nmbr: " + zNumber + " = " + this.CascadeMask[mNumber].ZoneSum[zNumber].ToString();

        }


        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ButtonScreenshotClick(object sender, RoutedEventArgs e)
        {
            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.ConnectDeviceFirst;
                return;
            }

            // create a png bitmap encoder which knows how to save a .png file
            BitmapEncoder encoder = new PngBitmapEncoder();

            // create frame from the writable bitmap and add to encoder
            encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

            string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

            string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            string path = Path.Combine(myPhotos, "KinectSnapshot-" + time + ".png");

            // write the new file to disk
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                this.statusBarText.Text = string.Format("{0} {1}", Properties.Resources.ScreenshotWriteSuccess, path);
            }
            catch (IOException)
            {
                this.statusBarText.Text = string.Format("{0} {1}", Properties.Resources.ScreenshotWriteFailed, path);
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            //print summ
            curMaskNum = int.Parse(comboBox1.Text);
            CascadeMask[curMaskNum].calcSum(vectorToAr(480,640),0,0);
            for (int i = 0; i < CascadeMask[curMaskNum].ZoneSum.Length; i++)
            {
                printSumOfMask(curMaskNum, i);
            }
        }
         
        private void button3_Click(object sender, RoutedEventArgs e)
        {  

            initMasks();
         
        }

     

        private void button4_Click(object sender, RoutedEventArgs e)
        {   //stop/start button
            if (running) { running = false; }
            else { running = true; }

            textBox1.Text = this.SavedState[25000].ToString();
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            //Show Mask button
            if (this.CascadeMask[curMaskNum].Visible)
            {
                
                curMaskNum = int.Parse(comboBox1.Text);
                this.CascadeMask[curMaskNum].Visible = false;
                button5.Content = "Show Mask";
                
            }
            else
            {
                curMaskNum = int.Parse(comboBox1.Text);
                this.CascadeMask[curMaskNum].Visible = true;
                button5.Content = "Hide Mask";
            }

        }

        
        private void Image_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Point p = new Point();
           // p = e.GetPosition(IInputElement;
        }

     

        public void initMasks()
        {
            CascadeMask[0] = new Mask(300,200,4);
            CascadeMask[0].drawSquare(100, 200, 0, 0, 1);
            CascadeMask[0].drawSquare(100, 200, 100, 0, 2);
            CascadeMask[0].drawSquare(100, 200, 200, 0, 3);
            CascadeMask[0].calcPrimeSum(vectorToAr(480, 640));
            this.textBox1.Text += "PrimeSum " + CascadeMask[0].PrimeZoneSum[1];

            CascadeMask[1] = new Mask(300, 200, 4);
            CascadeMask[1].drawSquare(300, 66, 0, 0, 1);
            CascadeMask[1].drawSquare(300, 67, 0, 66, 2);
            CascadeMask[1].drawSquare(300, 67, 0, 133, 3);
            CascadeMask[1].calcPrimeSum(vectorToAr(480, 640));
            


            //public void drawSquare( int mHeight, int mWidth, int hStart = 0, int wStart = 0, short zoneNumber = 0)
    
            CascadeMask[2] = new Mask(300, 200, 7);
            CascadeMask[2].drawSquare(100, 100, 0, 0, 1);
            CascadeMask[2].drawSquare(100, 100, 100, 0, 2);
            CascadeMask[2].drawSquare(100, 100, 200, 0, 3);
            CascadeMask[2].drawSquare(100, 100, 0, 100, 6);
            CascadeMask[2].drawSquare(100, 100, 100, 100, 4);
            CascadeMask[2].drawSquare(100, 100, 200, 100, 5);
            CascadeMask[2].calcPrimeSum(vectorToAr(480, 640));

            int mH =240; //div by 3
            int mW =160; //div by 2
            // create mask with 12 zones
            

            CascadeMask[3] = new Mask(mH, mW, 13);
            CascadeMask[3].drawSquare(mH/3, mW/4, 0, 0, 1);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, mH/3, 0, 3);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, (mH / 3)*2, 0, 2);

            CascadeMask[3].drawSquare(mH / 3, mW / 4, 0, mW/4, 5);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, mH / 3, mW / 4, 4);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, (mH / 3) * 2, mW / 4, 6);

            CascadeMask[3].drawSquare(mH / 3, mW / 4, 0, mW / 2, 7);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, mH / 3, mW / 2, 9);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, (mH / 3) * 2, mW / 2, 8);

            CascadeMask[3].drawSquare(mH / 3, mW / 4, 0, (mW / 4 )*3, 11);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, mH / 3, (mW / 4) * 3, 10);
            CascadeMask[3].drawSquare(mH / 3, mW / 4, (mH / 3) * 2, (mW / 4) * 3, 12);
            CascadeMask[3].calcPrimeSum(vectorToAr(480, 640));

            this.textBox1.Text += "\n Mask Coords: x " + CascadeMask[3].xStart + " y" + CascadeMask[3].yStart ;


        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            FindObject();
        }

        private void FindObject()
        {
           curMaskNum = int.Parse(comboBox1.Text);
           int usMask = curMaskNum;

          int[,] frame = vectorToAr(480,640);
          int maskH = 300;
          int maskW = 200;
          int[,] diffs = new int[48,64];

          for (int i = 0; i < 480 - maskH; i = i + 10)
          {
              for (int j = 0; j < 640 - maskW; j = j + 10)
              {
                  CascadeMask[usMask].calcSum(frame, i, j);
                  diffs[i/10,j/10] = CascadeMask[usMask].findDiff();  
              }
          }

          Point p = new Point();
          MinimalCoords = new SortedList<int, Point>();
          

            //find plase with minimal diff
          int minX=0, minY=0, minValue;
          minValue=diffs[0,0];
          for (int i = 0; i < (480 - maskH)/10; i++)
          {
              for (int j = 0; j < (640 - maskW) / 10; j++)
              {
                  if (diffs[i, j] < minValue)
                  {
                      MinimalCoords.Add(diffs[i, j],new Point(i, j));
                      minY = i;
                      minX = j;
                      minValue = diffs[i, j];
                  }
              }
          }
             


          minX *= 10;
          minY *= 10;

          this.textBox1.Text += "\n Object: x" + minX + " y " + minY;
          CascadeMask[usMask].xStart = minX;
          CascadeMask[usMask].yStart = minY;
          
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            textBox1.Text = "No Object Found";
            /*//send info
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //привязываем сокет к порту
            socket.Bind(new IPEndPoint(IPAddress.Any, 8080));
            //стартуем 
            socket.Listen(0);
            //бесконечный цикл приема коннектов клиентов
            //while (true){
            //ждем пока приконектится клиент
                Socket s = socket.Accept();                
                //отправлем ответ клиенту
                
                s.Send(Encoding.ASCII.GetBytes("Mnum"+ curMaskNum + "x" + CascadeMask[curMaskNum].xStart + "y" + CascadeMask[curMaskNum].yStart ));
                s.Close();
          // }
             */
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            
            Point p = new Point();
            MinimalCoords.TryGetValue(int.Parse(textBox2.Text), out p);

        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            //textBox1.Text = " ";
            sensor.ElevationAngle = sensor.ElevationAngle - 10;
        }

        private void button9_Click(object sender, RoutedEventArgs e)
        {
            sensor.ElevationAngle = sensor.ElevationAngle + 10;
        }

        
       
    }
}