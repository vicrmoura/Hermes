using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Web.Script.Serialization;
using System.Collections;

namespace Hermes
{
    public class TrackerClient
    {
        private string trackerPort;
        public string TrackerPort
        {
            get { return trackerPort; }
            set { trackerPort = value; newPortSpecified = true; }
        }
        private string trackerIP;
        public string TrackerIP
        {
            get { return trackerIP; }
            set { trackerIP = value; newIPSpecified = true; }
        }
        public int HeartbeatInterval
        {
            get;
            private set;
        }

        private static readonly string TAG = "TrackerClient";
        
        private TcpClient tcpClient;
        private JavaScriptSerializer jsonSerializer;
        private bool newIPSpecified = false;
        private bool newPortSpecified = false;
        private readonly object socketLock = new object();

        public TrackerClient(string ip, string port)
        {
            TrackerIP = ip;
            TrackerPort = port;
            HeartbeatInterval = 1000;
            jsonSerializer = new JavaScriptSerializer();
        }
        
        private string SendMessage(string data)
        {
            lock (socketLock)
            {
                try
                {
                    if (tcpClient == null || !tcpClient.Connected || newIPSpecified || newPortSpecified)
                    {
                        newIPSpecified = newPortSpecified = false;
                        int port = -1;
                        try
                        {
                            port = Int32.Parse(TrackerPort);
                        }
                        catch (Exception e)
                        {
                            Logger.log(TAG, "[Error] Invalid port for tracker server. Message: " + e.Message);
                        }

                        tcpClient = new TcpClient(TrackerIP, port);
                        Logger.log(TAG, "Connected");
                    }
                    Stream s = null;
                    try
                    {
                        s = tcpClient.GetStream();
                        var sw = new StreamWriter(s);
                        var sr = new StreamReader(s);
                        sw.AutoFlush = true;
                        Logger.log(TAG, "Ready to send data");
                        sw.WriteLine(data);
                        Logger.log(TAG, "Data sent, waiting response");
                        string response = sr.ReadLine();
                        if (response == null)
                        {
                            Logger.log(TAG, "[Error] Connection closed by Tracker");
                            throw new IOException();
                        }
                        Logger.log(TAG, "Response arrived");
                        return response;
                    }
                    catch (Exception e)
                    {
                        Logger.log(TAG, "[Error] There was a problem during tracker server communication. Message: " + e.Message);
                    }
                }
                catch (Exception e)
                {
                    Logger.log(TAG, "[Error] Cannot connect to tracker server. Message: " + e.Message);
                }
            }
            return null;
        }

        public string UploadMetaInfo(HFile file, string peerID, string peerIP, string peerPort)
        {
            var dict = new Dictionary<string, dynamic> {
                 {"type", "upload"},
                 {"fileName", file.Name},
                 {"size", file.Size}, 
                 {"pieceSize", file.PieceSize},
                 {"blockSize", file.BlockSize},
                 {"piecesSHA1S", file.Pieces.Select((p) => p.Sha)},
                 {"peerID", peerID},
                 {"port", peerPort},
                 {"ip", peerIP}
            };
            Logger.log(TAG, "Starting upload of " + file.Name);
            string response = SendMessage(jsonSerializer.Serialize(dict));
            if (response == null)
            {
                throw new IOException();
            }
            Dictionary<string, dynamic> jsonResponse = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(response);
            HeartbeatInterval = jsonResponse["interval"];
            Logger.log(TAG, "Upload of " + file.Name + " successfully concluded.");
            return jsonResponse["fileID"];
        }

        // Return the results of a query in the format:
        // [{'name': 'xxx', 'size': 100, 'fileID': 'yyy', 'numOfPeers': 10}, {...}, ...]
        public ArrayList Query(string fileName, uint limit, uint offset)
        {
            var dict = new Dictionary<string, dynamic> {
                 {"type", "query"},
                 {"name", fileName},
                 {"limit", limit}, 
                 {"offset", offset}
            };
            Logger.log(TAG, "Starting search of " + fileName);
            string response = SendMessage(jsonSerializer.Serialize(dict));
            if (response == null)
            {
                throw new IOException();
            }
            Dictionary<string, dynamic> jsonResponse = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(response);
            Logger.log(TAG, "Search of " + fileName + " successfully concluded, " + jsonResponse["results"].Count + " results received.");
            return jsonResponse["results"]; 
        }


        public Dictionary<string, dynamic> Heartbeat(Dictionary<string, HFile> files, string peerID, string peerIP, string peerPort, int maxPeers = 0)
        {
            Dictionary<string, string> fileStats = new Dictionary<string, string>();
            foreach (var fileID in files.Keys) 
            {
                string status;
                StatusType sType = files[fileID].Status;
                if ( sType == StatusType.Canceled || sType == StatusType.Paused) {
                    status = "inactive";
                } else {
                    status = "active";
                }
                fileStats.Add(fileID, status);
            }
            var dict = new Dictionary<string, dynamic> {
                 {"type", "heartbeat"},
                 {"files", fileStats},
                 {"peerID", peerID},
                 {"port", peerPort},
                 {"ip", peerIP},
            };
            if (maxPeers > 0)
            {
                dict.Add("maxPeers", maxPeers);    
            }
            Logger.log(TAG, "Sending Heartbeat of " + fileStats.Count + " file(s)");
            string response = SendMessage(jsonSerializer.Serialize(dict));
            if (response == null)
            {
                throw new IOException();
            }
            Dictionary<string, dynamic> jsonResponse = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(response);
            HeartbeatInterval = jsonResponse["interval"];
            Logger.log(TAG, "Heartbeat successfully concluded.");
            Dictionary<string, dynamic> peers = jsonResponse["peers"];
            return jsonResponse["peers"];
        }
    }
}
