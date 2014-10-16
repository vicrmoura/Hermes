using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hermes
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hermes Console Client v0.1");
            
            string[] input;
            string command;
            do {
                Console.Write("> ");
                input = Console.ReadLine().Trim().Split(new[]{' '}, 2);

                command = input[0];
                switch (command)
                {
                    case "upload":
                        Console.WriteLine("Started to crawl BaseFolder");
                        break;
                    case "search":
                        if (input.Length == 1)
                        {
                            Console.WriteLine("Cannot search empty string");
                        }
                        else
                        {
                            Console.WriteLine("Results for " + input[1] + ":");
                            Console.WriteLine("...");
                        }
                        break;
                    case "list":
                        Console.WriteLine("|--- Name ---|--- ID ---|--- size ---|--- % completed ---|");
                        Console.WriteLine("...");
                        break;
                    case "stats":
                        Console.WriteLine("Downloading: x files");
                        Console.WriteLine("Uploading:   y files");
                        Console.WriteLine("...");
                        break;
                    case "base":
                        if (input.Length == 1)
                        {
                            Console.WriteLine(@"Current BaseFolder: C:\Users\User\Folder");
                        }
                        else
                        {
                            Console.WriteLine(@"Old BaseFolder: C:\Users\User\Folder");
                            Console.WriteLine("New BaseFolder: " + input[1]);
                        }
                        break;
                    case "ip":
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
                        break;
                    case "download":
                        if (input.Length == 2)
                        {
                            Console.WriteLine("Started download of " + input[1]);
                        }
                        else
                        {
                            Console.WriteLine("Missing fileID");
                        }
                        break;
                    case "pause":
                        if (input.Length == 2)
                        {
                            Console.WriteLine("Pausing download of " + input[1]);
                        }
                        else
                        {
                            Console.WriteLine("Missing fileID");
                        }
                        break;
                    case "continue":
                        if (input.Length == 2)
                        {
                            Console.WriteLine("Continuing download of " + input[1]);
                        }
                        else
                        {
                            Console.WriteLine("Missing fileID");
                        }
                        break;
                    case "help":
                        Console.WriteLine(@"Available commands:
 * help                  Show available commands and version
 * upload                Crawl BaseFolder and upload metainfos for not-yet-
                         tracked files
 * search    <string>    Search a file containing <string> in database
 * list      [<filter>]  List files info: file name, fileID, size, percentage
                         completed, amount of running threads, state (=filter).
                         Filters (subset of): {completed, downloading, paused}
 * stats                 Statistics: total downloading files, total uploading
                         files, total connections
 * base      [<path>]    Show [or set] BaseFolder
 * ip        [<ip:port>] Show [or set] local IP and port
 * download  <fileID>    Start downloading <fileID>
 * pause     <fileID>    Pause downloading <fileID>
 * continue  <fileID>    Continue downloading <fileID>
 * quit                  Close gracefully all connections and quit program");
                        break;
                    case "quit":
                        Console.WriteLine("Closing connections...");
                        break;
                    case "":
                        break;
                    default:
                        Console.WriteLine("Unkown command: " + command);
                        break;
                }
            } while(command != "quit");
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
