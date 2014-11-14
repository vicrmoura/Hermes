using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;

namespace Hermes
{
    class P2PServer
    {
        public static readonly int SERVER_PORT = 3000;

        private TcpListener tcpListener;
        private Task listenTask;

        private JavaScriptSerializer jsonSerializer;

        Dictionary<string /*peer id*/, P2PUploader> uploaders;
        
        string myId;

        public P2PServer(string myId)
        {
            this.myId = myId;
            this.tcpListener = new TcpListener(IPAddress.Any, SERVER_PORT);
            uploaders = new Dictionary<string, P2PUploader>();
            jsonSerializer = new JavaScriptSerializer();
            listenTask = Task.Run(() => listenForClients());
        }

        private void listenForClients()
        {
            this.tcpListener.Start();

            while (true)
            {
                // blocks until a client has connected to the server
                TcpClient client = this.tcpListener.AcceptTcpClient();

                // create a thread to handle communication 
                // with connected client
                Task.Run(() => handleClientComm(client));
            }
        }

        private void handleClientComm(TcpClient tcpClient)
        {
            // get client stream (using \n as delimiter
            NetworkStream clientStream = tcpClient.GetStream();
            StreamReader sr = new StreamReader(clientStream);            

            // weather the client is connected
            bool connected = false; // initially false... waiting for initial handshake
            string peerId; // initialized during the 'connect' message
            
            do
            {
                try
                {
                    string data = sr.ReadLine();
                   
                    if (data == null)  // disconnected
                    {
                        connected = false;
                        break;
                    }

                    var json = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(data);
                    switch ((string)json["type"])
                    {
                        case "connect":
                            connected = true;
                            peerId = json["peerId"];
                            if (peerId == myId || uploaders.ContainsKey(peerId))
                            {
                                connected = false;
                            }
                            else
                            {
                                uploaders[peerId] = new P2PUploader();
                            }
                            break;
                        case "request":
                            break;
                        case "cancel":
                            break;
                        case "close":
                            connected = false;
                            break;
                        default:
                            connected = false;
                            break;

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    connected = false;
                }
            } while (connected);

            System.Threading.Thread.Sleep(1000);
            tcpClient.Close();
        }
    }
}
