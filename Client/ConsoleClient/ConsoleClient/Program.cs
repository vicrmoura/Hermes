using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
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
            { "stats",    new TSSA("",            "Statistics: total downloading files, total uploading" + spaces + "files, total connections", ExecuteStats) },
            { "base",     new TSSA("[<path>]",    "Show [or set] BaseFolder", ExecuteBase) },
            { "ip",       new TSSA("[<ip:port>]", "Show [or set] local IP and port", ExecuteIP) },
            { "download", new TSSA("<fileID>",    "Start downloading <fileID>", ExecuteDownload) },
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
        
        private static string TrackerIP;
        private static string TrackerPort;
        private static string LocalIP;
        private static string LocalPort;
        private static string BaseFolder;
        private static string PeerId;
        #endregion

        #region /* Main */
        static void Main(string[] args)
        {
            Console.WriteLine("Hermes Console Client v0.1");

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

            // Read Settings

            Console.Write(string.Format(" * {0,-30}", "Read Settings"));
            LocalIP = ConfigurationManager.AppSettings["LocalIP"];
            LocalPort = ConfigurationManager.AppSettings["LocalPort"];
            TrackerIP = ConfigurationManager.AppSettings["TrackerIP"];
            TrackerPort = ConfigurationManager.AppSettings["TrackerPort"];
            BaseFolder = ConfigurationManager.AppSettings["BaseFolder"];
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
            
            // Initializing file manager (faz isso a onde vc quiser croata)

            FileManager fileManager = new FileManager();

            // Start p2p-server

            Console.Write(string.Format(" * {0,-30}", "Start p2p-server"));
            P2PServer p2pServer = new P2PServer(PeerId, fileManager);
            var downloader = new P2PDownloader(fileManager, "file");
            P2PClient p2pClient = new P2PClient("Harry", downloader, "127.0.0.1", P2PServer.SERVER_PORT, fileManager);
            Console.WriteLine("[OK]");

            // Start crawling BaseFolder

            Console.Write(string.Format(" * {0,-30}", "Start crawling BaseFolder"));
            StartCrawlingBaseFolder();
            Console.WriteLine("[OK]");

            // Start heartbeat

            Console.Write(string.Format(" * {0,-30}", "Start heartbeat"));
            // TODO: Start heartbeat
            Console.WriteLine("[OK]");
        }

        private static void StartCrawlingBaseFolder()
        {
            var fileNames = new HashSet<string>();
            foreach (var file in files.Values)
            {
                fileNames.Add(file.Name);
            }
            SHA1 shaConverter = new SHA1CryptoServiceProvider();
            TrackerClient client = new TrackerClient(TrackerIP, TrackerPort);
            foreach (string filePath in Directory.EnumerateFiles(BaseFolder))
            {
                string fileName = Path.GetFileName(filePath);
                if (!fileNames.Contains(fileName)) // fileName is not yet synchronized by this program => create metainfo then update it to tracker
                {
                    HFile file = new HFile();
                    file.Name = fileName;
                    file.Status = StatusType.Completed;
                    file.PercentageSpecified = false;
                    file.Size = new FileInfo(filePath).Length;
                    file.PieceSize = (int)Math.Max(2*kB, file.Size/(10*kB));
                    file.BlockSize = Math.Max(1*kB, file.PieceSize / 100);

                    FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    byte[] buffer = new byte[file.PieceSize];
                    int N = (int)Math.Ceiling(1.0 * file.Size/file.PieceSize);
                    file.Pieces = new Piece[N];
                    for (int i = 0; i < N; i++)
                    {
                        int bytesRead = fs.Read(buffer, 0, file.PieceSize);
                        byte[] sha = shaConverter.ComputeHash(buffer, 0, bytesRead);
                        file.Pieces[i] = new Piece(Convert.ToBase64String(sha));
                        if (bytesRead != file.PieceSize)
                        {
                            file.Pieces[i].Size = bytesRead;
                        }
                    }
                    string fileID = client.UploadMetaInfo(file, null, PeerId, LocalIP, LocalPort);
                    file.ID = fileID;
                    files[fileID] = file;
                }
            }
        }
        #endregion

        #region /* Execute Methods */
        private static void ExecuteUpload()
        {
            StartCrawlingBaseFolder();
            Console.WriteLine("Started to crawl BaseFolder");
        }

        // TODO: ExecuteSearch
        private static void ExecuteSearch()
        {
            if (input.Length == 1)
            {
                Console.WriteLine("Cannot search empty string");
            }
            else
            {
                Console.WriteLine("Results for " + input[1] + ":");
                Console.WriteLine("...");
            }
        }

        // TODO: ExecuteList
        private static void ExecuteList()
        {
            Console.WriteLine("|--- Name ---|--- ID ---|--- size ---|--- % completed ---|");
            Console.WriteLine("...");
        }

        // TODO: ExecuteStats
        private static void ExecuteStats()
        {
            Console.WriteLine("Downloading: x files");
            Console.WriteLine("Uploading:   y files");
            Console.WriteLine("...");
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
                Console.WriteLine("Current port: 30403");
            }
            else
            {
                Console.WriteLine("Old IP:   " + LocalIP);
                Console.WriteLine("Old port: 30403");

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

        // TODO: ExecuteExecutewnload
        private static void ExecuteDownload()
        {
            if (input.Length == 2)
            {
                Console.WriteLine("Started download of " + input[1]);
            }
            else
            {
                Console.WriteLine("Missing fileID");
            }
        }

        // TODO: ExecutePause
        private static void ExecutePause()
        {
            if (input.Length == 2)
            {
                Console.WriteLine("Pausing download of " + input[1]);
            }
            else
            {
                Console.WriteLine("Missing fileID");
            }
        }

        // TODO: ExecuteContinue
        private static void ExecuteContinue()
        {
            if (input.Length == 2)
            {
                Console.WriteLine("Continuing download of " + input[1]);
            }
            else
            {
                Console.WriteLine("Missing fileID");
            }
        }

        // TODO: ExecuteCancel
        private static void ExecuteCancel()
        {
            if (input.Length == 2)
            {
                Console.WriteLine("Canceling download of " + input[1]);
            }
            else
            {
                Console.WriteLine("Missing fileID");
            }
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
            Console.WriteLine("Closing connections...");
            quit = true;
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
    }
}
