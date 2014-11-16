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
        bool quitted;

        public DownloadManager(string myId, P2PServer server)
        {
            this.myId = myId;
            this.server = server;
            filesInfo = new Dictionary<string, FileDownloadingInfo>();
            quitted = false;
            Task.Run(() => manageCompletedDownloads());
        }

        public void quit(){
            quitted = true;
        }

        public void manageCompletedDownloads()
        {
            while (!quitted)
            {
                lock (filesInfo)
                {
                    List<string> toBeRemoved = new List<string>();
                    foreach (var fileInfo in filesInfo)
                    {
                        var file = fileInfo.Value.file;

                        lock (file)
                        {
                            if (file.Status == StatusType.Completed)
                            {
                                toBeRemoved.Add(fileInfo.Key);
                            }
                        }
                    }
                    foreach (var key in toBeRemoved)
                    {
                        filesInfo.Remove(key);
                        Logger.log("DOWNLOADMANAGER","Completed downloading " + key);
                    }
                }
                System.Threading.Thread.Sleep(10000);
            }
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
                    P2PClient client = new P2PClient(myId, peer["peerID"], fileInfo.downloader, peer["ip"], int.Parse(peer["port"]));
                    fileInfo.clients.Add(client);
                }
                filesInfo[file.ID] = fileInfo;
            }
            return true;            
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

        private string getPossibilitiesString(List<string> possibilities)
        {
            string ans = "Please specify file. Possibilities: ";
            bool first = true;
            foreach (var possibility in possibilities)
            {
                if (first) { first = false; }
                else { ans += ", "; }
                ans += possibility;
            }
            return ans;
        }

        // not thread safe
        public Tuple<bool, string> evaluatePartialFileId(string partialFileId)
        {
            var possibilities = filesInfo.Keys.Where(k => k.StartsWith(partialFileId)).ToList();
            if (possibilities.Count == 0)
            {
                return Tuple.Create(false, "No such file");
            }
            if (possibilities.Count > 1)
            {
                return Tuple.Create(false, getPossibilitiesString(possibilities));
            }
            return Tuple.Create(true, possibilities[0]);
        }

        public string cancel(string partialFileId)
        {
            FileDownloadingInfo fileInfo;
            lock (filesInfo)
            {
                var t = evaluatePartialFileId(partialFileId);
                if (!t.Item1) return t.Item2;
                string fileId = t.Item2;
                fileInfo = filesInfo[fileId];
                filesInfo.Remove(fileId);
            }
            lock (fileInfo.file)
            {
                if (fileInfo.file.Status == StatusType.Completed)
                {
                    return "Download already completed";
                }
            }
            lock (fileInfo.file)
            {
                fileInfo.file.Status = StatusType.Canceled;
            }
            foreach (var client in fileInfo.clients)
            {
                client.cancel();
            }
            fileInfo.downloader.cancel();
            return "Canceling download of " + fileInfo.file.ID;
        }

        public string pauseDownload(string partialFileId)
        {
            string fileId;
            lock (filesInfo)
            {
                var t = evaluatePartialFileId(partialFileId);
                if (!t.Item1) return t.Item2;
                fileId = t.Item2;
                foreach (var client in filesInfo[fileId].clients)
                {
                    client.pause();
                }
            }
            lock (filesInfo[fileId].file)
            {
                if (filesInfo[fileId].file.Status == StatusType.Completed)
                {
                    return "Download already completed";
                }
            }
            lock (filesInfo[fileId].file)
            {
                filesInfo[fileId].file.Status = StatusType.Paused;
            }
            return "Pausing download of " + fileId;
        }

        public string continueDownload(string partialFileId)
        {
            string fileId;
            lock (filesInfo)
            {
                var t = evaluatePartialFileId(partialFileId);
                if (!t.Item1) return t.Item2;
                fileId = t.Item2;
                foreach (var client in filesInfo[fileId].clients)
                {
                    client.unpause();
                }
            }
            lock (filesInfo[fileId].file)
            {
                if (filesInfo[fileId].file.Status == StatusType.Completed)
                {
                    return "Download already completed";
                }
            }
            lock (filesInfo[fileId].file)
            {
                filesInfo[fileId].file.Status = StatusType.Downloading;
            }
            return "Continuing download of " + fileId;
        }
    }
}
