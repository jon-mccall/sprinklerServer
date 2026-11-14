using SprinklerWebServer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SprinklerWebServer
{
    //Class to define the HTTP WebServer
    public sealed class HttpRestServer : IDisposable
    {
        private bool inited = false;
        private readonly object initializationLock = new object();
        //Create a buffer to read HTTP data
        private const uint BufferSize = 8192;
        //Port to listen on
        private int port = 8888;
        //Listener to
        private readonly StreamSocketListener listener;
        //Connection to send status information back to PoolControllerWebService
        private AppServiceConnection appServiceConnection;

        SprinklerValveController sprinklerController;
        SprinklerProgramController programController;
        //TemperatureSensors tempSensors;

        public HttpRestServer(int serverPort, AppServiceConnection connection, SprinklerValveController sprinkler, SprinklerProgramController program)//, TemperatureSensors temp)
        {
            listener = new StreamSocketListener();
            port = serverPort;
            appServiceConnection = connection;
            sprinklerController = sprinkler;
            programController = program;
            //tempSensors = temp;

            //Add event handler for HTTP connections
            listener.ConnectionReceived += async (s, e) => await ProcessRequestAsync(e.Socket);

            System.Diagnostics.Debug.WriteLine("Sprinkler HTTP REST server starting...");
        }

        //Call to start the listner 
        internal async Task StartServerAsync()
        {
            await listener.BindServiceNameAsync(port.ToString());
            System.Diagnostics.Debug.WriteLine("HTTP REST server bound to port " + port);
        }

        public void Dispose()
        {
            listener.Dispose();
        }


        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            bool initializeSensors = false;
            lock (initializationLock)
            {
                if (!inited)
                {
                    inited = true;
                    initializeSensors = true;
                }
            }

            if (initializeSensors)
            {
                // Initialize once; concurrent first requests must not initialize GPIO twice.
                TemperatureSensors.InitSensors();
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("ProcessRequestAsync called..., BufferSize=" + BufferSize);
                StringBuilder request = new StringBuilder();
                //Get the incomming data
                using (IInputStream input = socket.InputStream)
                {
                    // Read through the complete HTTP header and, for POSTs, its declared body.
                    while (true)
                    {
                        IBuffer readBuffer = await input.ReadAsync(
                            new Windows.Storage.Streams.Buffer(BufferSize),
                            BufferSize,
                            InputStreamOptions.Partial);
                        if (readBuffer.Length == 0)
                            break;

                        byte[] bytesRead = readBuffer.ToArray();
                        request.Append(Encoding.UTF8.GetString(bytesRead, 0, bytesRead.Length));

                        int headerEnd = request.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (headerEnd >= 0)
                        {
                            int contentLength = GetContentLength(request.ToString(), headerEnd);
                            if (request.Length >= headerEnd + 4 + contentLength)
                                break;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine("Finished read " + request.Length + " bytes");
                }


                //Got the data start processing a response
                using (IOutputStream output = socket.OutputStream)
                {
                    string requestMethod = request.ToString();
                    string[] requestParts = { "" };
                    if (!String.IsNullOrWhiteSpace(requestMethod))
                    {
                        //Beakup the first line of the request into it parts
                        requestParts = requestMethod.Split('\n')[0].Split(' ');
                    }
                    //We only respond HTTP GETS and POST methods
                    if (requestParts[0] == "GET" && requestParts.Length > 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Responding to GET");
                        await WriteGetResponseAsync(requestParts[1], output);
                    }
                    else if (requestParts[0] == "POST" && requestParts.Length > 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Responding to POST");
                        // parse out any json
                        int bodyStart = requestMethod.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        string json = bodyStart >= 0 ? requestMethod.Substring(bodyStart + 4) : null;
                        await WritePostResponseAsync(requestParts[1], output, json);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Responding to UNSPORTED " + requestParts[0]);
                        await WriteMethodNotSupportedResponseAsync(requestParts[0], output);
                    }
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("ProcessRequestAsync Exception Caught: " + ex.Message);
            }
            finally
            {
                socket.Dispose();
            }
        }

        private static int GetContentLength(string request, int headerEnd)
        {
            string[] headerLines = request.Substring(0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (string header in headerLines)
            {
                const string contentLengthHeader = "Content-Length:";
                if (header.StartsWith(contentLengthHeader, StringComparison.OrdinalIgnoreCase))
                {
                    int contentLength;
                    if (Int32.TryParse(header.Substring(contentLengthHeader.Length).Trim(), out contentLength) && contentLength >= 0)
                        return contentLength;
                }
            }

            return 0;
        }

        //Handles all HTTP GET's
        private async Task WriteGetResponseAsync(string request, IOutputStream os)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            string responseMsg = "";

            // Browser cache-busting and other query parameters are not part of
            // the REST route (for example, /query/status?_=12345).
            int queryStringStart = request.IndexOf('?');
            if (queryStringStart >= 0)
                request = request.Substring(0, queryStringStart);

            //ZONE/[zone]/running - currently running or not?
            //ZONE/[zone]/timeleft - number of seconds left
            //program/[PROGRAM]/[zone]/runtime  -how many minutes for this program/zone
            //program/[PROGRAM]/[zone]/enabled - is this zone enabled
            //program/[PROGRAM]/starttime - what's the program start time
            //program/[PROGRAM]/progenabled - is this program enabled
            //program/[PROGRAM]/running - currently running or not?
            //query/runningZone  - currently running zone
            //query/status - json object containing datetime, temps, zone on/off list
            ///query/datetime  - current datetime on pi
            //query/temps  - string containing "inside|outside" temps
            //query/zonelist - returns json containing the list of zones
            //query/programdata - returns json containing the list of programs (includes zonelist also!)



            System.Diagnostics.Debug.WriteLine("Get request is: " + request.ToUpper());
            string requestUpper = request.ToUpper();
            // check if request is for sprinkler zone info
            if (requestUpper.StartsWith("/ZONE/"))
            {
                Tuple<int, string> set = ParseZoneCommand(requestUpper);
                if (set != null && set.Item1 > -1)
                {
                    switch (set.Item2)
                    {
                        case "RUNNING": // currently running or not?
                            urlFound = true;
                            bool run = sprinklerController.QueryZoneStatus(set.Item1);
                            responseMsg = run ? "ON" : "OFF";
                            break;
                        case "TIMELEFT": // number of seconds left in current run
                            urlFound = true;
                            responseMsg = "30"; // TODO
                            break;
                    }
                }
            }
            else if (requestUpper.StartsWith("/PROGRAM/"))
            {
                Tuple<int, int, string> info = ParseProgZoneCmd(requestUpper);
                //See if the request it matches any of the valid requests urls and create the response message
                if (info != null)
                {
                    switch (info.Item3)
                    {
                        case "RUNTIME": //how many seconds is this zone set to run
                            urlFound = true;
                            responseMsg = "3600"; // TODO
                            break;
                        case "ENABLED": // is this zone or program enabled
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                        case "STARTTIME": // what is the program starttime
                            urlFound = true;
                            responseMsg = DateTime.Now.ToString(); // TODO
                            break;
                        case "PROGENABLED": // is this program enabled
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                        case "RUNNING": // is this program running
                            urlFound = true;
                            responseMsg = "True"; // TODO
                            break;
                    }
                }
            }
            else if (requestUpper.StartsWith("/QUERY/"))
            {
                string cmd = ParseQueryCmd(requestUpper);
                if(cmd != null)
                {
                    switch(cmd)
                    {
                        case "DATETIME":
                            urlFound = true;
                            responseMsg = DateTime.Now.ToString();
                            break;
                        case "TEMPS":
                            urlFound = true;
                            responseMsg = String.Format("{0}|{1}", TemperatureSensors.InsideTemperature, TemperatureSensors.OutsideTemperature);
                            break;
                        case "STATUS":
                            urlFound = true;
                            responseMsg = MakeStatusJson();
                            System.Diagnostics.Debug.WriteLine("GetResponse for STATUS - made" );
                            break;
                        case "ALLZONESTATUS":
                            urlFound = true;
                            responseMsg = String.Join(",", sprinklerController
                                .QueryAllZonesStatusAsArray()
                                .Select(isOn => isOn ? "ON" : "OFF"));
                            break;
                        case "ZONELIST":
                            urlFound = true;
                            responseMsg = MakeZoneListJson();
                            break;
                        case "LOGS":
                            urlFound = true;
                            responseMsg = ReturnLogFile();
                            break;
                        case "PROGRAMDATA":
                            urlFound = true;
                            responseMsg = MakeProgramSettingsJson();
                            break;

                    }
                }
            }
            bodyArray = Encoding.UTF8.GetBytes(responseMsg);
            await WriteResponseAsync(request.ToUpper(), responseMsg, urlFound, bodyArray, os);
        }

        private string ReturnLogFile()
        {
            try
            {
                return Utils.ReadLogFile();

            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting zone list", ex);
                // swallow throw;
            }
        }

        private string MakeZoneListJson()
        {
            try
            {
                List<Zone> zones = new List<Zone>();
                zones.AddRange(programController.Data.ZoneList);
                string json = Utils.SerializeJSonZoneList(zones);
                return json;
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting zone list", ex);
                // swallow throw;
            }

        }

        private string MakeProgramSettingsJson()
        {
            try
            {
                string json = Utils.SerializeJSonSprinklerData(programController.Data);
                return json;
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting program list", ex);
                // swallow throw;
            }
        }

        private string MakeProgramListJson()
        {
            try
            {
                List<SprinklerProgram> programs = new List<SprinklerProgram>();
                programs.AddRange(programController.Data.Programs);
                string json = Utils.SerializeJSonProgramList(programs);
                return json;
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting program list", ex);
                // swallow throw;
            }
        }

        private string UpdateZoneList(string json)
        {
            try
            {
                List<Zone> zones = Utils.DeserializeJsonZoneList(json);

                programController.SetZoneList(zones);
                return "true";
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error updating zone list", ex);
                // swallow throw;
            }
        }

        private string UpdateProgramData(string json)
        {
            try
            {
                SprinklerData data = Utils.DeserializeJsonSprinklerData(json);

                programController.SetSprinklerData(data);
                return "true";
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error updating zone list", ex);
                // swallow throw;
            }
        }

        private string MakeStatusJson()
        {
            try
            {
                SprinklerStatus model = new SprinklerStatus()
                {
                    //InsideTemp = TemperatureSensors.InsideTemperature,
                    //OutsideTemp = TemperatureSensors.OutsideTemperature,
                    //CurrentTime = DateTime.Now.ToString(),
                    //ZonesOn = sprinklerController.QueryAllZonesStatusAsArray(),
                    //ZoneRunSecondsLeft = programController.ZoneRunSecondsLeft,
                    //IsPaused = programController.IsPaused,
                    //ZonePauseSecondsLeft = programController.ZonePauseSecondsLeft,
                    InsideTemp = TemperatureSensors.InsideTemperature,
                    OutsideTemp = TemperatureSensors.OutsideTemperature,
                    CurrentTime = DateTime.Now.ToString(),
                    ZonesOn = sprinklerController.QueryAllZonesStatusAsArray(),
                    ZoneRunSecondsLeft = programController.ZoneRunSecondsLeft,
                    IsPaused = programController.IsPaused,
                    ZonePauseSecondsLeft = programController.ZonePauseSecondsLeft
                };

                System.Diagnostics.Debug.WriteLine("MakeStatusJson - got the data, serializing it");

                string json = Utils.SerializeJSon(model);
                return json;
            }
            catch (Exception ex)
            {
                return MakeJsonErrorMsg("Error getting sprinkler status", ex);
                // swallow throw;
            }

        }

        private string MakeJsonErrorMsg(string msg, Exception ex)
        {
            return String.Format("{\"isError\":\"true\",\"msg\":\"{0}\", \"errorMsg\":\"{1}\",\"stackTrace\":\"{2}\"}",
                msg, ex.Message, ex.ToString());
        }

        /// <summary>
        /// Parse out the program/zone/command
        /// Note that zone might be -1 if not part of the request.
        /// 
        /// example requests 
        ///    //program/[PROGRAM]/[zone]/enabled - is this zone enabled
        ///    //program/[PROGRAM]/starttime - what's the program start time
        ///
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        private Tuple<int, int, string> ParseProgZoneCmd(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int prog = int.Parse(items[1]);
                string cmd = "";
                int zone = -1;
                if(items.Length == 3)
                {
                    cmd = items[2];
                }
                else
                {
                    zone = int.Parse(items[2]);
                    cmd = items[3];
                }
                return new Tuple<int, int, string>(prog, zone, cmd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
         }

        
        /// <summary>
        /// Parse out the program command from a message
        ///   Format is something like: PROGRAM/PAUSE/60
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <returns></returns>
        private Tuple<string, string> ParseProgramCommand(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = items[1];
                string param = null;
                if(items.Count() > 2)
                    param = items[2];
                return new Tuple<string, string>(cmd, param);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }


        /// <summary>
        /// Parse out the zone and command from a message
        ///   Format is something like: ZONE/[zone]/running
        /// </summary>
        /// <param name="requestUpper"></param>
        /// <returns></returns>
        private Tuple<int, string> ParseZoneCommand(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int zone = int.Parse(items[1]);
                string cmd = items[2];
                return new Tuple<int, string>(zone, cmd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing set from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }

        private string ParseQueryCmd(string requestUpper)
        {
            try
            {
                string[] items = requestUpper.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string cmd = items[1];
                return cmd;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error parsing query command from " + requestUpper);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                //swallow
            }

            return null;
        }

        //Handles all HTTP POST's
        /// <summary>
        /// 
        /// 
        /// //ZONE/[zone]/on - currently running or not?
        /// //ZONE/[zone]/off - currently running or not?
        /// //PROGRAM/PAUSE/[minutes]  pause current running program for this number of minutes
        /// //PROGRAM/UNPAUSE  
        /// //PROGRAM/STOP
        /// //PROGRAM/RUNNEXTZONE
        /// //PROGRAM/START/[program index]  run the program at index given (zero based)
        /// 
        /// //SET/ZONELIST   - sets the zone information
        /// //SET/PROGRAMDATA  - sets the program data
        /// </summary>
        /// <param name="request"></param>
        /// <param name="os"></param>
        /// <returns></returns>
        private async Task WritePostResponseAsync(string request, IOutputStream os, string json)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            string responseMsg = "";

            string requestUpper = request.ToUpper();
            if(requestUpper.StartsWith("/ZONE/"))
            {
                Tuple<int, string> zc = ParseZoneCommand(requestUpper);
                if (zc == null)
                {
                    bodyArray = Encoding.UTF8.GetBytes("Invalid zone command");
                    await WriteResponseAsync(request.ToUpper(), "Invalid zone command", false, bodyArray, os, 400);
                    return;
                }
                int zone = zc.Item1;
                string cmd = zc.Item2;
                switch(cmd)
                {
                    case "ON":
                        {
                            urlFound = true;
                            bool stat = sprinklerController.SetZoneOn(zone);
                            responseMsg = stat ? "ON" : "OFF";
                            break;
                        }
                    case "OFF":
                        {
                            urlFound = true;
                            bool stat = sprinklerController.SetZoneOff(zone);
                            responseMsg = stat ? "OFF" : "ON";
                            break;
                        }
                }
            }
            else if(requestUpper.StartsWith("/PROGRAM/"))
            {
                try
                {
                    Tuple<string, string> zc = ParseProgramCommand(requestUpper);
                    if (zc == null)
                        throw new ArgumentException("Invalid program command");
                    string cmd = zc.Item1;
                    switch (cmd)
                    {
                        case "PAUSE":
                            urlFound = true;
                            int minutes = int.Parse(zc.Item2);
                            programController.PauseProgram(minutes);
                            responseMsg = "true";
                            break;
                        case "UNPAUSE":
                            urlFound = true;
                            programController.UnpauseProgram();
                            responseMsg = "true";
                            break;
                        case "STOP":
                            urlFound = true;
                            programController.StopProgram();
                            responseMsg = "true";
                            break;
                        case "RUNNEXTZONE":
                            urlFound = true;
                            programController.RunNextZone();
                            responseMsg = "true";
                            break;
                        case "START":
                            urlFound = true;
                            int progIndex = int.Parse(zc.Item2);
                            programController.StartProgram(progIndex);
                            responseMsg = "true";
                            break;
                        //case "ENABLE":
                        //    urlFound = true;
                        //    // need program id!!!
                        //    Boolean ON = bool.Parse (zc.Item2);
                        //    programController.EnableProgram(on);
                        //    responseMsg = "true";
                        //    break;
                    }

                }
                catch (Exception ex)
                {
                    responseMsg = MakeJsonErrorMsg("Error", ex);
                    //swallow throw;
                }         

            }
            else if (requestUpper.StartsWith("/SET/"))
            {
                try
                {
                    Tuple<string, string> zc = ParseProgramCommand(requestUpper);
                    if (zc == null)
                        throw new ArgumentException("Invalid settings command");
                    string cmd = zc.Item1;
                    switch (cmd)
                    {
                        case "ZONELIST":
                            urlFound = true;
                            responseMsg = UpdateZoneList(json);
                            break;
                        case "PROGRAMDATA":
                            urlFound = true;
                            responseMsg = UpdateProgramData(json);
                            break;
                    }

                }
                catch (Exception ex)
                {
                    responseMsg = MakeJsonErrorMsg("Error", ex);
                    //swallow throw;
                }

            }

            bodyArray = Encoding.UTF8.GetBytes(responseMsg);
            await WriteResponseAsync(request.ToUpper(), responseMsg, urlFound, bodyArray, os);
        }

        //Write the response for unsupported HTTP methods
        private async Task WriteMethodNotSupportedResponseAsync(string request, IOutputStream os)
        {
            bool urlFound = false;
            byte[] bodyArray = null;
            bodyArray = Encoding.UTF8.GetBytes("");
            await WriteResponseAsync(request.ToUpper(), "NOT SUPPORTED", urlFound, bodyArray, os, 405);
        }

        //Write the response for HTTP GET's and POST's 
        private async Task WriteResponseAsync(string RequestMsg, string ResponseMsg, bool urlFound, byte[] bodyArray, IOutputStream os, int statusCode = 0)
        {
            //try  //The appService will die after a day or so. Let 's try catch it seperatly so the server will still return
            //{
            //    var updateMessage = new ValueSet();
            //    updateMessage.Add("Request", RequestMsg);
            //    updateMessage.Add("Response", ResponseMsg);
            //    var responseStatus = await appServiceConnection.SendMessageAsync(updateMessage);
            //}
            //catch (Exception) { }

            try
            {
                MemoryStream bodyStream = new MemoryStream(bodyArray);
                using (Stream response = os.AsStreamForWrite())
                {
                    bool includeBody = statusCode == 0 ? urlFound : statusCode != 404;
                    string contentType = ResponseMsg.StartsWith("{") || ResponseMsg.StartsWith("[")
                        ? "application/json; charset=utf-8"
                        : "text/plain; charset=utf-8";
                    string header = GetHeader(urlFound, includeBody ? bodyStream.Length.ToString() : "0", contentType, statusCode);
                    byte[] headerArray = Encoding.UTF8.GetBytes(header);
                    await response.WriteAsync(headerArray, 0, headerArray.Length);
                    if (includeBody)
                        await bodyStream.CopyToAsync(response);
                    await response.FlushAsync();
                    System.Diagnostics.Debug.WriteLine("wrote response");
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Error writing response: " + ex.Message);
            }
        }

        //Creates the HTTP header text for found and not found urls
        string GetHeader(bool urlFound, string bodyStreamLength, string contentType, int statusCode)
        {
            if (statusCode == 0)
                statusCode = urlFound ? 200 : 404;

            string statusText;
            switch (statusCode)
            {
                case 200:
                    statusText = "200 OK";
                    break;
                case 400:
                    statusText = "400 Bad Request";
                    break;
                case 405:
                    statusText = "405 Method Not Allowed";
                    break;
                default:
                    statusText = "404 Not Found";
                    break;
            }

            string header;
            if (statusCode == 200)
            {
                header = "HTTP/1.1 " + statusText + "\r\n" +
                           "Access-Control-Allow-Origin: *\r\n" +
                           "Content-Type: " + contentType + "\r\n" +
                           "Content-Length: " + bodyStreamLength + "\r\n" +
                           "Connection: close\r\n\r\n";
            }
            else
            {
                header = "HTTP/1.1 " + statusText + "\r\n" +
                         "Access-Control-Allow-Origin: *\r\n" +
                         "Content-Type: " + contentType + "\r\n" +
                         "Content-Length: 0\r\n" +
                         "Connection: close\r\n\r\n";
            }
            return header;
        }
    }

}
