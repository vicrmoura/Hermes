using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Hermes
{
    static class Logger
    {       
        public static void log(string label, string text)
        {
            if (label == "TrackerClient") return;
            System.Diagnostics.Trace.WriteLine(string.Format("{0} >> [{1}] {2}", DateTime.Now.ToString("HH:mm:ss"), label, text));
        }
    }
}
