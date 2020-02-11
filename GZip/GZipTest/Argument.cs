using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest
{
    public class Argument
    {
        string _fileToProces;
        public CommandInput Command { get; set; }
        public string FileToProcess
        {
            get
            {
                return _fileToProces;
            }
            set
            {
                {
                    _fileToProces = value;
                    FileInfo file = new FileInfo(_fileToProces);
                    FilePath = file.DirectoryName;
                    FileName = file.Name;
                    BlocksFolder = Path.Join(file.DirectoryName, $"{file.Name}_Blocks");
                    UnZipFile = Path.Join(file.DirectoryName, file.Name.Replace(".gz",".nw"));
                }
            }
        }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public bool CleanUpOutput { get; set; }
        public string BlocksFolder { get; set; }
        public string UnZipFile { get; set; }

    }
}
