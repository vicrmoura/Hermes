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
        private Dictionary<string, int> qtd = new Dictionary<string,int>(); // for debuging purposes


        private static readonly string SERVER_LOG = "SERVER";
        private static readonly int MAX_UNCHOKED = 5;

        public static readonly int SERVER_PORT = 3000;

        private TcpListener tcpListener;
        private Task listenTask;

        private FileManager fileManager;
        private JavaScriptSerializer jsonSerializer;
        private Dictionary<string /*peer id*/, P2PUploader> uploaders;
        private string myId;

        private HashSet<string /*peer id*/> chokedSet;
        private List<string /*peer id*/> connectedPeers;
        private object chokeUnchokeLock = new object();
        Random random = new Random();

        public P2PServer(string myId, FileManager fileManager)
        {
            Logger.log(SERVER_LOG, "Initializing P2P server...");
            this.myId = myId;
            this.fileManager = fileManager;
            this.tcpListener = new TcpListener(IPAddress.Any, SERVER_PORT);
            this.chokedSet = new HashSet<string>();
            this.connectedPeers = new List<string>();
            uploaders = new Dictionary<string, P2PUploader>();
            jsonSerializer = new JavaScriptSerializer();
            listenTask = Task.Run(() => listenForClients());
            Task.Run(() => chokeManagerTask());
        }

        private void listenForClients()
        {
            this.tcpListener.Start();
            Logger.log(SERVER_LOG, "P2P Server initialized");

            while (true)
            {
                // blocks until a clien t has connected to the server
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
                bool myChokeState = false;
                do
                {
                    try
                    {
                        if (!myChokeState)
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
                                    string fileId = json["fileId"];
                                    peerId = json["peerId"] + "#" + fileId; // # is a forbidden character for fileId and peerId
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
                                            uploaders[peerId] = new P2PUploader(fileManager, fileId);
                                        }

                                        Logger.log(SERVER_LOG, "Answering handshake");
                                        send(sw, connectMessage(uploaders[peerId].getBitField()));
                                    }
                                    if (!connected) break;

                                    lock (chokeUnchokeLock)
                                    {
                                        connectedPeers.Add(peerId);
                                    }
                                    qtd[peerId] = 0;
                                    myChokeState = false;
                                    maybeChokeRandomPeer(); // maybe i will get choked here... who knows!

                                    break;
                                case "request":
                                    int piece = json["piece"];
                                    int block = json["block"];
                                    //Logger.log(SERVER_LOG, "Client " + peerId + " requested (piece, block) = (" + piece + "," + block + ") [" + (qtd++) + " block requested]");
                                    //Logger.log(SERVER_LOG, "Client " + peerId + " [" + (qtd++) + " block requested]");
                                    qtd[peerId]++;
                                    P2PUploader uploader;
                                    lock (uploaders)
                                    {
                                        uploader = uploaders[peerId];
                                    }
                                    string blockData = uploader.getBlock(piece, block);
                                    send(sw, dataMessage(blockData));
                                    break;
                                //case "cancel": // for now caga    
                                  //  break;
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
                        else
                        {
                            System.Threading.Thread.Sleep(5000);
                        }

                        // if choke state changed, send message
                        bool shouldBeChoked;
                        lock(chokeUnchokeLock)
                        {
                            shouldBeChoked = chokedSet.Contains(peerId);
                        }
                        if (myChokeState && !shouldBeChoked)
                        {
                            myChokeState = false;
                            send(sw, unchokeMessage());
                        }
                        if (!myChokeState && shouldBeChoked)
                        {
                            myChokeState = true;
                            Logger.log(SERVER_LOG, "Choking " + peerId + " [" + qtd[peerId] + " request messages]");
                            send(sw, chokeMessage());
                        } 

                    }
                    catch (Exception e)
                    {
                        Logger.log(SERVER_LOG, e.ToString());
                        connected = false;
                    }
                } while (connected);

                // unchoke someone if I was unchoked because i am leaving
                lock (chokeUnchokeLock)
                {
                    connectedPeers.Remove(peerId);
                    if (chokedSet.Contains(peerId))
                    {
                        chokedSet.Remove(peerId);
                    }
                    else
                    {
                        unchokeRandomPeer();
                    }
                }

                sw.Flush();
                System.Threading.Thread.Sleep(1000); // wait for last messages to be sent and read
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

        dynamic chokeMessage()
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "choke";
            return dict;
        }

        dynamic unchokeMessage()
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "unchoke";
            return dict;
        }

        void maybeChokeRandomPeer()
        {
            lock (chokeUnchokeLock)
            {
                if (connectedPeers.Count - chokedSet.Count > MAX_UNCHOKED)
                {
                    chokeRandomPeer();
                }
            }
        }

        // not thread safe
        void chokeRandomPeer()
        {
            while (true)
            {
                int toChoke = random.Next() % connectedPeers.Count;
                if (chokedSet.Contains(connectedPeers[toChoke]))
                {
                    continue;
                }
                chokedSet.Add(connectedPeers[toChoke]);
                break;
            }
        }

        // not thread safe
        void unchokeRandomPeer()
        {
            if (chokedSet.Count == 0) return;
            while (true)
            {
                int toUnchoke = random.Next() % connectedPeers.Count;
                if (!chokedSet.Contains(connectedPeers[toUnchoke]))
                {
                    continue;
                }
                chokedSet.Remove(connectedPeers[toUnchoke]);
                break;
            }
        }

        private void chokeManagerTask() 
        {
            while (true)
            {
                System.Threading.Thread.Sleep(60000); // wait 1 minute to change choked peers
                lock (chokeUnchokeLock)
                {
                    chokedSet.Clear();
                    if (connectedPeers.Count - chokedSet.Count > MAX_UNCHOKED)
                    {
                        for (int i = 0; i < connectedPeers.Count - MAX_UNCHOKED; i++)
                        {
                            chokeRandomPeer();
                        }
                    }
                }
            }
        }

        void send(StreamWriter sw, dynamic dict)
        {
            string json = jsonSerializer.Serialize(dict);
            sw.WriteLine(json);
            sw.Flush();
        }
    }
}
