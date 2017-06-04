namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    public abstract class AnalyzerBase : DiagnosticAnalyzer
    {
        internal SemanticModel SemanticModel { get; set; }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSemanticModelAction(AnalyzeModel);
        }

        protected abstract void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback);

        private ExecutionPath.Node SplitAndAnalyzeBlock(ExecutionPath.Node prevNode, StatementSyntax statement, Action<Diagnostic> callback)
        {
            if (statement is BlockSyntax block)
            {
                return SplitAndAnalyzeBlock(prevNode, block.Statements, callback);
            }
            else
            {
                return SplitAndAnalyzeBlock(prevNode, new StatementSyntax[] { statement }, callback);
            }
        }

        private ExecutionPath.Node SplitAndAnalyzeBlock(ExecutionPath.Node prevNode, IEnumerable<StatementSyntax> statements, Action<Diagnostic> callback)
        {
            var resultNode = new ExecutionPath.Node();
            ExecutionPath.Edge edge = prevNode.CreatePathTo(resultNode);
            foreach (StatementSyntax statement in statements)
            {
                switch (statement)
                {
                    // If & switch:
                    case IfStatementSyntax ifStatement:
                        edge.Statements.Add(ifStatement.Condition);
                        ExecutionPath.Node ifStartNode = resultNode;
                        ExecutionPath.Node ifEndNode = new ExecutionPath.Node();
                        ExecutionPath.Node newResultNode = new ExecutionPath.Node();

                        ExecutionPath.Node ifBodyEndNode = SplitAndAnalyzeBlock(ifStartNode, ifStatement.Statement, callback);
                        ifBodyEndNode.CreatePathTo(ifEndNode);

                        if (ifStatement.Else?.Statement != null)
                        {
                            ExecutionPath.Node elseBodyEndNode = SplitAndAnalyzeBlock(ifStartNode, ifStatement.Else.Statement, callback);
                            elseBodyEndNode.CreatePathTo(ifEndNode);
                        }
                        else
                        {
                            ifStartNode.CreatePathTo(ifEndNode);
                        }

                        resultNode = newResultNode;
                        edge = ifEndNode.CreatePathTo(newResultNode);
                        break;
                    case SwitchStatementSyntax switchStatement:
                        break;

                    // Loops:
                    case ForStatementSyntax forStatement:
                        break;
                    case ForEachStatementSyntax forEachStatement:
                        break;
                    case DoStatementSyntax doStatement:
                        break;
                    case WhileStatementSyntax whileStatement:
                        break;
                    case TryStatementSyntax tryStatement:
                        break;

                    // Continue & Break:
                    case ContinueStatementSyntax continueStatement:
                        break;
                    case BreakStatementSyntax breakStatement:
                        break;

                    // Return:
                    case ReturnStatementSyntax returnStatement:
                        break;

                    default:
                        edge.Statements.Add(statement);
                        break;
                }
            }

            return resultNode;
        }

        private void AnalyzeModel(SemanticModelAnalysisContext context)
        {
            this.SemanticModel = context.SemanticModel;
            var methods = context.SemanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    ExecutionPath path = new ExecutionPath();
                    SplitAndAnalyzeBlock(path.Root, method.Body.Statements, context.ReportDiagnostic);
                    path.ToString();
                }
            }
        }
    }
}
