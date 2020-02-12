using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace GZipTest.Processor
{
    /// <summary>
    /// Used to notify errors to main thread
    /// </summary>
    /// <param name="blockName"></param>
    internal delegate void ErrorOccured(string blockName);

    internal class ProcessZip
    {

        readonly long _blockSize;
        readonly Argument _argument;
        readonly FileBlock _fileBlock;
        readonly Semaphore _pool;
        readonly Mutex _mut;

        static BinaryReader _fileData;
        bool _canceled = false;
        internal event ErrorOccured RaiseError;


        internal ProcessZip(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex mut)
        {
            _argument = argument;
            _fileBlock = fileBlock;
            _pool = pool;
            _blockSize = blockSize;
            _mut = mut;
        }

        /// <summary>
        /// Using received block, zip or unzip it
        /// </summary>
        /// <remarks>
        /// Uses the instance of _pool to don't overkill the processor
        /// Uses the instance of _mut to control the access to risk code
        /// </remarks>
        internal void ProcessBlock()
        {
            _pool.WaitOne();
            if (_canceled)
            {
                Console.WriteLine($"Block {_fileBlock.Name} was cancelled.");
                _pool.Release();
                return;
            }
            try
            {
                if (_argument.Command == CommandInput.Compress)
                {
                    ZipBlock();
                }
                else if (_argument.Command == CommandInput.Decompress)
                {
                    UnZipBlock();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating block {_fileBlock.Name}, Error: {ex.Message}");
                RaiseError(_fileBlock.Name);
            }
            finally
            {
                _pool.Release();
            }
        }

        /// <summary>
        /// Create a file gziped of a block of file
        /// </summary>
        private void ZipBlock()
        {
            long offset = (_fileBlock.Order * _blockSize);
            //if (offset > 0) offset--;
            int required = _fileBlock.Size;
            byte[] data = GetData(offset, required);
            WriteZip(data);
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }

        /// <summary>
        /// Create a file unziped based on a zipped file
        /// </summary>
        private void UnZipBlock()
        {
            var gzipFile = Path.Join(_argument.BlocksFolder, _fileBlock.Name);
            _fileBlock.Outputfile = Path.Join(_argument.BlocksFolder, $"{_fileBlock.Name}.txt");
            using var inputFileStream = new FileStream(gzipFile, FileMode.Open);
            using var outputFileStream = new FileStream(_fileBlock.Outputfile, FileMode.Create);
            using var gzipStream = new GZipStream(inputFileStream, CompressionMode.Decompress);
            gzipStream.CopyTo(outputFileStream);
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }

        /// <summary>
        /// having an <c>byte[]</byte> create a gziped file 
        /// </summary>
        /// <param name="data"><c>byte[]</c>with the date to gzip</param>
        private void WriteZip(byte[] data)
        {
            using MemoryStream writer = new MemoryStream();
            using FileStream compressedFileStream = File.Create(Path.Join(_argument.BlocksFolder, _fileBlock.Name));
            using GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
            writer.Write(data);
            writer.Position = 0;
            writer.CopyTo(compressionStream);
        }

        /// <summary>
        /// Get a block from a file
        /// </summary>
        /// <param name="offset">startup point to read in the file</param>
        /// <param name="required">block of the file to read</param>
        /// <returns></returns>
        private byte[] GetData(long offset, int required)
        {
            _mut.WaitOne();
            if (_fileData == null)
            {
                _fileData = new BinaryReader(File.Open(_argument.FileToProcess, FileMode.Open));
            }
            _fileData.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] data = _fileData.ReadBytes(required);
            _mut.ReleaseMutex();
            return data;
        }

        /// <summary>
        /// Event notifier that the current thread must be stopped
        /// </summary>
        /// <remarks>
        /// This event is used in case any other thread fails
        /// </remarks>
        internal void CancelThread(object sender, EventArgs e)
        {
            this._canceled = true;
        }

    }
}
