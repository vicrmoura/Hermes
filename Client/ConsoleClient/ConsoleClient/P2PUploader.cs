using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PUploader
    {
        private string fileId;
        private FileManager fileManager;
        private HFile file;

        public P2PUploader(FileManager fileManager, string fileId)
        {
            this.fileManager = fileManager;
            this.fileId = fileId;
            this.file = fileManager.getFile(fileId);
        }

        public bool fileExists()
        {
            return this.file != null;
        }

        public string getBitField()
        {
            return file.BitField;
        }

        public string getBlock(int piece, int block)
        {
            return "dummy";
        }
    }
}
