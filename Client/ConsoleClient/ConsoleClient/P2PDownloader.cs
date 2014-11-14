using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class P2PDownloader
    {
        private string fileId;
        private FileManager fileManager;

        public P2PDownloader(string fileId, FileManager fileManager)
        {
            this.fileId = fileId;
            this.fileManager = fileManager;
        }

        public void setBitField(string bitField)
        {

        }
    }
}
