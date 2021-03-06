﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PDownloader
    {
        /* Constants */

        public const string DOWNLOADING = ".downloading";
        private const int MAX_TIME = 5; // seconds

        /* Fields */

        public readonly string FileID;
        private readonly HFile hfile;
        private readonly string filePath;
        private readonly P2PServer server;
        private readonly Dictionary<string/*peerId*/, BitArray> bitFields;
        private readonly HashSet<Tuple<int, int>> requestedBlocks;
        private readonly Dictionary<Tuple<int, int>, int> blockTimes;
        private readonly double[] numbers;
        private bool finished = false;

        /* Constructor */

        public P2PDownloader(string fileID, HFile hfile, P2PServer server)
        {
            this.FileID = fileID;
            this.hfile = hfile;
            this.server = server;
            this.filePath = Path.Combine(Program.BaseFolder, hfile.Name + DOWNLOADING);
            this.bitFields = new Dictionary<string, BitArray>();
            this.requestedBlocks = new HashSet<Tuple<int, int>>();
            this.blockTimes = new Dictionary<Tuple<int, int>, int>();
            this.numbers = new double[hfile.Pieces.Length];

            var rnd = new ThreadSafeRandom();
            for (int i = 0; i < hfile.Pieces.Length; i++)
            {
                numbers[i] = rnd.NextDouble();
            }

            lock (hfile)
            {
                if (!File.Exists(filePath))
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        stream.SetLength(hfile.Size);
                    }
                }
            }

            Task.Run(() =>
            {
                var toDelete = new List<Tuple<int, int>>();
                var blockTimesCopy = new Dictionary<Tuple<int, int>, int>();
                while (!finished)
                {
                    lock (requestedBlocks)
                    {
                        toDelete.Clear();
                        blockTimesCopy.Clear();
                        foreach (var block in blockTimes.Keys)
                        {
                            blockTimesCopy[block] = blockTimes[block] - 1;
                            if (blockTimesCopy[block] == 0)
                            {
                                toDelete.Add(block);
                            }
                        }
                        foreach (var block in toDelete)
                        {
                            blockTimesCopy.Remove(block);
                            requestedBlocks.Remove(block);
                        }
                        blockTimes.Clear();
                        foreach (var block in blockTimesCopy.Keys)
                        {
                            blockTimes[block] = blockTimesCopy[block];
                        }
                    }

                    System.Threading.Thread.Sleep(500);
                }
            });
        }

        /* Methods */

        public void SetBitField(string peerName, string bitField)
        {
            BitArray ba = new BitArray(bitField.Length, false);
            for (int i = 0; i < bitField.Length; i++)
            {
                ba[i] = (bitField[i] == '1');
            }
            lock (bitFields)
            {
				bitFields[peerName] = ba;
            }
        }

        public void ReceiveHave(string peerID, int pieceId)
        {
            lock (bitFields)
            {
                bitFields[peerID][pieceId] = true;
            }
        }

        /// <summary>
        /// The next (piece, block) to download. Returns null if finished
        /// </summary>
        /// <returns>The pair (piece, block)</returns>
        public Tuple<int, int> GetNextBlock(string peerId)
        {
            Dictionary<string, BitArray> bitFieldsCopy;
            string myBitField;
            BitArray possible;
            List<Tuple<int, double>> counts = new List<Tuple<int, double>>();
            try
            {
                lock (this)
                {
                    bitFieldsCopy = new Dictionary<string, BitArray>();
                    lock (bitFields)
                    {
                        foreach (var kv in bitFields)
                        {
                            bitFieldsCopy[kv.Key] = new BitArray(kv.Value);
                        }
                    }
                    
                    lock (hfile)
                    {
                        myBitField = hfile.BitField;
                    }

                    // possible = other & ~mine
                    possible = bitFieldsCopy[peerId].And(new BitArray(myBitField.Select(c => c == '0').ToArray()));

                    int counter = 0;
                    for (int i = 0; i < possible.Length; i++)
                    {
                        if (possible[i])
                        {
                            counts.Add(Tuple.Create(i, bitFieldsCopy.Values.Select(a => a[i]).Count(b => b) + numbers[counter++]));
                        }
                    }
                    if (counts.Count == 0)
                    {
                        return null;
                    }
                    var orderedCounts = counts.OrderBy(c => c.Item2);

                    Tuple<int, int> result = null;
                    foreach (var idxPiece in orderedCounts.Select(t => t.Item1))
                    {
                        Piece piece = hfile.Pieces[idxPiece];
                        for (int idxBlock = 0; idxBlock < piece.BitField.Length; idxBlock++)
                        {
                            if (piece.BitField[idxBlock] == '0')
                            {
                                lock (requestedBlocks)
                                {
                                    if (!requestedBlocks.Contains(Tuple.Create(idxPiece, idxBlock)))
                                    {
                                        result = Tuple.Create(idxPiece, idxBlock);
                                        requestedBlocks.Add(result);
                                        blockTimes[result] = MAX_TIME;
                                        goto end;
                                    }
                                }
                            }
                        }
                    }
                    end: return result;
                }
            }
            catch (Exception e)
            {
                Logger.log("CROATA",e.ToString());
            }
            return null;
            
        }

        public void Cancel()
        {
            lock (hfile)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                else if (File.Exists(filePath + DOWNLOADING))
                {
                    File.Delete(filePath + DOWNLOADING);
                }
            }
        }

        /// <summary>
        /// Adds a block data to the local file
        /// </summary>
        /// <param name="piece">Piece index</param>
        /// <param name="block">Block index</param>
        /// <param name="data">Block data in base64</param>
        public void AddBlock(int piece, int block, string data)
        {
            byte[] byteData = Convert.FromBase64String(data);
            long offset = piece * (long)hfile.PieceSize + block * (long)hfile.BlockSize;
            if (offset + byteData.Length > hfile.Size || byteData.Length > hfile.BlockSize)
            {
                throw new InvalidOperationException("Alguém cagou o pau nos dummies");
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Write(byteData, 0, byteData.Length);
            }
            Logger.log("Downloader", "Downloaded (piece, block) = (" + piece + ", " + block + ") for " + hfile.ID);


            lock (this)
            {
                // Update hfile
                bool completed;
                lock (hfile.Pieces[piece])
                {
                    char[] bits = hfile.Pieces[piece].BitField.ToCharArray();
                    if (bits[block] != '0')
                    {
                        Logger.log("Downloader", "[WARNING] Already possessed (piece, block) = (" + piece + ", " + block + ")");
                        return;
                    }
                    bits[block] = '1';
                    hfile.Pieces[piece].BitField = new string(bits);
                    completed = hfile.Pieces[piece].BitField.All(c => c == '1');
                }
                lock (hfile)
                {
                    hfile.Percentage += 1.0 * byteData.Length / hfile.Size;
                }

                // Send event Have
                if (completed)
                {
                    lock (hfile)
                    {
                        char[] bits = hfile.BitField.ToCharArray();
                        if (bits[piece] != '0')
                        {
                            throw new InvalidOperationException("Already possessed piece = " + piece);
                        }
                        bits[piece] = '1';
                        hfile.BitField = new string(bits);
                        completed = hfile.BitField.All(c => c == '1');
                    }
                    server.SendHave(hfile, piece);
                }

                // Remove .downloading
                if (completed)
                {
                    finished = true;
                    lock (hfile)
                    {
                        hfile.Status = StatusType.Completed;
                        hfile.Percentage = 1.0;
                        lock (hfile.Name)
                        {
                            File.Move(filePath, filePath.Substring(0, filePath.Length - DOWNLOADING.Length)); 
                        }
                    }
                }

                // Got requested block
                lock (requestedBlocks)
                {
                    requestedBlocks.Remove(Tuple.Create(piece, block));
                    blockTimes.Remove(Tuple.Create(piece, block));
                }
            }
        }
    }
}
