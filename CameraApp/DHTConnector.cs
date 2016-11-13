using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using GrovePi;
using GrovePi.Sensors;
using Newtonsoft.Json;



namespace CameraApp
{
    public sealed class DHTConnector
    {
        const string IoTHostName = "stu-hub-iot.azure-devices.net";
        const string deviceKey = "A0rPXAYibVgFDr/x239HU4SposxD0Z0TLG83qXJA0Zg=";
        private const string deviceId = "motius16";


        private DeviceClient deviceClient;

        IDHTTemperatureAndHumiditySensor dht = DeviceFactory.Build.DHTTemperatureAndHumiditySensor(Pin.DigitalPin3, DHTModel.Dht11);

        private Timer timer;

        public DHTConnector()
        {
            deviceClient = DeviceClient.Create(IoTHostName, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId,deviceKey));
            timer = new Timer(this.Timer_Tick,null,0, TimeSpan.FromMinutes(1).Milliseconds);
        }
        private async void Timer_Tick(object sender)
        {
            // Take measurement
            var temp = dht.TemperatureInCelsius;
            var hum = dht.Humidity;
           Debug.WriteLine("DHT sagt: " + temp + "°C und " + hum + "%Feuchtigkeit.");
            // Create a datapoint
            var telemetryDataPoint = new
            {
                id = deviceId,
                temperature = temp,
                humdity = hum,
                date = DateTime.Now
            };

            // Format data to a JSON message
            var json = JsonConvert.SerializeObject(telemetryDataPoint);
            var message = new Message(Encoding.ASCII.GetBytes(json));

            // Send message to the IoT Hub
            await deviceClient.SendEventAsync(message);
        }

    }
}
