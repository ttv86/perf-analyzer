using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string source = File.ReadAllText(@"d:\users\timo\documents\visual studio 2017\Projects\PerformanceAnalyzer\ConsoleTester\TestClass.cs");
            

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            //CodeStringBuilder sb = new CodeStringBuilder();
            var root = (CompilationUnitSyntax)tree.GetRoot();
            //System.IO.File.WriteAllText(@"d:\users\timo\documents\visual studio 2015\Projects\RoslynTest\RoslynTest\output.cs", sb.ToString());
            //OutputTree(root, 0);

            MetadataReference[] references = new MetadataReference[] {
                MetadataReference.CreateFromFile(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll"),
                MetadataReference.CreateFromFile(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"),
                MetadataReference.CreateFromFile(@"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"),
            };

            CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            CSharpCompilation comp = CSharpCompilation.Create("Tester", new SyntaxTree[] { tree }, references, options);

            SemanticModel semModel = comp.GetSemanticModel(tree, true);
            //SourceInterpreter.SemanticModel = semModel;

            PerformanceAnalyzer.DictionaryAnalyzer analyzer = new PerformanceAnalyzer.DictionaryAnalyzer(semModel);
            var memberAccesses = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in memberAccesses)
            {
                analyzer.AnalyzeMethod(method, ShowMessage);
            }

            //var elementAccesses = root.DescendantNodes().OfType<ElementAccessExpressionSyntax>().ToList();

            //SourceInterpreter.WriteItem(root, sb);
            var emitter = comp.Emit(
                @"D:\Users\TIMO\Documents\visual studio 2015\Projects\RoslynTest\RoslynTest\test.exe",
                @"D:\Users\TIMO\Documents\visual studio 2015\Projects\RoslynTest\RoslynTest\test.pdb");
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
