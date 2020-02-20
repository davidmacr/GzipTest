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
        readonly Mutex _readerMutex;
        readonly Mutex _writerMutex;

        static BinaryReader _fileData;
        bool _canceled = false;
        internal event ErrorOccured RaiseError;


        internal ProcessZip(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex reader, Mutex writer)
        {
            _argument = argument;
            _fileBlock = fileBlock;
            _pool = pool;
            _blockSize = blockSize;
            _readerMutex = reader;
            _writerMutex = writer;

        }

        /// <summary>
        /// Using received block, zip or unzip it
        /// </summary>
        /// <remarks>
        /// Uses the instance of _pool to don't overkill the processor
        /// Uses the instance of _readerMutex to control the access to risk code
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
                    UnZipBlock2();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating block {_fileBlock.Name}, Error: {ex.Message}, Stack Trace: {ex.StackTrace}");
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
            ConcatZipped();
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }


        private ExitCode ConcatZipped()
        {            
            var fileName = $"{_argument.ZippedFile}_2";
            if (!Directory.Exists(_argument.BlocksFolder))
            {
                Directory.CreateDirectory(_argument.BlocksFolder);
            }
            if (!File.Exists(fileName))
            {
                var file = File.Create(fileName);
                file.Close();
            }
            var blockName = $"{_argument.BlocksFolder}\\{_fileBlock.Name}";            
            using var reader = File.Open(blockName, FileMode.Open);
            byte[] blockContent = new byte[reader.Length];            
            reader.Read(blockContent, 0, (int)reader.Length);

            _writerMutex.WaitOne();
            using var writer = File.Open(fileName, FileMode.Append);
            _fileBlock.OffSet = (int)writer.Length;
            writer.Write(blockContent, 0, (int)reader.Length);
            writer.Flush();
            writer.Close();
            _writerMutex.ReleaseMutex();

            _fileBlock.ZippedSize = (int)reader.Length;
            
            return ExitCode.OK;
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

        private void UnZipBlock2()
        {
            if (!Directory.Exists(_argument.BlocksFolder))
            {
                Directory.CreateDirectory(_argument.BlocksFolder);
            }
            _fileBlock.Outputfile = Path.Join(_argument.BlocksFolder, $"{_fileBlock.Name}.txt");
            var blockData = GetData(_fileBlock.OffSet, _fileBlock.ZippedSize);
            using var inputFileStream = new MemoryStream(blockData);
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
            var fileName = Path.Join(_argument.BlocksFolder, _fileBlock.Name);
            using MemoryStream writer = new MemoryStream();
            using FileStream compressedFileStream = File.Create(fileName);
            using GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
            var fileData = new FileInfo(fileName);
            writer.Write(data);
            writer.Position = 0;
            writer.CopyTo(compressionStream);
            _fileBlock.ZippedSize = data.Length;
        }

        /// <summary>
        /// Get a block from a file
        /// </summary>
        /// <param name="offset">startup point to read in the file</param>
        /// <param name="required">block of the file to read</param>
        /// <returns></returns>
        private byte[] GetData(long offset, int required)
        {
            _readerMutex.WaitOne();
            if (_fileData == null)
            {
                var fileName = _argument.FileToProcess;
                if (_argument.Command == CommandInput.Decompress)
                {
                    fileName = $"{_argument.ZippedFile}_2"; //TODO: Review this
                }
                _fileData = new BinaryReader(File.Open(fileName, FileMode.Open));
            }
            _fileData.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] data = _fileData.ReadBytes(required);
            _readerMutex.ReleaseMutex();
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
