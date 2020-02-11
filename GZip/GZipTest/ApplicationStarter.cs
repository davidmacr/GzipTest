using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GZipTest.Enums;
using GZipTest.Processor;

namespace GZipTest
{
    internal class ApplicationStarter
    {
        const int ArgumentCount = 2;

        internal int StartApp(string[] args)
        {
            var isValid = ValidateParamters(args);

            if (isValid != ExitCode.OK)
            {
                ShowApplicationExample();
                return (int)isValid;
            }
            Argument actionToExecute = ParseParameters(args);
            var startTime = DateTime.Now;
            var manager = new ProcessManager(actionToExecute);
            ExitCode returnValue = manager.RunTask();
            var endTime = DateTime.Now;
            Console.WriteLine($"Started at {startTime.ToString()} -- Ended at: {endTime.ToString()}");
            return (int)returnValue;
        }


        protected ExitCode ValidateParamters(string[] args)
        {
            if (args.Length < ArgumentCount)
            {
                Console.WriteLine($"This application requires {ArgumentCount} arguments, please check them");
                return ExitCode.MissingParameter;
            }

            var command = (args[0].ToLower()) switch
            {
                "decompress" => CommandInput.Decompress,
                "compress" => CommandInput.Compress,
                _ => CommandInput.Invalid,
            };

            if (command == CommandInput.Invalid)
            {
                Console.WriteLine($"Argument {command} is not valid");
                return ExitCode.InvalidCommand;
            }

            if (string.IsNullOrEmpty(args[1]))
            {
                Console.WriteLine("Argument file is not defined");
                return ExitCode.InvalidFileName;
            }

            if (!(File.Exists(args[1])))
            {
                Console.WriteLine($"File: {args[1]}, does not exist.");
                return ExitCode.FileNameDoesNotExist;
            }

            return ExitCode.OK;
        }


        protected Argument ParseParameters(string[] args)
        {
            var data = new Argument()
            {
                Command = (args[0].ToLower()) switch
                {
                    "decompress" => CommandInput.Decompress,
                    "compress" => CommandInput.Compress,
                    _ => throw new ArgumentException($"Invalid parameter {args[0]}"),
                },
                FileToProcess = args[1]
            };
            data.CleanUpOutput = false;
            if (args.Length == 3)
            {
                data.CleanUpOutput = "Y,y".Contains(args[2]);
            }

            return data;
        }

        protected void ShowApplicationExample()
        {
            Console.WriteLine();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("This Application accepts only 2 different commands:");
            Console.WriteLine("   Compress");
            Console.WriteLine("   Decompress");
            Console.WriteLine("Requires a file that exists");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("Examples:");
            Console.WriteLine(@"    GZipTest.exe compress c:\Test\myfile.text c:\Test\myfileFolder");
            Console.WriteLine(@"    GZipTest.exe compress c:\Test\myfileFolder\myfile.gz c:\Test\myfile.text");
            Console.WriteLine("-------------------------------------------------------------------------------");

        }
    }
}
