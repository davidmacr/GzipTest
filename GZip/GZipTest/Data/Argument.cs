using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest.Data
{
    public class Argument
    {
        string _fileToProces;
        readonly long _ticks = DateTime.Now.Ticks;
        FileInfo _fileData;
        readonly string _temporalFolder;
        
        public long HeaderSize { get; set; }

        public Argument()
        {
            _temporalFolder = Path.GetTempPath();
        }
        
        /// <summary>
        /// Has the operation <c>CommandInput</c> that will be exeuted by the application
        /// </summary>
        public CommandInput Command { get; set; }
        /// <summary>
        /// Full path of the file to process
        /// </summary>
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
                    _fileData = new FileInfo(_fileToProces);
                }
            }
        }
        /// <summary>
        /// Return the path of the <c>FileToProcess</c> to process
        /// </summary>
        public string FilePath
        {
            get
            {
                return _fileData.DirectoryName;
            }
        }
        /// <summary>
        /// Return the name of the <c>FileToProcess</c> to process
        /// </summary>
        public string FileName
        {
            get
            {
                return _fileData.Name;
            }
        }
        /// <summary>
        /// Return the path of blocks folder based on <c>FileToProcess</c>
        /// </summary>
        public string BlocksFolder
        {
            get
            {
                return Path.Join(_temporalFolder, $"{_fileData.Name}_Blocks");
            }
        }
        /// <summary>
        /// Return the full path of gzip file based on  <c>FileToProcess</c>
        /// </summary>
        public string OutcomeFile { get; set; }

    }
}