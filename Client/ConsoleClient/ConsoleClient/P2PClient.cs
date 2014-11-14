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
        private Task clientTask;
        JavaScriptSerializer jsonSerializer;
        string myId;

        public P2PClient(string myId, string ip, int port)
        {
            this.myId = myId;
            clientTask = Task.Run(() => runClient(ip, port));
            jsonSerializer = new JavaScriptSerializer();
        }

        private void runClient(string ip, int port)
        {
            TcpClient client = new TcpClient();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            client.Connect(serverEndPoint);

            NetworkStream clientStream = client.GetStream();
            StreamWriter sw = new StreamWriter(clientStream);
           
            try
            {
                send(sw, connectMessage());
            }
            catch
            {
                Console.WriteLine("[WARNING]: Connection closed by the server.");
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
