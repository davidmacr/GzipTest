using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GZipTest.Data;
using GZipTest.Enums;
using GZipTest.Processor;

namespace GZipTest
{
    public class ApplicationStarter
    {
        /// <summary>
        /// Number of parameters required by the application
        /// </summary>
        const int ArgumentCount = 3;
        /// <summary>
        /// Entry point of the app
        /// </summary>
        /// <param name="args">list of arguments</param>
        /// <returns><c>ExitCode</c> with the result of the operations</returns>
        public int StartApp(string[] args)
        {
            try
            {
                var isValid = ValidateParamters(args);

                if (isValid != ExitCode.OK)
                {
                    ShowApplicationExample();
                    return ShowApplicationResult(isValid);
                }
                Argument actionToExecute = ParseParameters(args);
                var startTime = DateTime.Now;
                using (var manager = new ProcessManager(actionToExecute))
                {
                    ExitCode returnValue = manager.RunTask();

                    if (returnValue == ExitCode.OK)
                    {
                        var endTime = DateTime.Now;
                        Console.WriteLine($"Started at {startTime.ToString()} -- Ended at: {endTime.ToString()}");
                    }
                    return ShowApplicationResult(returnValue);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a fatal error running the application. Error: {ex.Message}");
                return ShowApplicationResult(ExitCode.ApplicationError);
            }
        }


        public int ShowApplicationResult(ExitCode code)
        {
            switch (code)
            {
                case ExitCode.OK:
                    return 0;
                case ExitCode.ApplicationError:
                    Console.WriteLine($"There was an unexpected exeption during the execution, Error Code: {(int)ExitCode.ApplicationError}");
                    return 1;
                case ExitCode.MissingParameter:
                    Console.WriteLine($"Parameters are missing or were introduced incorrectly, Error Code: {(int)ExitCode.MissingParameter}");
                    return 1;
                case ExitCode.InvalidCommand:
                    Console.WriteLine($"This application only allow compress and decompress commands, Error Code: {(int)ExitCode.InvalidCommand}");
                    return 1;
                case ExitCode.InvalidFileName:
                    Console.WriteLine($"File name is invalid or do not exists, Error Code: {(int)ExitCode.ApplicationError}");
                    return 1;
                case ExitCode.ThreadsCancelledByError:
                    Console.WriteLine($"The process was cancelled due to unexpected exceptions, Error Code: {(int)ExitCode.ThreadsCancelledByError}");
                    return 1;
                case ExitCode.FileNameDoesNotExist:
                    Console.WriteLine($"File to process do not exists, Error Code: {(int)ExitCode.FileNameDoesNotExist}");
                    return 1;
                case ExitCode.InputFileDoesNotExists:
                    Console.WriteLine($"File to process do not exists, Error Code: {(int)ExitCode.InputFileDoesNotExists}");
                    return 1;
                case ExitCode.OutcommeFileExists:
                    Console.WriteLine($"Outcome file exists, Error Code: {(int)ExitCode.OutcommeFileExists}");
                    return 1;

                default:
                    return 1;
            }
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
                var message = "Original file parameter is not valid";
                if (command == CommandInput.Decompress) { message = "Archive file parameter is not valid"; }
                Console.WriteLine(message);
                return ExitCode.InvalidFileName;
            }

            if (string.IsNullOrEmpty(args[2]))
            {
                var message = "Archive file parameter is not valid";
                if (command == CommandInput.Decompress) { message = "Deconmpressing file parameter is not valid"; }
                Console.WriteLine(message);
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
                FileToProcess = args[1],
                OutcomeFile = args[2]
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
