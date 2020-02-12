using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace GZipTest.Processor
{
    internal class ProcessManager
    {
        /// <summary>
        /// Current process data
        /// </summary>
        private readonly Argument _argument;
        /// <summary>
        /// This control the number of concurrent threads that will be executed concurrenlty
        /// </summary>
        private Semaphore _pool;
        /// <summary>
        /// Limit the access to risk code on multithreading
        /// </summary>
        private readonly Mutex _mut = new Mutex();
        /// <summary>
        /// Size used to split the file to zip
        /// </summary>
        long _blockSize = 1048576;
        /// <summary>
        /// Number of blocks to process concurrently
        /// </summary>
        int _blockNumber = 64;
        /// <summary>
        /// Has the maximun number of concurrent threads, based on number of processor of the computer
        /// </summary>
        int _threadPool = Environment.ProcessorCount >= 2 ? Environment.ProcessorCount - 1 : 1;
        /// <summary>
        /// has the overal status of the threads
        /// </summary>
        ExitCode _threadStatus = ExitCode.OK;
        /// <summary>
        /// Event handler to rise a cancel on all pending threads
        /// </summary>
        public event EventHandler CancelAllThreads;

        internal ProcessManager(Argument argument)
        {
            _argument = argument;
            ReadAppSettings();
        }

        /// <summary>
        /// Read from the application settings the parameters to run the application
        /// </summary>
        private void ReadAppSettings()
        {
            if (ConfigurationManager.AppSettings["fileBlockSize"] != null)
            {
                _blockSize = long.Parse(ConfigurationManager.AppSettings["fileBlockSize"]);
            }

            if (ConfigurationManager.AppSettings["concurrentBlocks"] != null)
            {
                _blockNumber = int.Parse(ConfigurationManager.AppSettings["concurrentBlocks"]);
                if (_blockNumber > 64)
                {
                    throw new ArgumentNullException("concurrentBlocks setting can't be bigger than 64.");
                }
            }
            if (ConfigurationManager.AppSettings["threadPoolSize"] != null)
            {
                _threadPool = int.Parse(ConfigurationManager.AppSettings["threadPoolSize"]);
            }

        }

        /// <summary>
        /// Check that received parameters are valid
        /// </summary>
        /// <returns></returns>
        private ExitCode ValidateArguments()
        {
            var inputFile = _argument.FileToProcess;

            if (_argument.Command == CommandInput.Decompress)
            {
                inputFile = _argument.ZippedFile;
            }

            if (!File.Exists(inputFile))
            {
                return ExitCode.InputFileDoesNotExists;
            }
            return ExitCode.OK;
        }

        /// <summary>
        /// Entry point of the application
        /// </summary>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        internal ExitCode RunTask()
        {
            ExitCode returnCode = ValidateArguments();
            if (returnCode != ExitCode.OK) { return returnCode; }
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

        /// <summary>
        /// Merge the list of blocks(files) unziped, into a single file
        /// </summary>
        /// <param name="blocks"></param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode ConcatBlocks(List<FileBlock> blocks, Mutex mut)
        {
            try
            {
                mut.WaitOne();
                if (!File.Exists(_argument.UnZippedFile)) { 
                    var file = File.Create(_argument.UnZippedFile);
                    file.Close();
                }
                using (FileStream writer = new FileStream(_argument.UnZippedFile, FileMode.Append))
                {
                    foreach (var item in blocks)
                    {
                        var blockFileName = Path.Join(_argument.BlocksFolder, $"{item.Name}.txt");
                        using var inputFileStream = new FileStream(blockFileName, FileMode.Open);
                        inputFileStream.CopyTo(writer);
                    }
                }
                return ExitCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                return ExitCode.ApplicationError;
            }
            finally
            {
                mut.ReleaseMutex();
            }
        }

        /// <summary>
        /// Starts the zip process of the file
        /// </summary>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode CompressFile()
        {
            CleanUpOutput();
            var fileBlocks = CreateBlocks();
            if (ProcessBlocks(fileBlocks) == ExitCode.OK)
            {
                SaveBlocks(fileBlocks);
            }
            return _threadStatus;
        }

        /// <summary>
        /// Starts the unzip process of a file
        /// </summary>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode DecompressFile()
        {
            var fileBlocks = GetBlocksFromFiles();
            var returnCode = ProcessBlocks(fileBlocks);
            return returnCode;
        }

        /// <summary>
        /// For the Decompress process load the blocks of the file based on main file
        /// </summary>
        /// <returns>
        /// <c>List<FileBlock></c> with the data of the blocks
        /// </returns>
        private List<FileBlock> GetBlocksFromFiles()
        {
            using (StreamReader reader = new StreamReader(_argument.ZippedFile))
            {
                var deserializedFileBlocks = new List<FileBlock>();
                var ms = new MemoryStream(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
                var ser = new DataContractJsonSerializer(deserializedFileBlocks.GetType());
                deserializedFileBlocks = ser.ReadObject(ms) as List<FileBlock>;
                ms.Close();
                return deserializedFileBlocks;
            }
        }

        /// <summary>
        /// When zipping, deleted the blocks folder and its content if exists
        /// </summary>
        private void CleanUpOutput()
        {
            if (Directory.Exists(_argument.BlocksFolder))
            {
                Directory.Delete(_argument.BlocksFolder, true);
            }
            Directory.CreateDirectory(_argument.BlocksFolder);
        }

        /// <summary>
        /// Zip or unzip the blocks created based on the size of the file
        /// </summary>
        /// <param name="blocks">List of the blocks that will be process</param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode ProcessBlocks(List<FileBlock> blocks)
        {
            int currentCount = 0;
            if (blocks == null) { throw new ArgumentNullException(nameof(blocks)); }
            try
            {
                Semaphore _writerPool = new Semaphore(0, 2);
                Mutex writerMutex = new Mutex();
                List<List<WaitHandle>> writerProcessWaiter = new List<List<WaitHandle>>();
                while (currentCount < blocks.Count)
                {
                    if ((currentCount + _blockNumber) > blocks.Count)
                    {
                        _blockNumber = blocks.Count - currentCount;
                    }
                    var blockToProcess = blocks.GetRange(currentCount, _blockNumber);

                    _pool = new Semaphore(0, _threadPool);
                    List<WaitHandle> blockProcessWaiter = new List<WaitHandle>();

                    foreach (var item in blockToProcess)
                    {
                        ManualResetEvent waitHandle = new ManualResetEvent(false);
                        blockProcessWaiter.Add(waitHandle);
                        ProcessZip zip = new ProcessZip(_argument, item, _pool, _blockSize, _mut);
                        zip.RaiseError += Zip_RaiseError;
                        CancelAllThreads += zip.CancelThread;
                        Thread blockThread = new Thread(new ThreadStart(() =>
                        {
                            zip.ProcessBlock();
                            waitHandle.Set();
                        }));
                        blockThread.Start();
                    }

                    _pool.Release(_threadPool);
                    WaitHandle.WaitAll(blockProcessWaiter.ToArray());
                    currentCount += _blockNumber;

                    ManualResetEvent writerWaitHandle = new ManualResetEvent(false);

                    if (_argument.Command == CommandInput.Decompress)
                    {
                        if (blocks.Count > 64)
                        {
                            Thread fileWriterThread = new Thread(new ThreadStart(() =>
                            {
                                ConcatBlocks(blockToProcess, writerMutex);
                                writerWaitHandle.Set();
                            }));
                            fileWriterThread.Start();
                            AddWaitHandler(writerProcessWaiter, writerWaitHandle);
                        }
                        else
                        {
                            ConcatBlocks(blockToProcess, writerMutex);
                        }
                    }
                }

                foreach (var item in writerProcessWaiter)
                {
                    WaitHandle.WaitAll(item.ToArray());
                }
                return _threadStatus;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}, stack trace: {ex.StackTrace}");
                Zip_RaiseError("Starting");
                return ExitCode.ApplicationError;
            }
        }

        /// <summary>
        /// Create a list of <c>WaitHandle</c> in order to manage the merge of blocks when decompress the file
        /// </summary>
        /// <param name="waiters">List of waiters</param>
        /// <param name="writerWaitHandle">waithandle to add</param>
        private void AddWaitHandler(List<List<WaitHandle>> waiters, ManualResetEvent writerWaitHandle)
        {
            List<WaitHandle> writerProcessWaiter;
            if (waiters.Count == 0) { waiters.Add(new List<WaitHandle>()); }

            List<WaitHandle> currentList = waiters[waiters.Count - 1];

            if (waiters.Count == 0 || currentList.Count >= _blockNumber)
            {
                writerProcessWaiter = new List<WaitHandle>();
                waiters.Add(writerProcessWaiter);
                writerProcessWaiter.Add(writerWaitHandle);
                return;
            }

            if (currentList.Count < _blockNumber)
            {
                currentList.Add(writerWaitHandle);
                return;
            }
        }

        /// <summary>
        /// When an error occured processing a block, 
        /// all the other blocks that are pending to be executed are cancelled using the 
        /// event
        /// </summary>
        /// <param name="blockName"></param>
        private void Zip_RaiseError(string blockName)
        {
            _threadStatus = ExitCode.ThreadsCancelledByError;
            Console.WriteLine($"Error Ocurred in block {blockName}");
            EventHandler handler = CancelAllThreads;
            handler?.Invoke(this, null);
        }

        /// <summary>
        /// Based on the size of file create a list of blocks 
        /// </summary>        
        /// <returns><c>List<FileBlock></c> Blocks that will be created during the zip process</returns>
        private List<FileBlock> CreateBlocks()
        {
            var file = new FileInfo(_argument.FileToProcess);
            long fileLength = file.Length;
            long fileInMb = fileLength / _blockSize;
            long remaining = fileLength - (fileInMb * _blockSize);
            List<FileBlock> chunks = new List<FileBlock>();
            for (int i = 0; i < fileInMb; i++)
            {
                chunks.Add(new FileBlock()
                {
                    Name = $"{_argument.FileName}_block_{i}",
                    Order = i,
                    Size = (int)_blockSize
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

        /// <summary>
        /// Save an object <c>List<FileBlock></c> to a file that will be used for unzip
        /// </summary>
        /// <returns><c>List<FileBlock></c> blocks created during unzip process</returns>
        private void SaveBlocks(List<FileBlock> blocks)
        {
            if (blocks == null) return;
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<FileBlock>));
            using var ms = new MemoryStream();
            serializer.WriteObject(ms, blocks);
            using var writer = new StreamWriter($"{_argument.FilePath}\\{_argument.FileName}.gz");
            writer.Write(Encoding.Default.GetString(ms.ToArray()));
        }

    }
}



