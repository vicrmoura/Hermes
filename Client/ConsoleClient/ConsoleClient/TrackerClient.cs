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
        private bool newPortSpecified = false;
        private string trackerPort;
        public string TrackerPort
        {
            get { return trackerPort; }
            set { trackerPort = value; newPortSpecified = true; }
        }
        private bool newIPSpecified = false;
        private string trackerIP;
        public string TrackerIP
        {
            get { return trackerIP; }
            set { trackerIP = value; newIPSpecified = true; }
        }

        private TcpClient tcpClient;
        private JavaScriptSerializer jsonSerializer;
        private static readonly string TAG = "TrackerClient";

        public TrackerClient(string ip, string port)
        {
            TrackerIP = ip;
            TrackerPort = port;
            jsonSerializer = new JavaScriptSerializer();
        }
        
        private void sendMessage(string data)
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
                try
                {
                    StreamWriter sw = new StreamWriter(tcpClient.GetStream());
                    sw.AutoFlush = true;
                    Logger.log(TAG, "Ready to send data");
                    sw.WriteLine(data);
                    Logger.log(TAG, "Data sent");
                    sw.Close();
                }
                catch (Exception e)
                {
                    Logger.log(TAG, "[Error] There was a problem during tracker server communication. Message: " + e.Message);
                }
            }
            catch (Exception e)
            {
                Logger.log(TAG, "[Error] Cannot connect to tracker server. Message: " + e.Message);
                Console.Write(e.Message);
            }
        }

        public void uploadMetaInfo(HFile file, byte[][] sha1s, string peerID, string peerIP, string peerPort)
        {
            var dict = new Dictionary<string, dynamic> {
                 {"type", "upload"},
                 {"fileName", file.Name},
                 {"size", file.Size}, 
                 {"pieceSize", file.PieceSize},
                 {"blockSize", file.BlockSize},
                 {"piecesSHA1S", sha1s},
                 {"peerID", peerID},
                 {"port", peerIP},
                 {"ip", peerPort}
            };
            Logger.log(TAG, "Starting upload of " + file.Name);
            sendMessage(jsonSerializer.Serialize(dict));
        }
    }
}
