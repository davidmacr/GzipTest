using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Data
{
    [Serializable]
    internal class FileBlock
    {
        public string Name { get; set; }
        public long Order { get; set; }
        public int Size { get; set; }
        public string Outputfile { get; set; }
    }
}
