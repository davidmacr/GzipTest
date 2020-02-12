using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GZipTest.Data;
using GZipTest.Enums;
using GZipTest.Processor;

namespace GZipTest
{
    internal class ApplicationStarter
    {
        /// <summary>
        /// Number of parameters required by the application
        /// </summary>
        const int ArgumentCount = 2;
        /// <summary>
        /// Entry point of the app
        /// </summary>
        /// <param name="args">list of arguments</param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
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
            if (returnValue == ExitCode.OK)
            {
                var endTime = DateTime.Now;
                Console.WriteLine($"Started at {startTime.ToString()} -- Ended at: {endTime.ToString()}");
            }
            return (int)returnValue;
        }

        /// <summary>
        /// Check that parameters has valid values
        /// </summary>
        /// <param name="args">List of parameters</param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
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

            return ExitCode.OK;
        }

        /// <summary>
        /// Create an instance of object <c>Argument</c> with recieve parameters
        /// </summary>
        /// <param name="args">List of parameters</param>
        /// <returns><c>Argument</c> with data received</returns>
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


            return data;
        }

        /// <summary>
        /// Shows application parameters and examples
        /// </summary>
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
