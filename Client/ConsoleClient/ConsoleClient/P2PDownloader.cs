using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PDownloader
    {
        public string FileId { get; private set; }
        private FileManager fileManager;

        public P2PDownloader(FileManager fileManager, string fileId)
        {
            this.FileId = fileId;
            this.fileManager = fileManager;
        }

        public void setBitField(string bitField)
        {
            // TODO(luizmramos): Tambem receber nome do cliente (um bitfield por client)
            // TODO: this.bitField = bitfield (trocar de string pra mapa)
        }
        int c = 0;
        public Tuple<int, int> getNextBlock()
        {
            // TODO:  selecionar proximo bloco a ser baixado
            if (c++ == 100000) return null; // for test purposes
            return new Tuple<int, int>(c, 0);
        }

        public void addBlock(int piece, int block, string data)
        {
            // TODO(felipe): Salvar block no hd 
        }
    }
}
