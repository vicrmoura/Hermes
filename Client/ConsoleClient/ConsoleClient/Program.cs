using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using TSSA = System.Tuple<string, string, System.Action>;

namespace Hermes
{
    class Program
    {
        /* Constants */

        private const string spaces = "\n                         ";
        private static readonly IReadOnlyDictionary<string, TSSA> options = new ReadOnlyDictionary<string, TSSA>(new Dictionary<string, TSSA>
        {
            { "help", new TSSA("", "Show available commands and version", ExecuteHelp) },
            { "upload", new TSSA("", "Crawl BaseFolder and upload metainfos for not-yet-" + spaces + "tracked files", ExecuteUpload) },
            { "search", new TSSA("<string>", "Search a file containing <string> in database", ExecuteSearch) },
            { "list", new TSSA("[<filter>]", "List files info: file name, fileID, size, percentage" + spaces + "completed, amount of running threads, state (=filter)." + spaces + "Filters (subset of): {completed, downloading, paused}", ExecuteList) },
            { "stats", new TSSA("", "Statistics: total downloading files, total uploading" + spaces + "files, total connections", ExecuteStats) },
            { "base", new TSSA("[<path>]", "Show [or set] BaseFolder", ExecuteBase) },
            { "ip", new TSSA("[<ip:port>]", "Show [or set] local IP and port", ExecuteIP) },
            { "download", new TSSA("<fileID>", "Start downloading <fileID>", ExecuteExecutewnload) },
            { "pause", new TSSA("<fileID>", "Pause downloading <fileID>", ExecutePause) },
            { "continue", new TSSA("<fileID>", "Continue downloading <fileID>", ExecuteContinue) },
            { "cancel", new TSSA("<fileID>", "Cancel download of <fileID>", ExecuteCancel) },
            { "quit", new TSSA("", "Close gracefully all connections and quit program", ExecuteQuit) },
        });

        /* Fields */

        private static string[] input;
        private static bool quit = false;

        /* Main */

        static void Main(string[] args)
        {
            Console.WriteLine("Hermes Console Client v0.1");
            
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

        /* Methods */

        private static void ExecuteUpload()
        {
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
                Console.WriteLine("Results for " + input[1] + ":");
                Console.WriteLine("...");
            }
        }

        private static void ExecuteList()
        {
            Console.WriteLine("|--- Name ---|--- ID ---|--- size ---|--- % completed ---|");
            Console.WriteLine("...");
        }

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
                Console.WriteLine(@"Current BaseFolder: C:\Users\User\Folder");
            }
            else
            {
                Console.WriteLine(@"Old BaseFolder: C:\Users\User\Folder");
                Console.WriteLine("New BaseFolder: " + input[1]);
            }
        }

        private static void ExecuteIP()
        {
            if (input.Length == 1)
            {
                Console.WriteLine("Current IP:   161.24.24.200");
                Console.WriteLine("Current port: 30403");
            }
            else
            {
                string IP, port;
                if (TryParseIPPort(input[1], out IP, out port))
                {
                    Console.WriteLine("Old IP:   161.24.24.200");
                    Console.WriteLine("Old port: 30403");

                    Console.WriteLine("New IP:   " + IP);
                    Console.WriteLine("New port: " + port);
                }
                else
                {
                    Console.WriteLine("IP port in wrong format");
                }
            }
        }

        private static void ExecuteExecutewnload()
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

        private static void ExecuteQuit()
        {
            Console.WriteLine("Closing connections...");
            quit = true;
        }

        private static bool TryParseIPPort(string input, out string IP, out string port)
        {
            IP = "";
            port = "";

            if (input.Contains(' ') || input.Contains('-'))
            {
                return false;
            }

            string[] parts = input.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            IP = parts[0];
            port = parts[1];

            ushort portNum;
            if (!ushort.TryParse(port, out portNum))
            {
                return false;
            }

            parts = IP.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            byte b;
            return parts.All(part => byte.TryParse(part, out b));
        }
    }
}
