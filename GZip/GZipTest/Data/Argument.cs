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
        long _ticks = DateTime.Now.Ticks;
        FileInfo _fileData;
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
                return Path.Join(_fileData.DirectoryName, $"{_fileData.Name}_Blocks");
            }
        }
        /// <summary>
        /// Return the full path of gzip file based on  <c>FileToProcess</c>
        /// </summary>
        public string ZippedFile
        {
            get
            {
                if (_fileData.FullName.EndsWith(".gz")) { return _fileData.FullName; }
                return $"{_fileData.FullName}.gz";
            }
        }
        /// <summary>
        /// Return the full path of unzip file based on  <c>FileToProcess</c>
        /// </summary>
        public string UnZippedFile
        {
            get
            {
                if (_fileData.Exists) { return $"{_fileData.FullName}_{ _ticks}"; }
                return _fileData.FullName;
            }
        }
    }
}