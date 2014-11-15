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
        private HFile file;

        public P2PUploader(string fileId, HFile hfile)
        {
            this.fileId = fileId;
            this.file = hfile;
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
