using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hermes
{
    class FileManager
    {

        public HFile getFile(string fileId)
        {
            var dummy = new HFile();
            dummy.BitField = "test";
            return dummy;
        }

        public Dictionary<string, HFile> getFiles()
        {
            HFile testFile = new HFile();
            testFile.Name = "Captain America";
            testFile.PieceSize = 10000;
            testFile.BlockSize = 10;
            testFile.Size = 123456789;
            testFile.Status = StatusType.Downloading;
            Piece p = new Piece("abcd");
            testFile.Pieces = new Piece[] { p };
            return new Dictionary<string, HFile> {{"1234", testFile}};
        }
    }
}
