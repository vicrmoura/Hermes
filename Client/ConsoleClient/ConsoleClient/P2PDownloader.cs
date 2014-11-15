using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PDownloader
    {
        /* Fields */

        public readonly string FileID;

        private HFile hfile;
        private string filePath;

        /* Constructor */

        public P2PDownloader(string fileID, HFile hfile)
        {
            this.FileID = fileID;
            this.hfile = hfile;
            this.filePath = Path.Combine(Program.BaseFolder, hfile.Name + ".downloading");

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

        public void SetBitField(string bitField)
        {
            // TODO(luizmramos): Tambem receber nome do cliente (um bitfield por client)
            // TODO: this.bitField = bitfield (trocar de string pra mapa)
        }
        int c = 0;
        public Tuple<int, int> GetNextBlock()
        {
            // TODO:  selecionar proximo bloco a ser baixado
            if (c++ == 100000) return null; // for test purposes
            return new Tuple<int, int>(c, 0);
        }

        public void AddBlock(int piece, int block, string data)
        {
            return;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.Write))
            {
                byte[] byteData = Convert.FromBase64String(data);
                long offset = piece * (long)hfile.PieceSize + block * (long)hfile.BlockSize;
                if (offset + byteData.Length > hfile.Size || byteData.Length > hfile.BlockSize)
                {
                    throw new InvalidOperationException("Alguém cagou o pau nos dummies");
                }
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Write(byteData, 0, byteData.Length);
                // TODO: update hfile
                // TODO: send event Have
            }
        }
    }
}
