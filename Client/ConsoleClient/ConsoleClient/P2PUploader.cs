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

        /// <summary>
        /// Returns block data in base64
        /// </summary>
        /// <param name="piece">Piece index</param>
        /// <param name="block">Block index</param>
        /// <returns>Block data in base64</returns>
        public string getBlock(int piece, int block)
        {
            // TODO(felipe)
            return "dummy";
        }
    }
}
