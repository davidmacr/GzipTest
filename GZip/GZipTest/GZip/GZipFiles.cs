using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace GZipTest.GZip
{
    internal class GZipFiles : GZipMain, IGZipper
    {
        public event ErrorOccured RaiseError;

        internal GZipFiles(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex reader, Mutex writer, FileStream fileWriter) : base(argument, fileBlock, pool, blockSize, reader, writer, fileWriter)
        {
        }

        public void StartProcess()
        {
            
            if (_canceled)
            {   
                return;
            }
            try
            {
                _pool.WaitOne();
                ZipBlock();

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
            int required = _fileBlock.Size;
            byte[] data = GetData(offset, required);
            WriteZip(data);
            ConcatZipped();
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }

        /// <summary>
        /// having an <c>byte[]</byte> create a gziped file 
        /// </summary>
        /// <param name="data"><c>byte[]</c>with the date to gzip</param>
        public void WriteZip(byte[] data)
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

        private ExitCode ConcatZipped()
        {

            if (!Directory.Exists(_argument.BlocksFolder))
            {
                Directory.CreateDirectory(_argument.BlocksFolder);
            }
            if (!File.Exists(_argument.OutcomeFile))
            {
                var file = File.Create(_argument.OutcomeFile);
                file.Close();
            }
            var blockName = $"{_argument.BlocksFolder}\\{_fileBlock.Name}";
            using var reader = File.Open(blockName, FileMode.Open);
            byte[] blockContent = new byte[reader.Length];
            reader.Read(blockContent, 0, (int)reader.Length);
            try
            {
                _writerMutex.WaitOne();
                _fileBlock.OffSet = _fileWriter.Length;
                _fileWriter.Write(blockContent, 0, (int)reader.Length);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _writerMutex.ReleaseMutex();
            }
            _fileBlock.ZippedSize = (int)reader.Length;
            return ExitCode.OK;
        }


    }
}
