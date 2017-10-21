// <copyright file="DiagnosticVerifier.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace TestHelper
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;
    using Test = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Runs an analyser for given source codes.
    /// </summary>
    public static class DiagnosticVerifier
    {
        /// <summary>
        /// Called to test a C# DiagnosticAnalyzer when applied on the single inputted string as a source
        /// Note: input a DiagnosticResult for each Diagnostic expected
        /// </summary>
        /// <typeparam name="T">Type of the analyzer.</typeparam>
        /// <param name="source">A class in the form of a string to run the analyzer on</param>
        /// <param name="expected"> DiagnosticResults that should appear after the analyzer is run on the source</param>
        /// <returns>A collection of created analyzer messages.</returns>
        internal static Task<Diagnostic[]> CreateAndRunAnalyzerAsync<T>(string source, params DiagnosticResult[] expected) where T : DiagnosticAnalyzer
        {
            T analyzer = Activator.CreateInstance<T>();
            return DiagnosticVerifier.RunAnalyzerAsync(analyzer, GetDocuments(new[] { source }));
        }

        /// <summary>
        /// Given an analyzer and a document to apply it to, run the analyzer and gather an array of diagnostics found in it.
        /// The returned diagnostics are then ordered by location in the source document.
        /// </summary>
        /// <param name="analyzer">The analyzer to run on the documents</param>
        /// <param name="documents">The Documents that the analyzer will be run on</param>
        /// <returns>An IEnumerable of Diagnostics that surfaced in the source code, sorted by Location</returns>
        private static async Task<Diagnostic[]> RunAnalyzerAsync(DiagnosticAnalyzer analyzer, Document[] documents)
        {
            var projects = new HashSet<Project>();
            foreach (var document in documents)
            {
                projects.Add(document.Project);
            }

            var diagnostics = new List<Diagnostic>();
            foreach (var project in projects)
            {
                var compilationWithAnalyzers = (await project.GetCompilationAsync()).WithAnalyzers(ImmutableArray.Create(analyzer));
                var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
                diagnostics.AddRange(diags);
            }

            return diagnostics.ToArray();
        }

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a project and return the documents and spans of it.
        /// </summary>
        /// <param name="sources">Classes in the form of strings</param>
        /// <returns>A Tuple containing the Documents produced from the sources and their TextSpans if relevant</returns>
        private static Document[] GetDocuments(string[] sources)
        {
            var project = CreateProject(sources);
            var documents = project.Documents.ToArray();

            if (sources.Length != documents.Length)
            {
                throw new SystemException("Amount of sources did not match amount of Documents created");
            }

            return documents;
        }

        /// <summary>
        /// Create a Roslyn project using the inputted strings as sources.
        /// </summary>
        /// <param name="sources">Sources as a collection of strings</param>
        /// <returns>A Project created out of the Documents created from the source strings</returns>
        private static Project CreateProject(string[] sources)
        {
            var projectId = ProjectId.CreateNewId(debugName: "TestProject");

            // Creates a solution with some of most used references.
            // This is just for the unit testing. Actual analyzers should work with any references.
            var solution = new AdhocWorkspace()
                    .CurrentSolution
                    .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
                    .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    /*.AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location))
                    .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location))*/;

            int count = 0;
            foreach (var source in sources)
            {
                // All filenames of faked input files are in the format of Test[0-9]+.cs
                var newFileName = $"Test{count}.cs";
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, SourceText.From(source));
                count++;
            }

            return solution.GetProject(projectId);
        }
    }
}