using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Web.Script.Serialization;

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
        private int heartbeatInterval = 1000;
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
        private int heartbeatInterval = 1000;

        public TrackerClient(string ip, string port)
        {
            TrackerIP = ip;
            TrackerPort = port;
            jsonSerializer = new JavaScriptSerializer();
        }
        
        private string sendMessage(string data)
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
                    return sr.ReadLine();
                }
                catch (Exception e)
                {
                    Logger.log(TAG, "[Error] There was a problem during tracker server communication. Message: " + e.Message);
                }
                finally
                {
                    if (s != null) s.Close();
                }
            }
            catch (Exception e)
            {
                Logger.log(TAG, "[Error] Cannot connect to tracker server. Message: " + e.Message);
            }
            return "";
        }

        public string uploadMetaInfo(HFile file, byte[][] sha1s, string peerID, string peerIP, string peerPort)
        {
            var dict = new Dictionary<string, dynamic> {
                 {"type", "upload"},
                 {"fileName", file.Name},
                 {"size", file.Size}, 
                 {"pieceSize", file.PieceSize},
                 {"blockSize", file.BlockSize},
                 {"piecesSHA1S", sha1s},
                 {"peerID", peerID},
                 {"port", peerPort},
                 {"ip", peerIP}
            };
            Logger.log(TAG, "Starting upload of " + file.Name);
            string response = sendMessage(jsonSerializer.Serialize(dict));
            if (response.Length == 0)
            {
                throw new IOException();
            }
            Dictionary<string, dynamic> jsonResponse = jsonSerializer.Deserialize<Dictionary<string, dynamic>>(response);
            HeartbeatInterval = jsonResponse["interval"];
            return jsonResponse["fileID"];
        }
    }
}
