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
        JavaScriptSerializer jsonSerializer;
        string myId;

        public P2PClient(string myId, string ip, int port)
        {
            Logger.log(CLIENT_LOG, "Initializing client " + myId);
            this.myId = myId;
            clientTask = Task.Run(() => runClient(ip, port));
            jsonSerializer = new JavaScriptSerializer();
        }

        private void runClient(string ip, int port)
        {
            TcpClient client = new TcpClient();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            Logger.log(myId, "Requesting connection to " + ip + ":" + port);

            client.Connect(serverEndPoint);

            NetworkStream clientStream = client.GetStream();
            StreamWriter sw = new StreamWriter(clientStream);
           
            try
            {
                Logger.log(myId, "Starting handshake");
                send(sw, connectMessage());
            }
            catch
            {
               Logger.log(myId, "Connection closed by the server.");
            }


            sw.Flush(); // send last messages
            System.Threading.Thread.Sleep(1000);
            client.Close();
        }

        dynamic connectMessage()
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "connect";
            dict["peerId"] = myId;
            return dict;
        }

        void send(StreamWriter sw, dynamic dict)
        {
            string json = jsonSerializer.Serialize(dict);
            sw.WriteLine(json);
        }
    }
}
