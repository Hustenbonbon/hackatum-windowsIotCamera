using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using GrovePi;
using GrovePi.Sensors;

namespace Windowed
{
    class UltraSonicCamera
    {
        enum State
        {
            ULTRASONIC,
            PICTURE
        }

        private State myState = State.ULTRASONIC;

        private MediaCapture mediaCapture;

        private StorageFile photoFile;

        private DispatcherTimer timer;
        private String _sGroveUltrasonicSensor;
        IUltrasonicRangerSensor GroveRanger = DeviceFactory.Build.UltraSonicSensor(Pin.DigitalPin4);
        private ILed led = DeviceFactory.Build.Led(Pin.DigitalPin2);

        CircularBuffer buffer = new CircularBuffer(10);
        private int counter = 0;
        private bool isPictureInited = false;
        private bool IsSomeOneCLose = false;

        private MainPage mainPageReference;

        public UltraSonicCamera(MainPage mainPageReference)
        {
            this.mainPageReference = mainPageReference;
            InitPicture();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.3);
            timer.Tick += UltraSonicCheck;
            timer.Start();
        }



        private void UltraSonicCheck(object sender,object e)
        {
            try
            {
                var tmp = GroveRanger.MeasureInCentimeters();
                buffer.AddNewValue(tmp);
                mainPageReference.SetUltraSonicTextView(buffer.GetMedian()+"");
                if (buffer.GetMedian() < 30)
                {
                    led.ChangeState(SensorStatus.On);
                    IsSomeOneCLose = true;
                    if (isPictureInited)
                    {
                        if (myState == State.ULTRASONIC)
                        {
                            myState = State.PICTURE;
                            Debug.WriteLine("Start taking pictures");
                            takePhoto_Click();
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Trying to take picture, but not inited");
                    }
                    //cameraTimer = new Timer(this.PictureTaker, null, 0, 1000);
                }
                else
                {
                    led.ChangeState(SensorStatus.Off);
                    //cameraTimer = null;
                    myState = State.ULTRASONIC;
                }
                if (buffer.GetMedian() > 150 && IsSomeOneCLose && myState == State.ULTRASONIC)
                {
                    IsSomeOneCLose = false;
                    myState = State.PICTURE;
                    Debug.WriteLine("Taking empty picture");
                    takePhoto_Click();
                }
            }
            catch
            {
                Debug.WriteLine("Error");
            }
        }

        private async void takePhoto_Click()
        {
            try
            {
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                Debug.WriteLine("Image Properties: " + imageProperties);
                IRandomAccessStream photoStream = new InMemoryRandomAccessStream();
                if (mediaCapture != null)
                {
                    await mediaCapture.CapturePhotoToStreamAsync(imageProperties, photoStream);
                }
                else
                {

                    Debug.WriteLine("ERROOR" + mediaCapture);
                }
                Debug.WriteLine("Sending picture");
                /*
                BitmapImage bmp = new BitmapImage();
                bmp.DecodePixelWidth = 1280;
                bmp.SetSource(photoStream);
                Image image = mainPageReference.GetPhotoImage();
                image.Source = bmp;

                /* Wenn Max wieder da ist*/
                byte[] byteArray = await Convert(photoStream);
                HttpClient client = new HttpClient();
                MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new ByteArrayContent(byteArray, 0, byteArray.Length), "file", "file");
                HttpResponseMessage response = await client.PostAsync("http://52.166.143.196:9002/faces/verify", form);
                response.EnsureSuccessStatusCode();
                client.Dispose();
                Debug.WriteLine(response.StatusCode + " " + response.Content);
                /**/

                Debug.WriteLine("Take Photo succeeded: " + photoStream.Size);
                myState = State.ULTRASONIC;

            }
            catch (HttpRequestException re)
            {
                Debug.WriteLine("Wrong response");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Taking picture went wrong with message " + ex.StackTrace);
                Cleanup();
            }
        }

        async Task<byte[]> Convert(IRandomAccessStream s)
        {
            var dr = new DataReader(s.GetInputStreamAt(0));
            var bytes = new byte[s.Size];
            await dr.LoadAsync((uint)s.Size);
            dr.ReadBytes(bytes);
            return bytes;
        }

        async void InitPicture()
        {
            Debug.WriteLine("Initializing camera to capture audio and video...");
            // Use default initialization
            mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync();

            // Set callbacks for failure and recording limit exceeded
            Debug.WriteLine("Device successfully initialized for video recording!");
            //mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
            //mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);
            isPictureInited = true;
        }

        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (myState == State.PICTURE)
                {
                    await mediaCapture.StopPreviewAsync();
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
        }
    }



    class CircularBuffer
    {

        private int index;
        private int[] values;

        public CircularBuffer(int size)
        {
            values = new int[size];
            index = 0;
            //Avoid a problem because of an empty buffer
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = 300;
            }
        }

        public void AddNewValue(int newValue)
        {
            values[index] = newValue;
            index = (index + 1) % values.Length;
        }

        public int GetMedian()
        {
            int[] temp = values.OrderBy(i => i).ToArray();
            return temp[temp.Length / 2];
        }
    }
}
