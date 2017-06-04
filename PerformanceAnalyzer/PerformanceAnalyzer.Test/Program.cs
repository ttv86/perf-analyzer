// <copyright file="Program.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer.Test
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Main executable that runs all the tests included in this project.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Program main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        private static void Main(string[] args)
        {
            UnitTests testInstance = new UnitTests();
            var testMethods = typeof(UnitTests).GetMethods();
            foreach (var testMethod in testMethods)
            {
                if (testMethod.CustomAttributes.Any(x => x.AttributeType == typeof(TestMethodAttribute)))
                {
                    testMethod.Invoke(testInstance, new object[0]);
                }
            }
        }

        private static void X()
        {
            string currentPath = Environment.CurrentDirectory;
            string source = File.ReadAllText(currentPath + @"TestClass.cs");

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            string winPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            MetadataReference[] references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(winPath + @"\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll"),
                MetadataReference.CreateFromFile(winPath + @"\Microsoft.NET\Framework64\v4.0.30319\System.dll"),
                MetadataReference.CreateFromFile(winPath + @"\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"),
            };

            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            CSharpCompilation comp = CSharpCompilation.Create("Tester", new SyntaxTree[] { tree }, references, options);

            SemanticModel semModel = comp.GetSemanticModel(tree, true);

            PerformanceAnalyzer.MemoizationAnalyzer analyzer = new PerformanceAnalyzer.MemoizationAnalyzer();
            analyzer.SemanticModel = semModel;
            var memberAccesses = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in memberAccesses)
            {
                analyzer.AnalyzeMethod(method, ShowMessage);
            }

            var emitter = comp.Emit(
                currentPath + @"\test.exe",
                currentPath + @"\test.pdb");
            if (!emitter.Success)
            {
                emitter.Diagnostics.ToString();
            }

            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        private static void ShowMessage(Diagnostic diagnostic)
        {
            string msg = diagnostic.GetMessage();
            Debug.WriteLine(msg);
            Console.WriteLine(msg);
        }
    }
}
