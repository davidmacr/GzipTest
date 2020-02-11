using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace GZipTest.Processor
{
    internal class ProcessManager
    {
        const long _blockSize = 1048576;
        int _blockNumber = 64;
        public Argument _argument;
        private static Semaphore _pool;
        private readonly Mutex mut = new Mutex();
        readonly int threadPool = Environment.ProcessorCount >= 2 ? Environment.ProcessorCount - 1 : 1;
        public event EventHandler CancelAllThreads;
        ExitCode threadStatus = ExitCode.OK;

        internal void OnThresholdReached(EventArgs e)
        {
            EventHandler handler = CancelAllThreads;
            handler?.Invoke(this, e);
        }

        internal ProcessManager(Argument argument)
        {
            _argument = argument;
            if (!File.Exists(argument.FileToProcess))
            {
                throw new ArgumentException("fileName is required");
            }
        }

        internal ExitCode RunTask()
        {
            ExitCode returnCode = ExitCode.OK;
            switch (_argument.Command)
            {
                case CommandInput.Compress:
                    {
                        returnCode = CompressFile();
                        break;
                    }
                case CommandInput.Decompress:
                    {
                        returnCode = DecompressFile();
                        break;
                    }

                case CommandInput.Invalid:
                default:
                    {
                        throw new ArgumentException("CommandInput is not valid");
                    }
            }
            return returnCode;
        }


        private ExitCode CompressFile()
        {
            CleanUpOutput();
            var fileBlocks = GetFileBlocks();
            if (ProcessBlocks(fileBlocks) == ExitCode.OK)
            {
                SaveBlocks(fileBlocks);
            }
            return threadStatus;
        }

        private ExitCode DecompressFile()
        {
            var fileBlocks = GetBlocksFromFiles();
            return ProcessBlocks(fileBlocks);
        }

        private List<FileBlock> GetBlocksFromFiles()
        {
            using (StreamReader reader = new StreamReader(_argument.FileToProcess))
            {

                var deserializedFileBlocks = new List<FileBlock>();
                var ms = new MemoryStream(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
                var ser = new DataContractJsonSerializer(deserializedFileBlocks.GetType());
                deserializedFileBlocks = ser.ReadObject(ms) as List<FileBlock>;
                ms.Close();
                return deserializedFileBlocks;

            }
        }

        private void CleanUpOutput()
        {
            if (Directory.Exists(_argument.BlocksFolder))
            {
                Directory.Delete(_argument.BlocksFolder, true);
            }
            Directory.CreateDirectory(_argument.BlocksFolder);
        }

        private ExitCode ProcessBlocks(List<FileBlock> blocks)
        {
            int currentCount = 0;
            if (blocks == null) { throw new ArgumentNullException("blocks"); }
            try
            {
                BinaryReader fileData = new BinaryReader(File.Open(_argument.FileToProcess, FileMode.Open));

                while (currentCount < blocks.Count)
                {
                    if ((currentCount + _blockNumber) > blocks.Count)
                    {
                        _blockNumber = blocks.Count - currentCount;
                    }
                    var blockToProcess = blocks.GetRange(currentCount, _blockNumber);

                    _pool = new Semaphore(0, threadPool);
                    List<WaitHandle> waiter = new List<WaitHandle>();

                    foreach (var item in blockToProcess)
                    {
                        ManualResetEvent waitHandle = new ManualResetEvent(false);
                        waiter.Add(waitHandle);
                        ProcessZip zip = new ProcessZip(_argument, item, fileData, _pool, _blockSize, mut);
                        zip.RaiseError += Zip_RaiseError;
                        CancelAllThreads += zip.CancelThread;
                        Thread t = new Thread(new ThreadStart(() =>
                        {
                            zip.ProcessBlock();
                            waitHandle.Set();
                        }));

                        t.Start();
                    }

                    _pool.Release(threadPool);
                    WaitHandle.WaitAll(waiter.ToArray());

                    currentCount += _blockNumber;

                }
                return threadStatus;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}, stack trace: {ex.StackTrace}");
                Zip_RaiseError("Starting");
                return ExitCode.ApplicationError;
            }
        }

        private void Zip_RaiseError(string blockName)
        {
            threadStatus = ExitCode.ThreadsCancelledByError;
            Console.WriteLine($"Error Ocurred in block {blockName}");
            EventHandler handler = CancelAllThreads;
            handler?.Invoke(this, null);
        }

        private List<FileBlock> GetFileBlocks()
        {
            var file = new FileInfo(_argument.FileToProcess);
            return CreateBlocks(file.Length);
        }

        private List<FileBlock> CreateBlocks(long fileLength)
        {
            long fileInMb = fileLength / 1024 / 1024;
            long remaining = fileLength - (fileInMb * _blockSize);
            List<FileBlock> chunks = new List<FileBlock>();
            for (int i = 0; i < fileInMb; i++)
            {
                chunks.Add(new FileBlock()
                {
                    Name = $"{_argument.FileName}_block_{i}",
                    Order = i,
                    Size = 1048576
                });
            }
            chunks.Add(new FileBlock()
            {
                Name = $"{_argument.FileName}_block_{fileInMb}",
                Order = fileInMb,
                Size = (int)remaining
            });
            return chunks;
        }

        private void SaveBlocks(List<FileBlock> blocks)
        {
            if (blocks == null) return;
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<FileBlock>));
            using var ms = new MemoryStream();
            serializer.WriteObject(ms, blocks);
            using var writer = new StreamWriter($"{_argument.BlocksFolder}\\{_argument.FileName}.gz");
            writer.Write(Encoding.Default.GetString(ms.ToArray()));
        }

    }
}



