using System;
using System.Collections.Generic;
using System.Text;

namespace GZipTest.Enums
{
    public enum ExitCode:int
    {
        OK = 0,
        ApplicationError = 1,
        MissingParameter =2,
        InvalidCommand = 3,
        InvalidFileName = 4,
        ThreadsCancelledByError = 5,
        FileNameDoesNotExist = 6,
    }
}
