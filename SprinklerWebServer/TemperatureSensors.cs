using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace SprinklerWebServer
{
    public static class TemperatureSensors
    {
        private static I2cDevice Device;
        private static Timer periodicTimer;
        //How often to read temperature data from the Arduino Mini Pro
        private static int ReadInterval = 4000;  //4000 = 4 seconds

        //Variables to hold temperature data
        private static string insideTemperature = "--.--";
        private static string outsideTemperature = "--.--";

        //Property to expose the Temperature Data
        public static string InsideTemperature
        {
            get
            {   //Lock the variable incase the timer is tring to write to it
                lock (insideTemperature)
                {
                    return insideTemperature;
                }
            }

            set
            {   //Lock the variable incase the HTTP Server is tring to read from it
                lock (insideTemperature)
                {
                    insideTemperature = value;
                }
            }
        }


        //Property to expose the Temperature Data
        public static string OutsideTemperature
        {
            get
            {   //Lock the variable incase the timer is tring to write to it
                lock (outsideTemperature)
                {
                    return outsideTemperature;
                }
            }

            set
            {   //Lock the variable incase the HTTP Server is tring to read from it
                lock (outsideTemperature)
                {
                    outsideTemperature = value;
                }
            }
        }

        //public TemperatureSensors()
        //{
        //    //InitSensors();
        //}

        //Initilizes the I2C connection and starts the timer to read I2C Data
        public static async void InitSensors()
        {
            if (Device == null)
            {
                //Set up the I2C connection the Arduino
                var settings = new I2cConnectionSettings(0x40); // Arduino address
                settings.BusSpeed = I2cBusSpeed.StandardMode;
                string aqs = I2cDevice.GetDeviceSelector("I2C1");
                var dis = await DeviceInformation.FindAllAsync(aqs);
                Device = await I2cDevice.FromIdAsync(dis[0].Id, settings);

                //Create a timer to periodicly read the temps from the Arduino
                periodicTimer = new Timer(TimerCallback, null, 0, ReadInterval);
            }
            return;
        }

        //Handle the time call back
        private static void TimerCallback(object state)
        {
            byte[] ReadBuf = new byte[24];
            //Read the I2C connection
            try
            {
                Device.Read(ReadBuf); // read the data
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            //Parse the response
            //Data is in the format "88.99|78.12|100.00" where "PoolTemp|SolarTemp|OutsideTemp"
            char[] cArray = System.Text.Encoding.UTF8.GetString(ReadBuf, 0, 23).ToCharArray();  // Converte  Byte to Char
            String c = new String(cArray).Trim();
            System.Diagnostics.Debug.WriteLine("temp sense string: " + c);
            string[] data = c.Split('|');

            //Write the data to temperature variables
            try
            {
                if (data[0].Trim() != "")
                    InsideTemperature = data[0];
                if (data[1].Trim() != "")
                    OutsideTemperature = data[1];
            }
            catch (Exception) { }
        }


    }
}
