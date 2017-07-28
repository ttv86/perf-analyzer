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

        protected ExecutionPath SplitMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback)
        {
            ExecutionPath path = new ExecutionPath();
            ExecutionPath.Node startOfMethod = path.Root;
            startOfMethod.Name = "Start of method";
            ExecutionPath.Node endOfMethod = new ExecutionPath.Node()
            {
                Name = "End of method"
            };

            SplitInfo splitInfo = new SplitInfo() { Callback = callback, MethodEndNode = endOfMethod };
            ExecutionPath.Node lastNode = SplitAndAnalyzeBlock(startOfMethod, method.Body, splitInfo);
            if (lastNode != null)
            {
                lastNode.CreatePathTo(endOfMethod);
            }

            return path;
        }

        private ExecutionPath.Node SplitAndAnalyzeBlock(ExecutionPath.Node prevNode, StatementSyntax statement, SplitInfo splitInfo)
        {
            if (statement is BlockSyntax block)
            {
                return SplitAndAnalyzeBlock(prevNode, block.Statements, splitInfo);
            }
            else
            {
                return SplitAndAnalyzeBlock(prevNode, new StatementSyntax[] { statement }, splitInfo);
            }
        }
        
        /// <summary>
        /// Returns new end node for a block of statements, or a null if all code paths leave the block.
        /// </summary>
        private ExecutionPath.Node SplitAndAnalyzeBlock(ExecutionPath.Node startNode, IEnumerable<StatementSyntax> statements, SplitInfo splitInfo)
        {
            ExecutionPath.Node prevNode = startNode;
            foreach (StatementSyntax statement in statements)
            {
                ExecutionPath.Node statementNode = new ExecutionPath.Node() { SyntaxNode = statement };
                prevNode.CreatePathTo(statementNode);
                switch (statement)
                {
                    // If & switch:
                    case IfStatementSyntax ifStatement:
                        ExecutionPath.Node beginIfNode = statementNode;
                        statementNode.Name = "Begin if";
                        ExecutionPath.Node endIfNode = new ExecutionPath.Node() { Name = "End if" };

                        ExecutionPath.Node newResultNode = new ExecutionPath.Node();

                        ExecutionPath.Node ifBodyEndNode = SplitAndAnalyzeBlock(beginIfNode, ifStatement.Statement, splitInfo);
                        ifBodyEndNode?.CreatePathTo(endIfNode);

                        if (ifStatement.Else?.Statement != null)
                        {
                            ExecutionPath.Node elseBodyEndNode = SplitAndAnalyzeBlock(beginIfNode, ifStatement.Else.Statement, splitInfo);
                            elseBodyEndNode?.CreatePathTo(endIfNode);
                        }
                        else
                        {
                            // If there is no else statement, jump from the if to the end of its body.
                            beginIfNode.CreatePathTo(endIfNode);
                        }

                        if (endIfNode.PreviousNodes.Count == 0)
                        {
                            return null; // No execution paths left. Leave current block.
                        }
                        else
                        {
                            statementNode = endIfNode;
                        }

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
                        prevNode.CreatePathTo(splitInfo.LoopStartNode);
                        return null;
                    case BreakStatementSyntax breakStatement:
                        prevNode.CreatePathTo(splitInfo.LoopEndNode);
                        return null;

                    // Return:
                    case ReturnStatementSyntax returnStatement:
                        statementNode.CreatePathTo(splitInfo.MethodEndNode);
                        return null;

                    //BlockSyntax
                    //CheckedStatementSyntax
                    //ForEachVariableStatementSyntax
                    //EmptyStatementSyntax
                    //ExpressionStatementSyntax
                    //FixedStatementSyntax
                    //GotoStatementSyntax
                    //LabeledStatementSyntax
                    //LocalDeclarationStatementSyntax
                    //LocalFunctionStatementSyntax
                    //LockStatementSyntax
                    //ThrowStatementSyntax
                    //UnsafeStatementSyntax
                    //UsingStatementSyntax
                    //YieldStatementSyntax
                    //default:
                    //    edge.Statements.Add(statement);
                    //    break;
                }

                prevNode = statementNode;
            }

            return prevNode;
        }

        private void AnalyzeModel(SemanticModelAnalysisContext context)
        {
            this.SemanticModel = context.SemanticModel;
            var methods = context.SemanticModel.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    var executionPath = SplitMethod(method, context.ReportDiagnostic);
                }
            }
        }

        private class SplitInfo
        {
            public Action<Diagnostic> Callback { get; set; }

            public ExecutionPath.Node MethodEndNode { get; set; }

            public ExecutionPath.Node LoopStartNode { get; set; }

            public ExecutionPath.Node LoopEndNode { get; set; }
        }
    }
}
