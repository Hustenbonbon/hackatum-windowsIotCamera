using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GrovePi;
using GrovePi.Sensors;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace Windowed
{
    class DHTController
    {
        private IDHTTemperatureAndHumiditySensor dht =
            DeviceFactory.Build.DHTTemperatureAndHumiditySensor(Pin.DigitalPin7, DHTModel.Dht11);

        private DeviceClient deviceClient;
        private readonly string _deviceId;
        private MainPage mainPageReference;

        public DHTController(DeviceClient deviceClient, string deviceId, MainPage mainPageReference)
        {
            this.deviceClient = deviceClient;
            _deviceId = deviceId;
            this.mainPageReference = mainPageReference;
        }

        public async void Post_Values(object sender, object e)
        {
            // Take measurement
            dht.Measure();
            var temp = dht.TemperatureInCelsius;
            var hum = dht.Humidity;
            Debug.WriteLine("Temp " + temp + " Hum " + hum);
            mainPageReference.SetTempAndHumidityTextView(temp+"", ""+hum);
            // Create a datapoint
            var telemetryDataPoint = new
            {
                id = _deviceId,
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
