using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Hermes
{
    class P2PDownloader
    {
        /* Fields */

        public readonly string FileID;

        private readonly HFile hfile;
        private readonly string filePath;
        private readonly P2PServer server;
        private readonly Dictionary<string/*peerId*/, BitArray> bitFields;

        /* Constructor */

        public P2PDownloader(string fileID, HFile hfile, P2PServer server)
        {
            this.FileID = fileID;
            this.hfile = hfile;
            this.server = server;
            this.filePath = Path.Combine(Program.BaseFolder, hfile.Name + ".downloading");
            this.bitFields = new Dictionary<string, BitArray>();

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
        }

        /* Methods */

        public void SetBitField(string peerName, string bitField)
        {
            byte[] bitFieldData = Convert.FromBase64String(bitField);
            bitFields[peerName] = new BitArray(bitFieldData);
        }

        /// <summary>
        /// The next (piece, block) to download. Returns null if finished
        /// </summary>
        /// <returns>The pair (piece, block)</returns>
        public Tuple<int, int> GetNextBlock()
        {
            // TODO (croata): selecionar proximo bloco a ser baixado
            return null;
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

            // Update hfile
            bool completed;
            lock (hfile.Pieces[piece])
            {
                char[] bits = hfile.Pieces[piece].BitField.ToCharArray();
                if (bits[block] != '0')
                {
                    throw new InvalidOperationException("Already possessed (piece, block) = (" + piece + ", " + block + ")");
                }
                bits[block] = '1';
                hfile.Pieces[piece].BitField = new string(bits);
                completed = hfile.Pieces[piece].BitField.All(c => c == '1');
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
                }
                server.SendHave(hfile, piece);
            }

            // TODO (croata): remove ".downloading" extension
        }
    }
}
