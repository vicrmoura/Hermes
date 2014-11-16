using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class FileDownloadingInfo
    {
        public List<Dictionary<string, dynamic>> peersInfo; // ip, port, peerId
        public HFile file;
        public P2PDownloader downloader; // downloader (resposible for handling the hard disk accesses)
        public List<P2PClient> clients; // current connected clients
    }

    class DownloadManager
    {
        private Dictionary<string /*fileId*/, FileDownloadingInfo> filesInfo;
        private string myId;
        P2PServer server;

        public DownloadManager(string myId, P2PServer server)
        {
            this.myId = myId;
            this.server = server;
            filesInfo = new Dictionary<string, FileDownloadingInfo>();
        }

        public bool startDownload(HFile file, List<Dictionary<string, dynamic>> peers)
        {
            lock (filesInfo)
            {
                if (filesInfo.ContainsKey(file.ID))
                {
                    return false;
                }
                FileDownloadingInfo fileInfo = new FileDownloadingInfo();
                fileInfo.peersInfo = peers;
                fileInfo.downloader = new P2PDownloader(file.ID, file, server);
                fileInfo.file = file;
                fileInfo.clients = new List<P2PClient>();
                foreach (var peer in peers)
                {
                    P2PClient client = new P2PClient(myId, peer["peerId"], fileInfo.downloader, peer["ip"], int.Parse(peer["port"]));
                    fileInfo.clients.Add(client);
                }
            }
            return true;            
        }

        public void pauseDownload(string fileId)
        {
            lock(filesInfo) 
            {
                if (!filesInfo.ContainsKey(fileId))
                {
                    return;
                }
                foreach (var client in filesInfo[fileId].clients)
                {
                    client.pause();
                }
            }   
            
        }

        public void continueDownload(string fileId)
        {
            lock (filesInfo)
            {
                if (!filesInfo.ContainsKey(fileId))
                {
                    return;
                }
                foreach (var client in filesInfo[fileId].clients)
                {
                    client.unpause();
                }
            }   
        }

        // should be called during heartbeat
        public void updatePeers(string fileId, List<Dictionary<string, dynamic>> peers)
        {
            lock (filesInfo)
            {
                if (!filesInfo.ContainsKey(fileId))
                {
                    return;
                }
                filesInfo[fileId].peersInfo = peers;
            }
        }

        public void cancel(string fileId)
        {
            FileDownloadingInfo fileInfo;
            lock (filesInfo)
            {
                if (!filesInfo.ContainsKey(fileId))
                {
                    return;
                }
                fileInfo = filesInfo[fileId];
                filesInfo.Remove(fileId);
            }
            foreach (var client in fileInfo.clients)
            {
                client.cancel();
            }
            fileInfo.downloader.cancel();
        }
    }
}
