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

    internal delegate void ErrorOccured(string blockName);

    internal class ProcessZip
    {
        long _blockSize;
        Argument _argument;
        BinaryReader _fileData;
        FileBlock _fileBlock;
        Semaphore _pool;
        bool _canceled = false;
        internal event ErrorOccured RaiseError;
        Mutex _mut;


        internal ProcessZip(Argument argument, FileBlock fileBlock, BinaryReader fileData, Semaphore pool, long blockSize, Mutex mut)
        {
            _argument = argument;
            _fileBlock = fileBlock;
            _fileData = fileData;
            _pool = pool;
            _blockSize = blockSize;
            _mut = mut;
        }

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

        private void ZipBlock()
        {
            long offset = (_fileBlock.Order * _blockSize);
            if (offset > 0) offset--;
            int required = _fileBlock.Size;
            byte[] data = GetData(offset, required);
            WriteZip(data);

            //WriteFlat(data);
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }

        private void UnZipBlock()
        {
            var gzipFile = Path.Join(_argument.FilePath, _fileBlock.Name);
            _fileBlock.Outputfile = Path.Join(_argument.FilePath, $"{_fileBlock.Name}.txt");
            using var inputFileStream = new FileStream(gzipFile, FileMode.Open);
            using var outputFileStream = new FileStream(_fileBlock.Outputfile, FileMode.Create);
            using var gzipStream = new GZipStream(inputFileStream, CompressionMode.Decompress);
            gzipStream.CopyTo(outputFileStream);
            Console.WriteLine($"Block {_fileBlock.Name} created, Size: {_fileBlock.Size}");
        }


        private void WriteFlat(byte[] data)
        {
            using StreamWriter writer = new StreamWriter(Path.Join(_argument.BlocksFolder, _fileBlock.Name));
            writer.Write(Encoding.Default.GetString(data));
        }

        private void WriteZip(byte[] data)
        {
            using MemoryStream writer = new MemoryStream();
            using FileStream compressedFileStream = File.Create(Path.Join(_argument.BlocksFolder, _fileBlock.Name));
            using GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
            writer.Write(data);
            writer.Position = 0;
            writer.CopyTo(compressionStream);
        }


        private byte[] GetData(long offset, int required)
        {
            _mut.WaitOne();
            _fileData.BaseStream.Seek(offset, SeekOrigin.Begin);
            byte[] data = _fileData.ReadBytes(required);
            _mut.ReleaseMutex();
            return data;
        }

        internal void CancelThread(object sender, EventArgs e)
        {
            this._canceled = true;
        }

    }
}
