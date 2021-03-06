﻿using SprinklerWebServer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace SprinklerWebServer
{
    public sealed class SprinklerProgramController
    {
        private const string ZoneListFileName = "ZoneList.json";
        public SprinklerData Data { get; set; }
        public SprinklerProgram RunningProgram { get; set; }
        public int RunningZone { get; set; }
        private DateTime ZoneStopTime { get; set; }
        private bool isPaused;
        private DateTime ZonePauseStopTime { get; set; }
        private int zonePauseSecondsLeft = 0;

        private int zoneRunSecondsLeft = 0;
        private SprinklerValveController Controller;
        private readonly int ZONE_SWITCH_DELAY_MS = 5000;
        //public IList<Zone> ZoneList
        //{
        //    get; private set;
        //}

        public SprinklerProgramController(SprinklerValveController controller)
        {
            Utils.LogLine("------------------------");
            Utils.LogLine("Sprinkler program started");
            Controller = controller;
            Data = SprinklerData.Load();
            if(Data == null)
            {
                // first time set the default
                Data = new SprinklerData();
                Data.SetDefaults();
            }
            //// TODO remove test!!!!
            //if(Data.Programs.Count == 1)
            //    Data.AddProgram("Program2");


            // load programs
            RunningZone = -1;
            ZoneStopTime = DateTime.MinValue;
            isPaused = false;
            ZonePauseStopTime = DateTime.MaxValue;

            // start controller timer
            IAsyncAction asyncAction2 = Windows.System.Threading.ThreadPool.RunAsync(
            (workItem) =>
            {
                ControllerThread();
            });

            //if (Utils.LocalFileExists(ZoneListFileName))
            //{
            //    string json = Utils.ReadStringFromLocalFile(ZoneListFileName);
            //    if (json != null && json.Length > 0)
            //    {
            //        ZoneList = Utils.DeserializeJsonZoneList(json);
            //    }
            //}

            //if (ZoneList == null || ZoneList.Count == 0)
            //{
            //    // hard code zone list /names if the data file does not exist
            //    ZoneList = new List<Zone>();
            //    for (int i = 1; i < 16; i++)
            //    {
            //        ZoneList.Add(new Zone() { Id = i, IsEnabled = true, Name = String.Format("Zone {0}", i) });
            //    }
            //}

        }

        /// <summary>
        /// main sprinkler controller thread, starts programs and advances sets
        /// </summary>
        private async void ControllerThread()
        {
            // wait for a bit on startup before running out thread
            Task.Delay(5000).Wait();
            System.Diagnostics.Debug.WriteLine("SprinklerProgramController started");

            while (true)
            {
                if(RunningProgram == null)
                {
                    //check if we need to start a program
                    RunningProgram = StartProgramIfTime();
                }

                if(RunningProgram != null)
                {
                    if(isPaused)
                    {
                        // check if paused time is up
                        if(DateTime.Now > ZonePauseStopTime)
                        {
                            isPaused = false;
                            ZonePauseStopTime = DateTime.MaxValue;
                            // restart the stopped zone...
                            Controller.SetZoneOn(RunningZone);
                        }
                        // just figure out how much longer it needs to wait
                        TimeSpan diff = ZonePauseStopTime - DateTime.Now;
                        zonePauseSecondsLeft = (int)diff.TotalSeconds;
                    }

                    // Advance zone as needed
                    if (!isPaused)
                    {
                        StartZoneIfTime();
                    }
                }

                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Check for time to start a program running
        /// </summary>
        /// <returns></returns>
        private SprinklerProgram StartProgramIfTime()
        {
            DateTime now = DateTime.Now;
            foreach (SprinklerProgram prog in Data.Programs)
            {
                if (!prog.IsEnabled)
                    continue;

                bool runDay = false;
                switch (prog.ProgramRunSpec)
                {
                    case RunInterval.OddDays:
                        if (now.Day % 2 != 0)
                        {
                            runDay = true;
                        }
                        break;
                    case RunInterval.EvenDays:
                        if( now.Day % 2 == 0)
                        {
                            runDay = true;
                        }
                        break;
                    case RunInterval.EveryDay:
                        runDay = true;
                        break;
                    case RunInterval.SpecificDaysOfWeek:
                        if(prog.ProgramRunDays.Contains((MyDayOfWeek)(int)now.DayOfWeek))
                        {
                            runDay = true;
                        }
                        break;
                    default:
                        break;
                }
                if (runDay)
                {
                    // check if the hour and minute match (and not over 5 seconds into that minute)
                    if (now.Hour == prog.StartHour && now.Minute == prog.StartMinute && now.Second < 5)
                    {
                        RunningZone = -1;
                        zoneRunSecondsLeft = 0;
                        return prog;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Check for time to start the next zone running
        /// </summary>
        public void StartZoneIfTime()
        {
            if(RunningZone == -1)
            {
                // no zones running, start first with time
                for (int index = 0; index < RunningProgram.ZoneTimes.Count(); index++)
                {
                    if(RunningProgram.ZoneTimes[index] > 0)
                    {
                        // start this one
                        System.Diagnostics.Debug.WriteLine("StartZoneIfTime starting first zone");
                        SetRunningZone(index + 1);
                        return;
                    }
                }
            }
            else
            {
                if( DateTime.Now > ZoneStopTime )
                {
                    // find next zone to start
                    for (int index = RunningZone; index < RunningProgram.ZoneTimes.Count(); index++)
                    {
                        if (RunningProgram.ZoneTimes[index] > 0)
                        {
                            // start this one
                            System.Diagnostics.Debug.WriteLine("StartZoneIfTime starting NEXT zone");
                            SetRunningZone(index + 1);
                            return;
                        }
                    }

                    // if we get here we are done running the current program
                    Controller.SetZoneOff(RunningZone);
                    RunningZone = -1;
                    zoneRunSecondsLeft = 0;
                    RunningProgram = null;
                }
                else
                {
                    // just figure out how much longer it needs to run
                    TimeSpan diff = ZoneStopTime - DateTime.Now;
                    zoneRunSecondsLeft = (int)diff.TotalSeconds;
                }
            }
        }

        private void SetRunningZone(int zone)
        {
            // turn off current zone if its running
            if(RunningZone >= 0)
            {
                Controller.SetZoneOff(RunningZone);
                Task.Delay(ZONE_SWITCH_DELAY_MS).Wait();
            }
            // turn on this zone
            Controller.SetZoneOn(zone);
            int minutes = RunningProgram.ZoneTimes[zone - 1];
            RunningZone = zone;
            ZoneStopTime = DateTime.Now.AddMinutes(minutes);
            zoneRunSecondsLeft = minutes * 60;
            System.Diagnostics.Debug.WriteLine("Running Zone " + zone + ", stop time " + ZoneStopTime + " in " + zoneRunSecondsLeft + " seconds");
        }

        /// <summary>
        /// start a program running
        /// </summary>
        /// <param name="index"></param>
        public void StartProgram(int index)
        {
            if(RunningProgram == null )
            {
                SprinklerProgram prog = Data.Programs.FirstOrDefault(x => x.Id == index);
                if (prog != null)
                {
                    RunningZone = -1;
                    zoneRunSecondsLeft = 0;
                    zonePauseSecondsLeft = 0;
                    isPaused = false;
                    RunningProgram = prog;
                }
            }
        }

        /// <summary>
        ///  stop the program currently running
        /// </summary>
        /// <param name="index"></param>
        public void StopProgram()
        {
            if (RunningProgram != null)
            {
                if (RunningZone > -1)
                {
                    // if we get here we are done running the current program
                    Controller.SetZoneOff(RunningZone);
                    RunningZone = -1;
                }
                zoneRunSecondsLeft = 0;
                zonePauseSecondsLeft = 0;
                isPaused = false;
                RunningProgram = null;
            }
        }

        /// <summary>
        /// stop current zone and start next
        /// </summary>
        public void RunNextZone()
        {
            if (RunningProgram != null)
            {
                // find next zone to start
                for (int index = RunningZone; index < RunningProgram.ZoneTimes.Count(); index++)
                {
                    if (RunningProgram.ZoneTimes[index] > 0)
                    {
                        // start this one
                        System.Diagnostics.Debug.WriteLine("RunNextZone starting NEXT zone");
                        SetRunningZone(index + 1);
                        return;
                    }
                }

                // if we get here we are done running the current program
                Controller.SetZoneOff(RunningZone);
                RunningZone = -1;
                zoneRunSecondsLeft = 0;
                RunningProgram = null;
            }
        }

        /// <summary>
        /// pause the running program for a specified number of minutes
        /// </summary>
        /// <param name="index"></param>
        /// <param name="minutes"></param>
        public void PauseProgram(int minutes)
        {
            if (RunningProgram != null)
            {
                if (RunningZone > -1)
                {
                    // stop current zone
                    Controller.SetZoneOff(RunningZone);
                }
                isPaused = true;
                zoneRunSecondsLeft += minutes * 60;
                ZoneStopTime = ZoneStopTime.AddMinutes(minutes);
                zonePauseSecondsLeft = minutes * 60;
                ZonePauseStopTime = DateTime.Now.AddMinutes(minutes);
            }

        }

        public void UnpauseProgram()
        {
            if(RunningProgram != null && isPaused)
            {
                isPaused = false;
                ZoneStopTime = ZoneStopTime.AddSeconds(-zonePauseSecondsLeft);
                if(RunningZone > -1)
                {
                    Controller.SetZoneOn(RunningZone);
                }
            }
        }

        public int ZoneRunSecondsLeft
        {
            get
            {
                return zoneRunSecondsLeft;
            }

            set
            {
                zoneRunSecondsLeft = value;
            }
        }

        public int ZonePauseSecondsLeft
        {
            get
            {
                return zonePauseSecondsLeft;
            }

            set
            {
                zonePauseSecondsLeft = value;
            }
        }

        public bool IsPaused
        {
            get
            {
                return isPaused;
            }

            set
            {
                isPaused = value;
            }
        }

        internal void SetSprinklerData(SprinklerData data)
        {
            // replace current Program Data
            Data.Programs = new List<SprinklerProgram>(data.Programs);

            // save to disk
            Data.Save();
        }



        public void SetZoneList(IList<Zone> newList)
        {
            // replace current list
            Data.ZoneList = newList;
            //Data.ZoneList.Clear();
            //((List<Zone>)Data.ZoneList).AddRange(newList);

            // save to disk
            Data.Save();
        }
    }
}
