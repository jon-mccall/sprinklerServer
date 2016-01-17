using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SprinklerWebServer
{
    public enum RunInterval
    {
        OddDays,
        EvenDays,
        EveryDay,
        SpecificDaysOfWeek
    }

    // Summary:
    //     Specifies the day of the week.
    public enum MyDayOfWeek
    {
        //
        // Summary:
        //     Indicates Sunday.
        Sunday = 0,
        //
        // Summary:
        //     Indicates Monday.
        Monday = 1,
        //
        // Summary:
        //     Indicates Tuesday.
        Tuesday = 2,
        //
        // Summary:
        //     Indicates Wednesday.
        Wednesday = 3,
        //
        // Summary:
        //     Indicates Thursday.
        Thursday = 4,
        //
        // Summary:
        //     Indicates Friday.
        Friday = 5,
        //
        // Summary:
        //     Indicates Saturday.
        Saturday = 6
    }

    [DataContract]
    public sealed class SprinklerProgram
    {
        [DataMember]
        public int Id { get; set; }

        private string name;
        private bool isEnabled;

        /// <summary>
        /// which days to run this program on
        /// </summary>
        private RunInterval programRunSpec = RunInterval.EveryDay;
        /// <summary>
        /// Which days of week to run, if RunInterval is set to SpecificDaysOfWeek
        /// </summary>
        [DataMember]
        public IList<MyDayOfWeek> ProgramRunDays {get; set; }

        /// <summary>
        /// start hour of the day (24 hour time)
        /// </summary>
        private int startHour;
        /// <summary>
        /// start minute from the StartHour
        /// </summary>
        private int startMinute;

        /// <summary>
        /// zone run times in minutes
        /// </summary>
        [DataMember]
        public IList<int> ZoneTimes { get; set; }

        public SprinklerProgram()
        {
            ProgramRunDays = new List<MyDayOfWeek>();
            ZoneTimes = new List<int>();
            isEnabled = true;

            DateTime test = DateTime.Now.AddSeconds(70);
            StartHour = test.Hour;
            StartMinute = test.Minute;
            for (int i = 0; i < 14; i++)
            {
                ZoneTimes.Add(2);
            }
        }

        

        [DataMember]
        public RunInterval ProgramRunSpec
        {
            get
            {
                return programRunSpec;
            }

            set
            {
                programRunSpec = value;
            }
        }


        [DataMember]
        public int StartHour
        {
            get
            {
                return startHour;
            }

            set
            {
                startHour = value;
            }
        }

        [DataMember]
        public int StartMinute
        {
            get
            {
                return startMinute;
            }

            set
            {
                startMinute = value;
            }
        }

        [DataMember]
        public string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
            }
        }

        [DataMember]
        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }

            set
            {
                isEnabled = value;
            }
        }
    }
}
