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
        public IList<SprinklerProgram> Programs { get; set; }
        public SprinklerProgram RunningProgram { get; set; }
        public int RunningZone { get; set; }
        private DateTime ZoneStopTime { get; set; }
        private bool isPaused;
        private DateTime ZonePauseStopTime { get; set; }
        private int zonePauseSecondsLeft = 0;

        private int zoneRunSecondsLeft = 0;
        private SprinklerValveController Controller;
        private readonly int ZONE_SWITCH_DELAY_MS = 5000;

        public SprinklerProgramController(SprinklerValveController controller)
        {
            Controller = controller;

            // load programs
            Programs = new List<SprinklerProgram>();
            RunningZone = -1;
            ZoneStopTime = DateTime.MinValue;
            isPaused = false;
            ZonePauseStopTime = DateTime.MaxValue;

            Programs.Add(new SprinklerProgram() { Name = "Program1" });

            // start controller timer
            IAsyncAction asyncAction2 = Windows.System.Threading.ThreadPool.RunAsync(
            (workItem) =>
            {
                ControllerThread();
            });

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
            foreach (SprinklerProgram prog in Programs)
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
            if(RunningProgram == null && index < Programs.Count)
            {
                RunningZone = -1;
                zoneRunSecondsLeft = 0;
                zonePauseSecondsLeft = 0;
                isPaused = false;
                RunningProgram = Programs[index];
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
                zoneRunSecondsLeft += minutes * 60;
                ZoneStopTime = ZoneStopTime.AddMinutes(minutes);
                zonePauseSecondsLeft = minutes * 60;
                ZonePauseStopTime = DateTime.Now.AddMinutes(minutes);
                isPaused = true;
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
    }
}
