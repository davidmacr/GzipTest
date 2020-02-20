using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Data
{
    [Serializable]
    public class FileBlock
    {
        public string Name { get; set; }
        public long Order { get; set; }
        public int Size { get; set; }
        public string Outputfile { get; set; }
        public int ZippedSize { get; set; }
        public long OffSet { get; set; }
    }
}
