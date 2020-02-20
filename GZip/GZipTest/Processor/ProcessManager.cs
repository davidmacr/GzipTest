using GZipTest.Data;
using GZipTest.Enums;
using GZipTest.GZip;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace GZipTest.Processor
{
    public class ProcessManager : IDisposable
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
        private readonly Mutex _readerMutex = new Mutex();

        /// <summary>
        /// Used to control risk code when write the unziped file
        /// </summary>
        private readonly Mutex _writerMutex = new Mutex();

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
        /// reserved bytes for each block
        /// </summary>
        readonly long _blockTextSize = 300;
        readonly int _offset = 10;

        /// <summary>
        /// has the overal status of the threads
        /// </summary>
        ExitCode _threadStatus = ExitCode.OK;

        /// <summary>
        /// Event handler to rise a cancel on all pending threads
        /// </summary>
        public event EventHandler CancelAllThreads;

        /// <summary>
        /// Rise to release all resources of threads
        /// </summary>
        public event EventHandler DisposeAllThreads;

        FileStream treadsWriter;

        /// <summary>
        /// Initialize the manager class
        /// </summary>
        /// <param name="argument">Argument object with the date need to process</param>
        public ProcessManager(Argument argument)
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
        public ExitCode RunTask()
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
                if (_argument.Command == CommandInput.Decompress)
                {
                    return ConcatUnZipped(blocks);
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
        /// Process the concatenation process of the unziped file
        /// </summary>
        /// <param name="blocks">list of blocks to concact</param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode ConcatUnZipped(List<FileBlock> blocks)
        {
            if (!File.Exists(_argument.UnZippedFile))
            {
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

        /// <summary>
        /// Starts the zip process of the file
        /// </summary>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        private ExitCode CompressFile()
        {
            var fileBlocks = CreateBlocks();
            InitializeFiles();
            treadsWriter = new FileStream(_argument.ZippedFile, FileMode.Append);
            if (ProcessBlocks(fileBlocks) == ExitCode.OK)
            {
                CreateZippedFile(fileBlocks);
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

        private List<FileBlock> GetBlocksFromFiles()
        {
            using BinaryReader file = new BinaryReader(File.Open(_argument.ZippedFile, FileMode.Open));
            file.BaseStream.Seek(0, SeekOrigin.Begin);
            byte[] data = file.ReadBytes(_offset + 2);
            int lenght = BitConverter.ToInt32(data);
            file.BaseStream.Seek(_offset+2, SeekOrigin.Begin);
            var blocksData = file.ReadBytes(lenght);
            if (blocksData[0] != 91)
            {
                file.BaseStream.Seek(_offset + 3, SeekOrigin.Begin);
                blocksData = file.ReadBytes(lenght);
            }
            var str = Encoding.Default.GetString(blocksData);
            var deserializedFileBlocks = new List<FileBlock>();
            using var ms = new MemoryStream(blocksData.ToArray());
            var ser = new DataContractJsonSerializer(deserializedFileBlocks.GetType());
            deserializedFileBlocks = ser.ReadObject(ms) as List<FileBlock>;
            ms.Close();
            return deserializedFileBlocks;
        }


        /// <summary>
        /// When zipping, deleted the blocks folder and its content if exists
        /// </summary>
        private void InitializeFiles()
        {
            if (File.Exists(_argument.ZippedFile))
            {
                File.Delete(_argument.ZippedFile);
            }

            if (!Directory.Exists(_argument.BlocksFolder))
            {
                Directory.CreateDirectory(_argument.BlocksFolder);
            }
            var data = Encoding.ASCII.GetBytes(new string(' ', (int)_argument.HeaderSize));
            FileStream writer = new FileStream(_argument.ZippedFile, FileMode.Create);
            using BinaryWriter streamGenerator = new BinaryWriter(writer);
            streamGenerator.Write(data);
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
                        var processor = GzipFactory.CreateClass(_argument, item, _pool, _blockSize, _readerMutex, _writerMutex, treadsWriter);
                        processor.RaiseError += Zip_RaiseError;
                        CancelAllThreads += processor.CancelThread;
                        DisposeAllThreads += processor.CancelThread;
                        Thread blockThread = new Thread(new ThreadStart(() =>
                        {
                            processor.StartProcess();
                            waitHandle.Set();
                        }));
                        blockThread.Start();
                    }

                    _pool.Release(_threadPool);
                    WaitHandle.WaitAll(blockProcessWaiter.ToArray());
                    currentCount += _blockNumber;

                    ManualResetEvent writerWaitHandle = new ManualResetEvent(false);
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

                foreach (var item in writerProcessWaiter)
                {
                    WaitHandle.WaitAll(item.ToArray());
                }
                DisposeAllThrreads();
                return _threadStatus;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}, stack trace: {ex.StackTrace}");
                Zip_RaiseError("Starting");
                return ExitCode.ApplicationError;
            }
            finally
            {
                if (treadsWriter != null)
                {
                    treadsWriter.Flush();
                    treadsWriter.Close();
                }
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
        /// Start  the dispose action of all treads
        /// event
        /// </summary>
        /// <param name="blockName"></param>
        private void DisposeAllThrreads()
        {   
            EventHandler handler = DisposeAllThreads;
            handler?.Invoke(this, null);
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
            List<FileBlock> blocks = new List<FileBlock>();
            for (int i = 0; i < fileInMb; i++)
            {
                blocks.Add(new FileBlock()
                {
                    Name = $"{_argument.FileName}_block_{i}",
                    Order = i,
                    Size = (int)_blockSize
                });
            }
            blocks.Add(new FileBlock()
            {
                Name = $"{_argument.FileName}_block_{fileInMb}",
                Order = fileInMb,
                Size = (int)remaining
            });
            _argument.HeaderSize = (blocks.Count * _blockTextSize) + _offset;
            return blocks;
        }

        /// <summary>
        /// Save an object <c>List<FileBlock></c> to a file that will be used for unzip
        /// </summary>
        /// <returns><c>List<FileBlock></c> blocks created during unzip process</returns>
        private void CreateZippedFile(List<FileBlock> blocks)
        {
            if (blocks == null) return;
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<FileBlock>));
            using var blocksData = new MemoryStream();
            serializer.WriteObject(blocksData, blocks);            
            var blockStr = Encoding.Default.GetString(blocksData.ToArray());
            using FileStream writer = new FileStream(_argument.ZippedFile, FileMode.Open);
            using BinaryWriter binaryWriter = new BinaryWriter(writer);
            var lenghtAsBits = BitConverter.GetBytes(blockStr.Length);
            writer.Position = 0;
            binaryWriter.Write(lenghtAsBits[0]);
            binaryWriter.Write(lenghtAsBits[1]);
            binaryWriter.Write(lenghtAsBits[2]);
            binaryWriter.Write(lenghtAsBits[3]);
            writer.Position = 10;
            binaryWriter.Write(blockStr);
        }

        public void Dispose()
        {
            DisposeAllThrreads();

            if (treadsWriter != null)
            {
                treadsWriter.Close();
                treadsWriter = null;
            }

        }
    }
}



