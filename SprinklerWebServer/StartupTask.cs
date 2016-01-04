using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace SprinklerWebServer
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral serviceDeferral;
        AppServiceConnection appServiceConnection;
        SprinklerValveController valveController;
        SprinklerProgramController programController;
        //TemperatureSensors tempSensors;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            //if (!_started)
            //{
            //    tempSensors = new TemperatureSensors();
            //    tempSensors.InitSensors();
            //    _started = true;
            //}
            
            // Associate a cancellation handler with the background task. 
            taskInstance.Canceled += TaskInstance_Canceled;

            // Get the deferral object from the task instance
            serviceDeferral = taskInstance.GetDeferral();

            var appService = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            if (appService != null && appService.Name == "App2AppComService")
            {
                appServiceConnection = appService.AppServiceConnection;
                appServiceConnection.RequestReceived += AppServiceConnection_RequestReceived; ;
            }

            // just run init, maybe don't need the above...
            Initialize();
        }

        private async void AppServiceConnection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var message = args.Request.Message;
            string command = message["Command"] as string;

            switch (command)
            {
                case "Initialize":
                    {
                        //Sensors.InitSensors();
                        //Devices.InitDevices();

                        var messageDeferral = args.GetDeferral();
                        Initialize();
                        ////Define a new instance of our HTTPServer on Port 8888
                        //HttpServer server = new HttpServer(8888, appServiceConnection);
                        //IAsyncAction asyncAction = Windows.System.Threading.ThreadPool.RunAsync(
                        //    (workItem) =>
                        //    {   //Start the Sever
                        //        server.StartServer();
                        //    });

                        //Set a result to return to the caller
                        var returnMessage = new ValueSet();
                        //Respond back to PoolWebService with a Status of Success 
                        returnMessage.Add("Status", "Success");
                        var responseStatus = await args.Request.SendResponseAsync(returnMessage);
                        messageDeferral.Complete();
                        break;
                    }

                case "Quit":
                    {
                        //Service was asked to quit. Give us service deferral
                        //so platform can terminate the background task
                        serviceDeferral.Complete();
                        break;
                    }
            }
        }

        private void Initialize()
        {
            valveController = new SprinklerValveController();
            programController = new SprinklerProgramController(valveController);

            //Define a new instance of our HTTPServer on Port 8888
            HttpRestServer server = new HttpRestServer(8888, appServiceConnection, valveController, programController);//, tempSensors);
            IAsyncAction asyncAction = Windows.System.Threading.ThreadPool.RunAsync(
                (workItem) =>
                {   //Start the Sever
                                server.StartServer();
                });

            WebServer webServer = new WebServer(8889);
            IAsyncAction asyncAction2 = Windows.System.Threading.ThreadPool.RunAsync(
                (workItem) =>
                {   //Start the Sever
                    webServer.StartServer();
                });


        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            //cleanup as needed
        }
    }
}
