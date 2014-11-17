using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PUploader
    {
        private readonly string fileId;
        private readonly HFile hfile;
        private readonly string filePath;

        public P2PUploader(string fileId, HFile hfile)
        {
            this.fileId = fileId;
            this.hfile = hfile;
            this.filePath = Path.Combine(Program.BaseFolder, hfile.Name);
        }

        public bool fileExists()
        {
            return this.hfile != null;
        }

        public string getBitField()
        {
            string bitField;
            lock (hfile)
            {
                bitField = hfile.BitField;
            }
            return bitField;
        }

        /// <summary>
        /// Returns block data in base64
        /// </summary>
        /// <param name="piece">Piece index</param>
        /// <param name="block">Block index</param>
        /// <returns>Block data in base64</returns>
        public string getBlock(int piece, int block)
        {
            lock (hfile)
            {
                if (hfile.BitField[piece] == '0')
                {
                    throw new ArgumentException("HFile doesn't posses that piece", "piece");
                }
            }

            lock (hfile.Pieces[piece])
            {
                if (hfile.Pieces[piece].BitField[block] == '0')
                {
                    throw new ArgumentException("HFile doesn't posses that (piece, block)", "block");
                }
            }

            long offset = piece * (long)hfile.PieceSize + block * (long)hfile.BlockSize;
            int size = Math.Min(hfile.BlockSize, hfile.Pieces[piece].Size - block * hfile.BlockSize);
            byte[] byteData = new byte[size];
            lock (hfile.Name)
            {
                string path = File.Exists(filePath) ? filePath : filePath + P2PDownloader.DOWNLOADING;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    int bytesRead = stream.Read(byteData, 0, size);
                    if (bytesRead != size)
                    {
                        throw new Exception("bytesRead != size");
                    }
                }
            }

            return Convert.ToBase64String(byteData);
        }
    }
}
