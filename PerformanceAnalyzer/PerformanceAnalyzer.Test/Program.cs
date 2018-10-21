// <copyright file="Program.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Main executable that runs all the tests included in this project.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Program main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Returns number of failed tests.</returns>
        private static async Task<int> Main(string[] args)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            int failCount = 0;
            bool waitEnd = false;
            if (args.Contains("/wait"))
            {
                waitEnd = true;
            }

            var testInstance = new MemoizationUnitTests();
            await testInstance.TestSearchOnForEach();
            /*
            var testMethods = typeof(MemoizationUnitTests).GetMethods();
            foreach (var testMethod in testMethods)
            {
                if (testMethod.CustomAttributes.Any(x => x.AttributeType == typeof(TestMethodAttribute)))
                {
                    var expectedExceptions = testMethod.CustomAttributes.OfType<ExpectedExceptionAttribute>().Select(x => x.ExceptionType).ToList();
                    Console.ForegroundColor = defaultColor;
                    Console.WriteLine(testMethod.Name);
                    try
                    {
                        testMethod.Invoke(testInstance, new object[0]);
                        if (expectedExceptions.Count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("OK");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Expected an exception, but result was a success");
                            failCount++;
                        }
                    }
                    catch (Exception caughtException)
                    {
                        if (expectedExceptions.Contains(caughtException.GetType()))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("OK");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(caughtException.GetType().Name + ": " + caughtException.Message);
                            failCount++;
                        }
                    }
                }
            }
            // Restore default text color;
            Console.ForegroundColor = defaultColor;
            */
            if (waitEnd)
            {
                Console.WriteLine("Press any key to continue. . .");
                Console.ReadKey();
            }

            return failCount;
        }
    }
}
