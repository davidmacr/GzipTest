using System;
using System.IO;
using GZipTest.Enums;

namespace GZipTest
{
    class Program
    {
        static int Main(string[] args)
        {
            var  app = new ApplicationStarter();
            return app.StartApp(args);
        }
    }
}
