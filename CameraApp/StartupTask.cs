using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using Windows.ApplicationModel.Background;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;

using GrovePi;
using GrovePi.Sensors;

using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace CameraApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        enum State
        {
            ULTRASONIC,
            PICTURE
        }

        private State myState = State.ULTRASONIC;

        private MediaCapture mediaCapture;

        private StorageFile photoFile;

        private Timer peroidicTimer;
        private Timer cameraTimer;
        private String _sGroveUltrasonicSensor;
        IUltrasonicRangerSensor GroveRanger = DeviceFactory.Build.UltraSonicSensor(Pin.DigitalPin4);
        private ILed led = DeviceFactory.Build.Led(Pin.DigitalPin2);

        CircularBuffer buffer = new CircularBuffer(20);
        private int counter = 0;
        private bool isPictureInited = false;

        

        public void Run(IBackgroundTaskInstance taskInstance)
        {

            InitPicture();
            peroidicTimer = new Timer(this.UltraSonicCheck, null, 0, 500);
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();



        }

        private void UltraSonicCheck(object sender)
        {

            if (myState == State.PICTURE)
            {
                return;
            }
            try
            {
                var tmp = GroveRanger.MeasureInCentimeters();
                buffer.AddNewValue(tmp);

                if (buffer.GetMedian() < 20 && cameraTimer == null)
                {
                    led.ChangeState(SensorStatus.On);
                    myState = State.PICTURE;
                    Debug.WriteLine("Start taking pictures");
                    PictureTaker();
                    //cameraTimer = new Timer(this.PictureTaker, null, 0, 1000);
                }
                else
                {
                    led.ChangeState(SensorStatus.Off);
                    //cameraTimer = null;
                    myState = State.ULTRASONIC;
                }
            }
            catch
            {
                Debug.WriteLine("Error");
            }
        }

        void PictureTaker()
        {
            if (isPictureInited)
            {
                takePhoto_Click();
            }
            else
            {
                Debug.WriteLine("Trying to take picture, but not inited");
            }
        }

        private async void takePhoto_Click()
        {
            try
            {

                //photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                //    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                //Debug.WriteLine("Photo File: " + photoFile);
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

                //WriteableBitmap bmp = new WriteableBitmap(300,169);


                //Debug.WriteLine("Bitmap Start" + bmp);

                //bmp.SetSource(photoStream);
                Debug.WriteLine("Sending picture");
                byte[] byteArray = await Convert(photoStream);

                HttpClient client = new HttpClient();
                MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new ByteArrayContent(byteArray, 0, byteArray.Length), "file", "file");
                HttpResponseMessage response = await client.PostAsync("http://192.168.0.113:5000/faces/verify", form);
                response.EnsureSuccessStatusCode();
                client.Dispose();
                Debug.WriteLine(response.StatusCode + " " +response.Content);


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
            index = (index + 1)%values.Length;
        }

        public int GetMedian()
        {
            int[] temp = values.OrderBy(i => i).ToArray();
            return temp[temp.Length/2];
        }
    }
}
