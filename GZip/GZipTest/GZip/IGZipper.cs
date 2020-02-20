using GZipTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GZipTest.GZip
{
    public interface IGZipper
    {
        public event ErrorOccured RaiseError;

        public void CancelThread(object sender, EventArgs e);

        public void DisposeThread(object sender, EventArgs e);

        public void StartProcess();


    }
}
