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
        private static readonly string SERVER_LOG = "SERVER";

        public static readonly int SERVER_PORT = 3000;

        private TcpListener tcpListener;
        private Task listenTask;

        private FileManager fileManager;
        private JavaScriptSerializer jsonSerializer;
        private Dictionary<string /*peer id*/, P2PUploader> uploaders;
        private string myId;

        public P2PServer(string myId, FileManager fileManager)
        {
            Logger.log(SERVER_LOG, "Initializing P2P server...");
            this.myId = myId;
            this.fileManager = fileManager;
            this.tcpListener = new TcpListener(IPAddress.Any, SERVER_PORT);
            uploaders = new Dictionary<string, P2PUploader>();
            jsonSerializer = new JavaScriptSerializer();
            listenTask = Task.Run(() => listenForClients());
        }

        private void listenForClients()
        {
            this.tcpListener.Start();
            Logger.log(SERVER_LOG, "P2P Server initialized");

            while (true)
            {
                // blocks until a client has connected to the server
                TcpClient client = this.tcpListener.AcceptTcpClient();
                Logger.log(SERVER_LOG, "Accepted new client");
                // create a thread to handle communication 
                // with connected client
                Task.Run(() => handleClientComm(client));
            }
        }

        private void handleClientComm(TcpClient tcpClient)
        {
            
            try
            {
                // get client stream (using \n as delimiter
                NetworkStream clientStream = tcpClient.GetStream();
                StreamReader sr = new StreamReader(clientStream);
                StreamWriter sw = new StreamWriter(clientStream);            

                // weather the client is connected
                bool connected = false; // initially false... waiting for initial handshake
                string peerId = ""; // initialized during the 'connect' message
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
                        if (!connected && json["type"] != "connect") 
                        {
                            Logger.log(SERVER_LOG, "First message is not a handshake: " + data);
                            break;
                        }
                        switch ((string)json["type"])
                        {
                            case "connect":
                                peerId = json["peerId"];
                                lock (chokeUnchokeLock)
                                {
                                    connectedPeers.Add(peerId);
                                }
                                maybeChokeRandomPeer();
                                Logger.log(SERVER_LOG, string.Format("Peer \"{0}\" started handshake...", peerId));
                                connected = true;
                                lock (uploaders)
                                {
                                    if (peerId == myId || uploaders.ContainsKey(peerId))
                                    {
                                        Logger.log(SERVER_LOG, "Connection with peer " + peerId + " rejected");
                                        connected = false;
                                    }
                                    else
                                    {
                                        Logger.log(SERVER_LOG, "Connection with peer " + peerId + " accepted");
                                        uploaders[peerId] = new P2PUploader(fileManager, json["fileId"]);
                                    }

                                    Logger.log(SERVER_LOG, "Answering handshake");
                                    send(sw, connectMessage(uploaders[peerId].getBitField()));
                                }

                                break;
                            case "request":
                                int piece = json["piece"];
                                int block = json["block"];
                                Logger.log(SERVER_LOG, "Client " + peerId + " requested (piece, block) = (" + piece + "," + block + ")");
                                P2PUploader uploader;
                                lock(uploaders)
                                {
                                    uploader = uploaders[peerId];
                                }
                                string blockData = uploader.getBlock(piece, block);
                                send(sw, dataMessage(blockData));
                                break;
                            case "cancel":
                                break;
                            case "close":
                                connected = false;
                                Logger.log(SERVER_LOG, "Connection closed by the client");
                                break;
                            default:
                                connected = false;
                                Logger.log(SERVER_LOG, "Unknown message type: " + json["type"]);
                                break;

                        }
                    }
                    catch (Exception e)
                    {
                        Logger.log(SERVER_LOG, e.ToString());
                        connected = false;
                    }
                } while (connected);

                sw.Flush();
                System.Threading.Thread.Sleep(1000);
                clientStream.Close();
            }
            catch (Exception e)
            {
                Logger.log(SERVER_LOG, e.ToString());
            }
            finally
            {
                tcpClient.Close();
            }
        }

        dynamic connectMessage(string bitField)
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "connect";
            dict["bitField"] = bitField;
            return dict;
        }

        dynamic dataMessage(string data)
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "data";
            dict["content"] = data;
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
