﻿using System;
using System.Net;
using System.Net.NetworkInformation; 
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TSSA = System.Tuple<string, string, System.Action>;

namespace Hermes
{
    class Program
    {
        #region /* Constants */
        private const int kB = 1024;
        private const string spaces = "\n                         ";
        private static readonly IReadOnlyDictionary<string, TSSA> options = new ReadOnlyDictionary<string, TSSA>(new Dictionary<string, TSSA>
        {
            { "help",     new TSSA("",            "Show available commands and version", ExecuteHelp) },
            { "upload",   new TSSA("",            "Crawl BaseFolder and upload metainfos for not-yet-" + spaces + "tracked files", ExecuteUpload) },
            { "search",   new TSSA("<string>",    "Search a file containing <string> in database", ExecuteSearch) },
            { "list",     new TSSA("[<filter>]",  "List files info: file name, fileID, size, percentage" + spaces + "completed, amount of running threads, state (=filter)." + spaces + "Filters (subset of): {completed, downloading, paused}", ExecuteList) },
            { "base",     new TSSA("[<path>]",    "Show [or set] BaseFolder", ExecuteBase) },
            { "ip",       new TSSA("[<ip:port>]", "Show [or set] local IP and port", ExecuteIP) },
            { "pause",    new TSSA("<fileID>",    "Pause downloading <fileID>", ExecutePause) },
            { "continue", new TSSA("<fileID>",    "Continue downloading <fileID>", ExecuteContinue) },
            { "cancel",   new TSSA("<fileID>",    "Cancel download of <fileID>", ExecuteCancel) },
            { "quit",     new TSSA("",            "Close gracefully all connections and quit program", ExecuteQuit) },
        });
        #endregion

        #region /* Fields */
        private static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        private static string[] input;
        private static bool quit = false;
        private static Dictionary<string, HFile> files;
        private static TrackerClient trackerClient;
        private static Dictionary<string, dynamic>[] searchResults;
        private static DownloadManager downloadManager;

        private static string TrackerIP;
        private static string TrackerPort;
        private static string LocalIP;
        private static string LocalPort;
        public static string BaseFolder;
        private static string PeerId;

        #endregion

        #region /* Main */
        static void Main(string[] args)
        {
            Console.WriteLine("Hermes Console Client v0.5");

            Initialize();

            do {
                Console.Write(">>> ");
                input = Console.ReadLine().Trim().Split(new[]{' '}, 2);
                string command = input[0];
                if (command == "") continue;

                var possibilities = options.Keys.Where(k => k.StartsWith(command)).ToList();
                if (possibilities.Count == 0)
                {
                    Console.WriteLine("Unknown command: " + command);
                }
                else if (possibilities.Count == 1)
                {
                    options[possibilities[0]].Item3.Invoke();
                }
                else
                {
                    Console.WriteLine("Possible commands: " + string.Join(" ", possibilities));
                }
            } while(!quit);
        }
        #endregion

        #region /* Methods */

        #region /* Other methods */
        private static void Initialize()
        {
            Console.WriteLine("Initializing...");

            // Gracefully quit
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);

            // Read Settings

            Console.Write(string.Format(" * {0,-30}", "Read Settings"));

            LocalIP = LocalIPAddress();
            LocalPort = ConfigurationManager.AppSettings["LocalPort"];
            TrackerIP = ConfigurationManager.AppSettings["TrackerIP"];
            TrackerPort = ConfigurationManager.AppSettings["TrackerPort"];
            BaseFolder = ConfigurationManager.AppSettings["BaseFolder"];
            if (!Directory.Exists(BaseFolder))
            {
                Directory.CreateDirectory(BaseFolder);
            }
            PeerId = ConfigurationManager.AppSettings["PeerId"];
            if (PeerId == "")
            {
                PeerId = Guid.NewGuid().ToString();
                config.AppSettings.Settings["PeerId"].Value = PeerId;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
            }

            Console.WriteLine("[OK]");

            // Load database

            Console.Write(string.Format(" * {0,-30}", "Load database"));
            if (File.Exists("database.xml"))
            {
                using (StreamReader sr = new StreamReader("database.xml"))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(HFile[]), new XmlRootAttribute() { ElementName = "files" });
                    files = ((HFile[])serializer.Deserialize(sr)).ToDictionary(file => file.ID);
                }
            }
            else
            {
                files = new Dictionary<string, HFile>();
            }
            Console.WriteLine("[OK]");

            // Initializing TrackerClient

            trackerClient = new TrackerClient(TrackerIP, TrackerPort);

            // Start crawling BaseFolder

            Console.Write(string.Format(" * {0,-30}", "Start crawling BaseFolder"));
            StartCrawlingBaseFolder();
            Console.WriteLine("[OK]");

            // Start p2p-server

            Console.Write(string.Format(" * {0,-30}", "Start p2p-server"));
            P2PServer p2pServer = new P2PServer(PeerId, files, int.Parse(LocalPort));
            Console.WriteLine("[OK]");

            // Start download manager

            downloadManager = new DownloadManager(PeerId, p2pServer);

            // Start heartbeat
            
            Console.Write(string.Format(" * {0,-30}", "Start heartbeat"));
            Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(trackerClient.HeartbeatInterval);
                    if (files.Count > 0)
                    {
                        Dictionary<string, dynamic> fileUpdates;
                        try
                        {
                            fileUpdates = trackerClient.Heartbeat(files, PeerId, LocalIP, LocalPort);
                            foreach (var fileID in fileUpdates.Keys) {
                                downloadManager.updatePeers(fileID, ((ArrayList)fileUpdates[fileID]).Cast<Dictionary<string, dynamic>>().ToList());
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.log("HEARTBEAT", "[ERROR] " + e.ToString());
                        }
                        
                    }
                }
            });          
            Console.WriteLine("[OK]");

            Task.Run(() => manageCanceledDownloads());
        }
        
        private static string LocalIPAddress()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var addr = ni.GetIPProperties().GatewayAddresses.FirstOrDefault();
                if (addr != null && !addr.Address.ToString().Equals("0.0.0.0"))
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
            }
            return String.Empty;
    
        }

        private static void StartCrawlingBaseFolder()
        {
            var fileNames = new HashSet<string>();
            foreach (var file in files.Values)
            {
                fileNames.Add(file.Name);
            }
            SHA1 shaConverter = new SHA1CryptoServiceProvider();
            foreach (string filePath in Directory.EnumerateFiles(BaseFolder))
            {
                string fileName = Path.GetFileName(filePath);
                // fileName is not yet synchronized by this program => create metainfo then update it to tracker
                if (!fileNames.Contains(fileName) && fileName.Split('.').Last() != "downloading")
                {
                    HFile file = new HFile();
                    file.Name = fileName;
                    file.Status = StatusType.Completed;
                    file.Percentage = 1.0;
                    file.Size = new FileInfo(filePath).Length;
                    file.PieceSize = (int)Math.Max(100*kB, file.Size/(10*kB)/100*100);
                    file.BlockSize = Math.Max(10*kB, file.PieceSize / 100);

                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buffer = new byte[file.PieceSize];
                        int N = (int)Math.Ceiling(1.0 * file.Size / file.PieceSize);
                        file.Pieces = new Piece[N];
                        file.BitField = new string('1', N);
                        for (int i = 0; i < N; i++)
                        {
                            int bytesRead = fs.Read(buffer, 0, file.PieceSize);
                            byte[] sha = shaConverter.ComputeHash(buffer, 0, bytesRead);
                            file.Pieces[i] = new Piece() { Sha = Convert.ToBase64String(sha) };
                            file.Pieces[i].Size = bytesRead;
                            file.Pieces[i].BitField = new string('1', (int)Math.Ceiling(1.0 * bytesRead / file.BlockSize));
                        }
                        try
                        {
                            string fileID = trackerClient.UploadMetaInfo(file, PeerId, LocalIP, LocalPort);
                            file.ID = fileID;
                            files[fileID] = file;
                        }
                        catch (IOException e)
                        {
                            Logger.log("CRAWLER", e.Message);
                        }
                    }
                }
                else if (fileNames.Contains(fileName))
                {
                    fileNames.Remove(fileName);
                }
            }
            var fileIDs = new List<string>();
            foreach (var kv in files)
            {
                if (fileNames.Contains(kv.Value.Name))
                {
                    fileIDs.Add(kv.Key);
                }
            }
            foreach (var fileID in fileIDs)
            {
                files.Remove(fileID);
            }
        }

        private static void RunSearchCommandLine()
        {
            uint limit = 10, offset = 0;
            uint page = 1;
            try
            {
                searchResults = trackerClient.Query(input[1], limit, offset);
            }
            catch (Exception e)
            {
                Console.WriteLine("There was a problem with the connection to the server");
                Logger.log("SEARCH", "[ERROR] " + e.ToString());
                return;
            } 
            bool printTable = true;
            string command;
            do
            {
                if (searchResults.Length > 0)
                {
                    if (printTable)
                    {
                        Console.WriteLine("Page " + page + " of results for " + input[1] + ":\n");
                        // TODO (croata): alinhar
                        Console.WriteLine("|- ID -|--------------------- Name ---------------------|-- Size --|- Peers -|");
                        for (int i = 0; i < searchResults.Length; i++)
                        {
                            Console.WriteLine(String.Format(
                                "| {0,4} | {1,-46} | {2,8} | {3,7} |",
                                i + 1,
                                searchResults[i]["name"].Substring(0, Math.Min(46, searchResults[i]["name"].Length)),
                                SizeToString(searchResults[i]["size"]),
                                searchResults[i]["numOfPeers"]));
                        }
                        Console.WriteLine("\nn: next page, p: previous page, <id>: download file <id>, q: quit");
                    }
                    Console.Write("S> ");
                    command = Console.ReadLine();
                    if (command.Equals("n"))
                    {
                        if (searchResults.Length < limit)
                        {
                            Console.WriteLine("This is the last page");
                            printTable = false;
                            continue;
                        }
                        page++;
                        offset = limit * (page - 1);
                        Dictionary<string, dynamic>[] newSearchResults = null;
                        try
                        {
                            newSearchResults = trackerClient.Query(input[1], limit, offset);
                        }
                        catch (Exception e) 
                        {
                            Console.WriteLine("There was a problem with the connection to the server");
                            Logger.log("SEARCH", "[ERROR] " + e.ToString());
                            break;
                        }
                        if (newSearchResults.Length == 0)
                        {
                            Console.WriteLine("This is the last page");
                            printTable = false;
                            continue;
                        }
                        else
                        {
                            searchResults = newSearchResults;
                            printTable = true;
                            continue;
                        }
                    }
                    if (command.Equals("p"))
                    {
                        if (page == 1)
                        {
                            Console.WriteLine("This is the first page");
                            printTable = false;
                            continue;
                        }
                        page--;
                        offset = limit * (page - 1);
                        searchResults = trackerClient.Query(input[1], limit, offset);
                        printTable = true;
                        continue;
                    }
                    if (command.Equals("q"))
                    {
                        break;
                    }
                    uint id = 0;
                    if (!UInt32.TryParse(command, out id) || id < offset + 1 || id > searchResults.Length)
                    {
                        Console.WriteLine("Invalid command");
                        printTable = false;
                        continue;
                    }
                    try
                    {
                        ExecuteDownload(id - 1);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to download file");
                        Logger.log("SEARCH" , "[ERROR] " + e.ToString());
                    }
                    printTable = false;
                    break;
                }
                else
                {
                    Console.WriteLine("No results where found for " + input[1]);
                    break;
                }
            } while (true);
        }

        private static string SizeToString(long size)
        {
            string[] suffixes = { "B", "kB", "MB", "GB" };
            foreach (var suffix in suffixes)
            {
                if (size < kB)
                {
                    return size + suffix;
                }
                size /= kB;
            }
            return size + "TB";
        }
        #endregion

        #region /* Execute Methods */
        private static void ExecuteUpload()
        {
            StartCrawlingBaseFolder();
            Console.WriteLine("Started to crawl BaseFolder");
        }

        private static void ExecuteSearch()
        {
            if (input.Length == 1)
            {
                Console.WriteLine("Cannot search empty string");
            }
            else
            {
                RunSearchCommandLine();
            }
        }

        private static void ExecuteList()
        {
            Console.WriteLine("|------------------ Name ------------------|-- Size --|-- % --|--- Status ---|");
            foreach (var hfile in files.Values)
            {
                Console.Write(string.Format("| {0,-40} ", hfile.Name.Substring(0, Math.Min(40, hfile.Name.Length))));
                Console.Write(string.Format("| {0,8} ", SizeToString(hfile.Size)));
                Console.Write(string.Format("| {0,5:0.0} ", hfile.Percentage*100));
                Console.WriteLine(string.Format("| {0,12} |", hfile.Status));
            }
        }

        private static void ExecuteBase()
        {
            if (input.Length == 1)
            {
                Console.WriteLine(@"Current BaseFolder: " + BaseFolder);
            }
            else
            {
                Console.WriteLine(@"Old BaseFolder: " + BaseFolder);
                Console.WriteLine("New BaseFolder: " + input[1]);

                BaseFolder = input[1];
                config.AppSettings.Settings["BaseFolder"].Value = BaseFolder;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);

                StartCrawlingBaseFolder();
                Console.WriteLine("Started to crawl BaseFolder");
            }
        }

        private static void ExecuteIP()
        {
            if (input.Length == 1)
            {
                Console.WriteLine("Current IP:   " + LocalIP);
                Console.WriteLine("Current port: " + LocalPort);
            }
            else
            {
                Console.WriteLine("Old IP:   " + LocalIP);
                Console.WriteLine("Old port: " + LocalPort);

                if (TryParseIPPort(input[1], ref LocalIP, ref LocalPort))
                {
                    Console.WriteLine("New IP:   " + LocalIP);
                    Console.WriteLine("New port: " + LocalPort);

                    config.AppSettings.Settings["LocalIP"].Value = LocalIP;
                    config.AppSettings.Settings["LocalPort"].Value = LocalPort;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
                }
                else
                {
                    Console.WriteLine("** IP:port in wrong format **");
                }
            }
        }

        private static void manageCanceledDownloads()
        {
            while (!quit)
            {
                lock (files)
                {
                    List<string> toBeRemoved = new List<string>();
                    foreach (var file in files)
                    {

                        lock (file.Value)
                        {
                            if (file.Value.Status == StatusType.Canceled)
                            {
                                toBeRemoved.Add(file.Key);
                            }
                        }
                    }
                    foreach (var key in toBeRemoved)
                    {
                        files.Remove(key);
                        Logger.log("CANCELMANAGER", "Canceled downloading " + key);
                    }
                }
                System.Threading.Thread.Sleep(10000);
            }
        }

        private static void ExecuteDownload(uint id)
        {
            string fileId = searchResults[id]["fileID"];
            var response = trackerClient.GetMetaInfo(fileId, PeerId);

            Console.WriteLine("File Id:" + fileId);
            Console.WriteLine("PeerId:" + PeerId);
            if (files.ContainsKey(fileId))
            {
                lock (files[fileId])
                {
                    if (files[fileId].Status == StatusType.Downloading)
                    {
                        Console.WriteLine("This file is already being downloaded");
                        return;
                    }
                    else if (files[fileId].Status == StatusType.Completed)
                    {
                        Console.WriteLine("This file was already downloaded");
                        return;
                    }
                    else if (files[fileId].Status == StatusType.Paused)
                    {
                        Console.WriteLine("This file is paused... You can continued the download by typing 'continue <id>'");
                        return;
                    }
                    else if (files[fileId].Status == StatusType.Canceled)
                    {
                        Console.WriteLine("This download was previously canceled. Try again later.");
                        return;
                    }
                }
            }

            bool started = downloadManager.startDownload(response.Item1, response.Item2.ToList());
            if (started)
            {
                lock (files)
                {
                    files[fileId] = response.Item1;
                }
                Console.WriteLine("Started download of " + fileId);
            }
            else
            {
                Console.WriteLine("This file is already being downloaded");
            }
        }

        private static void ExecutePause()
        {
            if (input.Length != 2)
            {
                Console.WriteLine("Missing fileID");
                return;
            }

            string message = downloadManager.pauseDownload(input[1]);
            Console.WriteLine(message);
        }

        private static void ExecuteContinue()
        {
            if (input.Length != 2)
            {
                Console.WriteLine("Missing fileID");
                return;
            }

            string message = downloadManager.continueDownload(input[1]);
            Console.WriteLine(message);
        }

        private static void ExecuteCancel()
        {
            if (input.Length != 2)
            {
                Console.WriteLine("Missing fileID");
                return;
            }
            string message = downloadManager.cancel(input[1]);
            Console.WriteLine(message);
        }

        private static void ExecuteHelp()
        {
            foreach (string command in options.Keys)
            {
                Console.WriteLine(string.Format(" * {0,-10}{1,-12}{2}", command, options[command].Item1, options[command].Item2));
            }
            Console.WriteLine();
        }

        // TODO: ExecuteQuit
        private static void ExecuteQuit()
        {
            if (quit) return;
            quit = true;

            Console.Write("Closing connections...  ");
            Console.WriteLine("[OK]");

            Console.Write("Saving database...      ");
            using (var sr = new StreamWriter("database.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(HFile[]), new XmlRootAttribute() { ElementName = "files" });
                serializer.Serialize(sr, files.Values.ToArray());
            }
            Console.WriteLine("[OK]");
        }

        private static bool TryParseIPPort(string input, ref string IP, ref string port)
        {
            if (input.Contains(' ') || input.Contains('-'))
            {
                return false;
            }

            string[] parts = input.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            ushort portNum;
            if (!ushort.TryParse(parts[1], out portNum))
            {
                return false;
            }

            string[] bytes = parts[0].Split('.');
            if (bytes.Length != 4)
            {
                return false;
            }

            byte b;
            if (!bytes.All(part => byte.TryParse(part, out b)))
            {
                return false;
            }

            IP = parts[0];
            port = parts[1];
            return true;
        }
        #endregion

        #endregion

        #region Gracefully Quit
        private delegate bool ConsoleEventDelegate(int eventType);

        private static ConsoleEventDelegate handler;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        private static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                ExecuteQuit();
            }
            return false;
        }
        #endregion
    }
}
