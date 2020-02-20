using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace GZipTest.GZip
{
    /// <summary>
    /// Used to notify errors to main thread
    /// </summary>
    /// <param name="blockName"></param>
    public delegate void ErrorOccured(string blockName);

    public class GZipMain
    {
        public readonly long _blockSize;
        public readonly Argument _argument;
        public readonly FileBlock _fileBlock;
        public readonly Semaphore _pool;
        public readonly Mutex _readerMutex;
        public readonly Mutex _writerMutex;
        public static BinaryReader _fileData;
        public readonly FileStream _fileWriter;
        public bool _canceled = false;

        public GZipMain(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex reader, Mutex writer, FileStream threadsWriter)
        {
            _argument = argument;
            _fileBlock = fileBlock;
            _pool = pool;
            _blockSize = blockSize;
            _readerMutex = reader;
            _writerMutex = writer;
            _fileWriter = threadsWriter;
        }


        /// <summary>
        /// Get a block from a file
        /// </summary>
        /// <param name="offset">startup point to read in the file</param>
        /// <param name="required">block of the file to read</param>
        /// <returns></returns>
        public byte[] GetData(long offset, int required)
        {
            byte[] data;
            try
            {
                _readerMutex.WaitOne();
                if (_fileData == null)
                {
                    _fileData = new BinaryReader(File.Open(_argument.FileToProcess, FileMode.Open));
                }
                _fileData.BaseStream.Seek(offset, SeekOrigin.Begin);
                data = _fileData.ReadBytes(required);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                _readerMutex.ReleaseMutex();
            }

            return data;
        }

        /// <summary>
        /// Event notifier that the current thread must be stopped
        /// </summary>
        /// <remarks>
        /// This event is used in case any other thread fails
        /// </remarks>
        public void CancelThread(object sender, EventArgs e)
        {
            this._canceled = true;
        }

        public void DisposeThread(object sender, EventArgs e)
        {
            lock (_argument)
            {
                if (_fileData != null)
                {
                    _fileData.Close();
                    _fileData = null;

                }
            }
        }


    }
}
