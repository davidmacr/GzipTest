using GZipTest.Data;
using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace GZipTest.GZip
{
    public class GzipFactory
    {
        public static IGZipper CreateClass(Argument argument, FileBlock fileBlock, Semaphore pool, long blockSize, Mutex reader, Mutex writer, FileStream threadWriter)
        {
            switch (argument.Command)
            {
                case CommandInput.Compress:
                    {
                        return new GZipFiles(argument, fileBlock, pool, blockSize, reader, writer, threadWriter);
                    }
                case CommandInput.Decompress:
                    {
                        return new UnGZipFiles(argument, fileBlock, pool, blockSize, reader, writer);
                    }
                case CommandInput.Invalid:
                default:
                    {
                        throw new ArgumentException("CommandInput is not valid");
                    }
            }
        }
    }
}
