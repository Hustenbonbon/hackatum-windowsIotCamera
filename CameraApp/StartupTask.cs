using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;


using GrovePi;
using GrovePi.Sensors;

using System.Threading;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace CameraApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        enum State
        {
            ULTRASONIC,
            PICTURE,
            WAITING_FOR_RESPONSE
        }

        private State myState = State.ULTRASONIC;

        private MediaCapture mediaCapture;

        private StorageFile photoFile;

        private const string PHOTO_FILE_NAME = "photo.jpg";

        private Timer peroidicTimer;
        private Timer cameraTimer;
        private String SGroveUltrasonicSensor;
        IUltrasonicRangerSensor GroveRanger = DeviceFactory.Build.UltraSonicSensor(Pin.DigitalPin4);
        private ILed led = DeviceFactory.Build.Led(Pin.DigitalPin2);

        CircularBuffer buffer = new CircularBuffer(20);
        private int counter = 0;
        private bool isPictureInited = false;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            InitPicture();
            peroidicTimer = new Timer(this.TimerCallBack, null, 0, 100);
            cameraTimer = null;
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();
        }
        private void TimerCallBack(object state)
        {
            UltraSonicCheck();
        }

        private void UltraSonicCheck()
        {
            try
            {
                counter++;
                var tmp = GroveRanger.MeasureInCentimeters();
                buffer.AddNewValue(tmp);
                SGroveUltrasonicSensor = "Distance: " + tmp.ToString() + "cm";
                if (counter % 10 == 0)
                {
                    Debug.WriteLine(SGroveUltrasonicSensor + " Median: " + buffer.GetMedian() + " cm");
                }
                counter %= 100;
                if (buffer.GetMedian() < 20)
                {
                    led.ChangeState(SensorStatus.On);
                    myState = State.PICTURE;
                    cameraTimer = new Timer(this.PictureTaker, null, 0, 1000);
                }
                else
                {
                    led.ChangeState(SensorStatus.Off);
                    cameraTimer = null;
                    myState = State.ULTRASONIC;
                }
            }
            catch (Exception /*ex*/)
            {

            }
            
        }

        void PictureTaker(object state)
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

                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                Debug.WriteLine("Take Photo succeeded: " + photoFile.Path);
                
            }
            catch (Exception ex)
            {
                Cleanup();
            }
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
