using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Windowed
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string IoTHostName = "stu-hub-iot.azure-devices.net";
        const string deviceKey = "1LC+Jp0TCQyOMTGqumL4XTNysRje1awtlfZyKjv2kWo=";
        private const string deviceId = "motius16";

        private DeviceClient deviceClient;
        private DispatcherTimer timer;

        public MainPage()
        {
            this.InitializeComponent();
            deviceClient = DeviceClient.Create(IoTHostName, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId,deviceKey));
            DHTController dhtController = new DHTController(deviceClient,deviceId,this);
            UltraSonicCamera camera = new UltraSonicCamera(this);
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += dhtController.Post_Values;
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            // Take measurement
            var temp = 5;
            Debug.WriteLine($"Time: {DateTime.Now}, Temperature: {temp} \u00B0C");

            // Update UI
            TemperatureText.Text = $"Temperature: {temp} \u00B0C";

            // Create a datapoint
            var telemetryDataPoint = new
            {
                id = deviceId,
                temperature = temp,
                date = DateTime.Now
            };

            // Format data to a JSON message
            var json = JsonConvert.SerializeObject(telemetryDataPoint);
            var message = new Message(Encoding.ASCII.GetBytes(json));

            // Send message to the IoT Hub
            await deviceClient.SendEventAsync(message);
        }

        public void SetUltraSonicTextView(string ultraSonicText)
        {
            UltraSonicText.Text = ultraSonicText + " cm";
        }

        public void SetTempAndHumidityTextView(string temp, string humidity)
        {
            TemperatureText.Text = $"Temperature: {temp} \u00B0C";
            Humidity_Text.Text = $"Humidity: {humidity} ";
        }

        public Image GetPhotoImage()
        {
            return image;
        }
    }
}
