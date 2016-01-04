using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;

namespace SprinklerWebServer
{
    public sealed class SprinklerValveController
    {
        Dictionary<int, MyPins> mapZoneToPinNo = new Dictionary<int, MyPins>();
        Dictionary<int, GpioPin> mapZoneToPin = new Dictionary<int, GpioPin>();

        bool[] zoneOffOnList = new bool[16];

        enum MyPins
        {
            //Led0 = 0,
            //Led1 = 1,
            //Led2 = 2,
            //Led3 = 3,
           /// Led4 = 4,
            Led6 = 6,
            Led5 = 5,
            Led25 = 25,
            Led24 = 24,
            Led23 = 23,
            Led26 = 26,
            Led18 = 18,
            Led17 = 17,
            Led12 = 12,
            Led13 = 13,
            Led19 = 19,
            Led27 = 27,
            Led16 = 16,
            Led20 = 20,
            Led21 = 21,

            Led7 = 7,
            //Led9 = 9,
            //Led10 = 10,
            //Led14 = 14,
            //Led15 = 15,
            // these work but not currently used
            Led22 = 22
        }

        public SprinklerValveController()
        {
            InitGPIO();
        }

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                System.Diagnostics.Debug.WriteLine( "There is no GPIO controller on this device.");
                return;
            }

            InitPins();
            //pin = gpio.OpenPin(LED_PIN);
            //pinValue = GpioPinValue.High;
            //pin.Write(pinValue);
            //pin.SetDriveMode(GpioPinDriveMode.Output);

            System.Diagnostics.Debug.WriteLine( "GPIO pin initialized correctly.");

        }

        private bool InitPins()
        {
            //SpiConnectionSettings(0)

            MyPins[] pins = (MyPins[])Enum.GetValues(typeof(MyPins));
            var gpio = GpioController.GetDefault();

            mapZoneToPinNo.Add(1, MyPins.Led6);
            mapZoneToPinNo.Add(2, MyPins.Led5);
            mapZoneToPinNo.Add(3, MyPins.Led25);
            mapZoneToPinNo.Add(4, MyPins.Led24);
            mapZoneToPinNo.Add(5, MyPins.Led23);
            mapZoneToPinNo.Add(6, MyPins.Led22);
            mapZoneToPinNo.Add(7, MyPins.Led18);
            mapZoneToPinNo.Add(8, MyPins.Led17);
            mapZoneToPinNo.Add(9, MyPins.Led12);
            mapZoneToPinNo.Add(10,MyPins.Led13);
            mapZoneToPinNo.Add(11, MyPins.Led19);
            mapZoneToPinNo.Add(12, MyPins.Led26);
            mapZoneToPinNo.Add(13, MyPins.Led16);
            mapZoneToPinNo.Add(14, MyPins.Led20);
            mapZoneToPinNo.Add(15, MyPins.Led21);


            foreach( int zone in mapZoneToPinNo.Keys)
            {
                MyPins id = mapZoneToPinNo[zone];
                try
                {
                    GpioPin pin = gpio.OpenPin((int)id);

                    GpioPinValue pinValue = GpioPinValue.High;
                    pin.Write(pinValue);
                    pin.SetDriveMode(GpioPinDriveMode.Output);

                    mapZoneToPin.Add(zone, pin);

                    zoneOffOnList[zone - 1] = false;
                    
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unable to open pin: " + id);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);

                }

            }

            return true;
        }

        /// <summary>
        /// return turn a list of bools with all zone off/on status.  In order by zone.
        /// </summary>
        /// <returns></returns>
        public IList<bool> QueryAllZonesStatusAsArray()
        {
            List<bool> list = new List<bool>(zoneOffOnList);
            return list;
        }

        /// <summary>
        /// is the zone on/off?  return true if on, false if off
        /// </summary>
        /// <param name="zone"></param>
        /// <returns></returns>
        public bool QueryZoneStatus(int zone)
        {
            try { 
                GpioPin pin = mapZoneToPin[zone];
                GpioPinValue value = pin.Read();
                return (value == GpioPinValue.Low);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in SetZoneOn zone=" + zone);
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }

        }

        /// <summary>
        /// set zone on (1 based)
        /// </summary>
        /// <param name="zone"></param>
        /// <returns></returns>
        public bool SetZoneOn(int zone)
        {
            try {
                GpioPin pin = mapZoneToPin[zone];
                pin.Write(GpioPinValue.Low);
                zoneOffOnList[zone - 1] = true;
                return true;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in SetZoneOn zone=" + zone);
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        /// <summary>
        /// set zone off (1 based)
        /// </summary>
        /// <param name="zone"></param>
        /// <returns></returns>
        public bool SetZoneOff(int zone)
        {
            GpioPin pin = mapZoneToPin[zone];
            pin.Write(GpioPinValue.High);
            zoneOffOnList[zone - 1] = false;

            return true;
        }

    }
}
