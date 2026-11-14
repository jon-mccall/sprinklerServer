using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SprinklerWebServer
{
    //Class to define the HTTP WebServer
    public sealed class WebServer : IDisposable
    {
        //Create a buffer to read HTTP data
        private const uint BufferSize = 8192;
        //Port to listen on
        private int port = 8889;
        //Listener to
        private readonly StreamSocketListener listener;
        Dictionary<string, string> mimeTypeMap = new Dictionary<string, string>();
        // root folder for web files (set in ctor)
        private string sMyWebServerRoot;

        public WebServer(int serverPort)
        {
            listener = new StreamSocketListener();
            port = serverPort;

            mimeTypeMap.Add(".htm",  "text/html" );
            mimeTypeMap.Add(".html", "text/html" );
            mimeTypeMap.Add(".asp",  "text/html" );
            mimeTypeMap.Add(".css", "text/css");
            mimeTypeMap.Add(".js", "text/javascript");
            mimeTypeMap.Add(".bmp", "image/bmp");
            mimeTypeMap.Add(".jpg", "image/jpeg");
            mimeTypeMap.Add(".png", "image/png");

            //Add event handler for HTTP connections
            listener.ConnectionReceived += async (s, e) => await ProcessRequestAsync(e.Socket);

            System.Diagnostics.Debug.WriteLine("Sprinkler Web server starting...");
            System.Diagnostics.Debug.WriteLine("Current dir: "+ Directory.GetCurrentDirectory());

            sMyWebServerRoot = String.Format(@"{0}\web", Directory.GetCurrentDirectory());
        }

        //Call to start the listner 
        internal async Task StartServerAsync()
        {
            System.Diagnostics.Debug.WriteLine("WebServer - binding the listener");
            await listener.BindServiceNameAsync(port.ToString());
            System.Diagnostics.Debug.WriteLine("Web server bound to port " + port);
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("WebServer - disposing the listener");
            listener.Dispose();
        }


        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            string name = "";
            try
            {
                //System.Diagnostics.Debug.WriteLine("ProcessRequestAsync called...");
                StringBuilder request = new StringBuilder();
                //Get the incomming data
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[BufferSize];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = BufferSize;
                    //Read all the incoming data; use the actual bytes read each iteration
                    while (dataRead == BufferSize)
                    {
                        IBuffer readBuffer = await input.ReadAsync(buffer, BufferSize, InputStreamOptions.Partial);
                        uint bytesRead = readBuffer.Length;
                        if (bytesRead == 0) break;
                        request.Append(Encoding.UTF8.GetString(data, 0, (int)bytesRead));
                        dataRead = bytesRead;
                    }
                }

                //Got the data start processing a response
                using (IOutputStream output = socket.OutputStream)
                {
                    string requestMethod = request.ToString();
                    string[] requestParts = { "" };
                    if (requestMethod != null)
                    {
                        //System.Diagnostics.Debug.WriteLine(requestMethod);
                        //Beakup the request into it parts
                        requestMethod = requestMethod.Split('\n')[0];
                        requestParts = requestMethod.Split(' ');
                    }
                    name = requestMethod;
                    //We only respond HTTP GETS and POST methods
                    if (requestParts[0] == "GET")
                    {
                        await HandleGetRequest(requestMethod, requestParts, output);
                    }
                    //if (requestParts[0] == "GET")
                    //    await WriteGetResponseAsync(requestParts[1], output);
                    //else if (requestParts[0] == "POST")
                    //    await WritePostResponseAsync(requestParts[1], output);
                    else
                    {
                        string msg = "<H2>Error!! Method " + requestParts[0] + "not supported.</H2><Br>";
                        //sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";

                        //Format The Message
                        byte[] header = MakeReponseHeader("HTTP/1.1", "", msg.Length, "");

                        //Send to the browser
                        await SendToBrowser(msg, header, output);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error ("+ name + "): " + ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);

            }
            finally
            {
                //System.Diagnostics.Debug.WriteLine("ProcessRequestAsync Finished! " + name);
                socket.Dispose();

            }
        }




        private async Task HandleGetRequest(string requestMethod, string[] requestParts, IOutputStream output)
        {
            // Look for HTTP request
            int iStartPos = requestMethod.IndexOf("HTTP", 1);

            // Get the HTTP text and version e.g. it will return "HTTP/1.1"
            string sHttpVersion = (iStartPos >= 0 && requestMethod.Length >= iStartPos + 8) ? requestMethod.Substring(iStartPos, 8) : "HTTP/1.1";

            string sRequestedFile = (requestParts.Length > 1) ? requestParts[1] : "/";
            // strip query string if present
            if (sRequestedFile.Contains("?")) sRequestedFile = sRequestedFile.Substring(0, sRequestedFile.IndexOf('?'));

            /////////////////////////////////////////////////////////////////////
            // Identify the Physical Directory
            /////////////////////////////////////////////////////////////////////
            if (sRequestedFile == "/")
            {
                sRequestedFile += "index.html";
            }

            string sfile = sRequestedFile.Replace('/', '\\');
            // use Path.Combine to construct physical path; trim leading slashes from sfile
            string sPhysicalFilePath = Path.Combine(sMyWebServerRoot, sfile.TrimStart('\\'));


            //System.Diagnostics.Debug.WriteLine("File Requested : " + sRequestedFile);

            //If the physical directory does not exists then
            // dispaly the error message
            string sErrorMessage = null;
            if (!File.Exists(sPhysicalFilePath))
            {
                sErrorMessage = "<H2>Error!! Requested Resource does not exists</H2><Br>";
                //sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";

                //Format The Message
                byte[] header = MakeReponseHeader(sHttpVersion, "", sErrorMessage.Length, " 404 Not Found");

                //Send to the browser
                await SendToBrowser(sErrorMessage, header, output);

            }
            else
            {
                // Get TheMime Type
                String sMimeType = GetMimeType(sRequestedFile);

                byte[] bytes = File.ReadAllBytes(sPhysicalFilePath);
                int iTotBytes = bytes.Length;
                byte[] header = MakeReponseHeader(sHttpVersion, sMimeType, iTotBytes, " 200 OK");
                await SendToBrowser(bytes, header, output);
            }
        }

        /// <summary>
        /// This function takes FileName as Input and returns the mime type..
        /// </summary>
        /// <param name="sRequestedFile">To indentify the Mime Type</param>
        /// <returns>Mime Type</returns>
        public string GetMimeType(string sRequestedFile)
        {
            string ext = Path.GetExtension(sRequestedFile).ToLower();
            if (mimeTypeMap.ContainsKey(ext))
            {
                return mimeTypeMap[ext];
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("UNKNOWN MIME TYPE: " + ext + ", requested file '" + sRequestedFile + "'");
                return mimeTypeMap[".html"];
            }
        }



        /// <summary>
        /// This function send the Header Information to the client (Browser)
        /// </summary>
        /// <param name="sHttpVersion">HTTP Version</param>
        /// <param name="sMIMEHeader">Mime Type</param>
        /// <param name="iTotBytes">Total Bytes to be sent in the body</param>
        /// <param name="mySocket">Socket reference</param>
        /// <returns></returns>
        public byte[] MakeReponseHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode)
        {

            StringBuilder sBuffer = new StringBuilder();

            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }

            sBuffer.Append(sHttpVersion);
            sBuffer.Append(sStatusCode);
            sBuffer.Append("\r\nServer: cx1193719-b\r\nContent-Type: ");
            sBuffer.Append(sMIMEHeader);
            sBuffer.Append("\r\nAccept-Ranges: bytes\r\nContent-Length: ");
            sBuffer.Append(iTotBytes);
            sBuffer.Append("\r\nConnection: close\r\n\r\n");


            //sBuffer = sBuffer + sHttpVersion + sStatusCode + "\r\n";
            //sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            //sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            //sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            //sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n";
            //sBuffer = sBuffer + "Connection: close\r\n\r\n";

            //System.Diagnostics.Debug.WriteLine("Reponse header: " + sBuffer);

            Byte[] bSendData = Encoding.ASCII.GetBytes(sBuffer.ToString());
            return bSendData;

        }

        /// <summary>
        /// Overloaded Function, takes string, convert to bytes and calls 
        /// overloaded sendToBrowserFunction.
        /// </summary>
        /// <param name="sData">The data to be sent to the browser(client)</param>
        /// <param name="mySocket">Socket reference</param>
        private async Task SendToBrowser(String sData, [ReadOnlyArray()] byte[] header, IOutputStream os)
        {
            await SendToBrowser(Encoding.ASCII.GetBytes(sData), header, os);
        }

        /// <summary>
        /// Sends data to the browser (client)
        /// </summary>
        /// <param name="bSendData">Byte Array</param>
        /// <param name="mySocket">Socket reference</param>
        private async Task SendToBrowser([ReadOnlyArray()] byte[] bSendData, [ReadOnlyArray()]byte[] header, IOutputStream os)
        {
            try
            {
                MemoryStream bodyStream = new MemoryStream(bSendData);
                using (Stream response = os.AsStreamForWrite())
                {

                    await response.WriteAsync(header, 0, header.Length);
                    await bodyStream.CopyToAsync(response);
                    await response.FlushAsync();
                    //System.Diagnostics.Debug.WriteLine("No. of bytes send {0}", bSendData.Length);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error writing response: " + ex.Message);
            }

        }


    }
}
