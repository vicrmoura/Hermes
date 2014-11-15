using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Web.Script.Serialization;
using System.Threading;

namespace Hermes
{
    class P2PClient
    {
        private static readonly string CLIENT_LOG = "CLIENT";

        private Task clientTask;
        private JavaScriptSerializer jsonSerializer;
        private string myId;
        private FileManager fileManager;
        private P2PDownloader downloader;
        private bool isDownloading;
        private string logLabel;

        public P2PClient(string myId, P2PDownloader downloader, string ip, int port, FileManager fileManager)
        {
            Logger.log(CLIENT_LOG, "Initializing client " + myId);
            this.myId = myId;
            this.fileManager = fileManager;
            this.downloader = downloader;
            this.isDownloading = false;
            this.logLabel = myId + ":" + downloader.FileId;
            clientTask = Task.Run(() => runClient(ip, port));
            jsonSerializer = new JavaScriptSerializer();
        }

        private int lastRequestedPiece;
        private int lastRequestedBlock;

        private void runClient(string ip, int port)
        {
            TcpClient client = new TcpClient();

            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            Logger.log(logLabel, "Requesting connection to " + ip + ":" + port);

            try
            {
                // connecting to server
                client.Connect(serverEndPoint);
                
                NetworkStream clientStream = client.GetStream();
                StreamReader sr = new StreamReader(clientStream);
                StreamWriter sw = new StreamWriter(clientStream);

                // Starting handshake
                Logger.log(logLabel, "Starting handshake");
                send(sw, connectMessage(downloader.FileId));

                // Receiving handshake answer
                string handshake = sr.ReadLine();
                if (handshake == null)  // disconnected
                {
                    throw new Exception();
                }

                var json = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(handshake);

                if (json["type"] != "connect")
                {
                    Logger.log(logLabel, "Server didn't answer the handshake properly. Answer: " + handshake);
                }

                downloader.setBitField(json["bitField"]);

                Logger.log(logLabel, "Handshake complete");

                object cv = new object();
                
                isDownloading = true;
                Task t = Task.Run(() => clientListener(sr, sw, cv));
                
                while (isDownloading)
                {
                    var tup = downloader.getNextBlock();
                    if (tup == null)
                    {
                        Logger.log(logLabel, "Finished downloading");
                        isDownloading = false;
                        break;
                    }

                    int piece = tup.Item1;
                    int block = tup.Item2;
                    lastRequestedPiece = piece;
                    lastRequestedBlock = block;

                    Logger.log(logLabel, "Requesting (piece, block) = (" + piece + ", " + block + ")");
                    var message = requestMessage(piece, block);
                    send(sw, message);
                    
                    lock (cv)
                    {
                        Monitor.Wait(cv); // waiting server to answer
                    }
                }

                // Closing protocol
                Logger.log(logLabel, "Closing connection");
                send(sw, closeMessage());

                t.Wait();
                sw.Flush(); // send last messages
                System.Threading.Thread.Sleep(1000); // waiting for last messages to be sent and read
                clientStream.Close();
            }
            catch(Exception e)
            {
                Logger.log(logLabel, e.ToString());
                Logger.log(logLabel, "Connection closed by the server.");
            }
            finally
            {
                isDownloading = false;
                client.Close();
            }

        }

        void clientListener(StreamReader sr, StreamWriter sw, object requesterCv)
        {
            while (isDownloading)
            {
                try
                {
                    string data = sr.ReadLine();
                    if (data == null)  // disconnected
                    {
                        isDownloading = false;
                        break;
                    }

                    var json = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(data);
                    switch ((string)json["type"])
                    {
                        
                        case "data":
                            string content = json["content"];
                            int piece = lastRequestedPiece;
                            int block = lastRequestedBlock;
                            Logger.log(logLabel, "Received (piece, block) = (" + piece + "," + block + ")");
                            lock (requesterCv)
                            {
                                Monitor.Pulse(requesterCv); // let the other thread request more blocks
                            }
                            downloader.addBlock(piece, block, content);
                            break;
                        case "have":
                            break;
                        case "choke":
                            break;
                        case "unchoke":
                            break;
                        case "close":
                            isDownloading = false;
                            Logger.log(logLabel, "Connection closed by the client");
                            break;
                        default:
                            Logger.log(logLabel, "Unknown message type: " + json["type"]);
                            isDownloading = false;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.log(logLabel, e.ToString());
                    isDownloading = false;
                }
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

        dynamic requestMessage(int piece, int block)
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "request";
            dict["piece"] = piece;
            dict["block"] = block;
            return dict;
        }

        dynamic closeMessage()
        {
            var dict = new Dictionary<string, dynamic>();
            dict["type"] = "close";
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
