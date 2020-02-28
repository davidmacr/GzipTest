using GZipTest.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace GZipTest.GZip
{
    public class UnGZipFiles : GZipMain, IGZipper
    {
        public event ErrorOccured RaiseError;

        internal UnGZipFiles(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex reader, Mutex writer) : base(argument, fileBlock, pool, blockSize, reader, writer, null)
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
                UnZipBlock();

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

        private void UnZipBlock()
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
    }
}
