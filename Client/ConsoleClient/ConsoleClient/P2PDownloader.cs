﻿using System;
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

        }
    }
}