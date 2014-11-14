using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Web.Script.Serialization;

namespace Hermes
{
    class P2PClient
    {
        private static readonly string CLIENT_LOG = "CLIENT";

        private Task clientTask;
        private JavaScriptSerializer jsonSerializer;
        private string myId;
        private string fileId;
        private FileManager fileManager;
        private P2PDownloader downloader;

        public P2PClient(string myId, string fileId, string ip, int port, FileManager fileManager)
        {
            Logger.log(CLIENT_LOG, "Initializing client " + myId);
            this.myId = myId;
            this.fileId = fileId;
            this.fileManager = fileManager;
            this.downloader = new P2PDownloader(fileId, fileManager);
            clientTask = Task.Run(() => runClient(ip, port));
            jsonSerializer = new JavaScriptSerializer();
        }

        private void runClient(string ip, int port)
        {
            TcpClient client = new TcpClient();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            Logger.log(myId, "Requesting connection to " + ip + ":" + port);

            try
            {
                // connecting to server
                client.Connect(serverEndPoint);

                NetworkStream clientStream = client.GetStream();
                StreamReader sr = new StreamReader(clientStream);
                StreamWriter sw = new StreamWriter(clientStream);

                // Starting handshake
                Logger.log(myId, "Starting handshake");
                send(sw, connectMessage(fileId));

                // Receiving handshake answer
                string handshake = sr.ReadLine();
                if (handshake == null)  // disconnected
                {
                    throw new Exception();
                }

                var json = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(handshake);

                if (json["type"] != "connect")
                {
                    Logger.log(myId, "Server didn't answer the handshake properly. Answer: " + handshake);
                }

                downloader.setBitField(json["bitField"]);

                Logger.log(myId, "Handshake complete");

                sw.Flush(); // send last messages
                System.Threading.Thread.Sleep(1000); // waiting for last messages to be sent and read
                clientStream.Close();
            }
            catch
            {
                Logger.log(myId, "Connection closed by the server.");
            }
            finally
            {
                client.Close();
            }
        }

        dynamic connectMessage(string fileId)
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "connect";
            dict["peerId"] = myId;
            dict["fileId"] = fileId;
            return dict;
        }

        void send(StreamWriter sw, dynamic dict)
        {
            string json = jsonSerializer.Serialize(dict);
            sw.WriteLine(json);
            sw.Flush();
        }
    }
}
