using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceAnalyzer
{
    public abstract class PathAnalyzer : AnalyzerBase
    {
        protected abstract void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback);

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback)
        {
            var executionPath = SplitMethod(method);
            this.AnalyzeMethod(method, executionPath, callback);
        }

        protected ExecutionPath SplitMethod(MethodDeclarationSyntax method)
        {
            ExecutionPath path = new ExecutionPath();
            ExecutionPath.Node startOfMethod = path.Root;
            startOfMethod.Name = "Start of method";
            ExecutionPath.Node endOfMethod = new ExecutionPath.Node()
            {
                Name = "End of method"
            };

            SplitInfo splitInfo = new SplitInfo() { MethodEndNode = endOfMethod };
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
                        statementNode = SplitIf(ifStatement, statementNode, splitInfo);
                        break;
                    case SwitchStatementSyntax switchStatement:
                        statementNode = SplitSwitch(switchStatement, statementNode, splitInfo);
                        break;

                    // Loops:
                    case ForStatementSyntax forStatement:
                        statementNode = SplitFor(forStatement, statementNode, splitInfo);
                        break;
                    case ForEachStatementSyntax forEachStatement:
                        statementNode = SplitForeach(forEachStatement, statementNode, splitInfo);
                        break;
                    case DoStatementSyntax doStatement:
                        statementNode = SplitDo(doStatement, statementNode, splitInfo);
                        break;
                    case WhileStatementSyntax whileStatement:
                        statementNode = SplitWhile(whileStatement, statementNode, splitInfo);
                        break;
                    case TryStatementSyntax tryStatement:
                        statementNode = SplitTry(tryStatement, statementNode, splitInfo);
                        break;

                    // Continue & Break:
                    case ContinueStatementSyntax continueStatement:
                        statementNode.CreatePathTo(splitInfo.LoopStartNode);
                        return null;
                    case BreakStatementSyntax breakStatement:
                        statementNode.CreatePathTo(splitInfo.LoopOrSwitchEndNode);
                        return null;

                    // Return:
                    case ReturnStatementSyntax returnStatement:
                        statementNode.CreatePathTo(splitInfo.MethodEndNode);
                        return null;

                        ////case BlockSyntax
                        ////case CheckedStatementSyntax
                        ////case ForEachVariableStatementSyntax
                        ////case EmptyStatementSyntax
                        ////case ExpressionStatementSyntax
                        ////case FixedStatementSyntax
                        ////case GotoStatementSyntax
                        ////case LabeledStatementSyntax
                        ////case LocalDeclarationStatementSyntax
                        ////case LocalFunctionStatementSyntax
                        ////case LockStatementSyntax
                        ////case ThrowStatementSyntax
                        ////case UnsafeStatementSyntax
                        ////case UsingStatementSyntax
                        ////case YieldStatementSyntax
                        ////default:
                        ////    edge.Statements.Add(statement);
                        ////    break;
                }

                if (statementNode.PreviousNodes.Count == 0)
                {
                    return null; // No execution paths left. Leave current block.
                }
                else
                {
                    prevNode = statementNode;
                }
            }

            return prevNode;
        }

        private ExecutionPath.Node SplitIf(IfStatementSyntax ifStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginIfNode = statementNode;
            beginIfNode.SyntaxNode = ifStatement.Condition;
            statementNode.Name = "Begin if";
            ExecutionPath.Node endIfNode = new ExecutionPath.Node() { Name = "End if" };
            
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

            return endIfNode;
        }

        private ExecutionPath.Node SplitSwitch(SwitchStatementSyntax switchStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginSwitchNode = statementNode;
            beginSwitchNode.SyntaxNode = switchStatement.Expression;
            statementNode.Name = "Begin switch";
            bool hasDefault = false;
            ExecutionPath.Node endSwitchNode = new ExecutionPath.Node() { Name = "End switch" };
            foreach (SwitchSectionSyntax section in switchStatement.Sections)
            {
                ExecutionPath.Node startOfBlock = beginSwitchNode;
                foreach (SwitchLabelSyntax label in section.Labels)
                {
                    switch (label)
                    {
                        case CaseSwitchLabelSyntax caseLabel:
                            break;
                        case CasePatternSwitchLabelSyntax caseWhenLabel:
                            break;
                        case DefaultSwitchLabelSyntax defaultLabel:
                            hasDefault = true;
                            break;
                    }

                    SplitInfo sectionSplitInfo = splitInfo.Clone();
                    sectionSplitInfo.LoopOrSwitchEndNode = endSwitchNode;
                    var endOfSection = this.SplitAndAnalyzeBlock(startOfBlock, section.Statements, sectionSplitInfo);
                }
            }

            if (!hasDefault)
            {
                // If there is no default, jump possibly streight to end.
                beginSwitchNode.CreatePathTo(endSwitchNode);
            }
            
            return endSwitchNode;
        }

        private ExecutionPath.Node SplitFor(ForStatementSyntax forStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginForNode = statementNode;
            beginForNode.Name = "Begin for";
            beginForNode.SyntaxNode = forStatement.Declaration;
            
            ExecutionPath.Node endForNode = new ExecutionPath.Node() { Name = "End for" };

            ExecutionPath.Node forTestNode = new ExecutionPath.Node() { SyntaxNode = forStatement.Condition };
            beginForNode.CreatePathTo(forTestNode);

            SplitInfo forSplitInfo = splitInfo.Clone();
            forSplitInfo.LoopStartNode = beginForNode;
            forSplitInfo.LoopOrSwitchEndNode = endForNode;

            ExecutionPath.Node forBodyEndNode = SplitAndAnalyzeBlock(forTestNode, forStatement.Statement, forSplitInfo);
            if (forBodyEndNode == null)
            {
                return null; // No executions paths left.
            }

            ExecutionPath.Node forIncrementNode = ExpressionSyntaxListToNodes(forStatement.Incrementors, forBodyEndNode);
            forIncrementNode?.CreatePathTo(forTestNode);

            forTestNode.CreatePathTo(endForNode);
            return endForNode;
        }

        private ExecutionPath.Node SplitForeach(ForEachStatementSyntax forEachStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginForEachNode = statementNode;
            beginForEachNode.Name = "Begin foreach";
            beginForEachNode.SyntaxNode = forEachStatement.Expression;

            ExecutionPath.Node endForEachNode = new ExecutionPath.Node() { Name = "End foreach" };
            beginForEachNode.CreatePathTo(endForEachNode);

            SplitInfo forEachSplitInfo = splitInfo.Clone();
            forEachSplitInfo.LoopStartNode = beginForEachNode;
            forEachSplitInfo.LoopOrSwitchEndNode = endForEachNode;

            ExecutionPath.Node forEachBodyEndNode = SplitAndAnalyzeBlock(beginForEachNode, forEachStatement.Statement, forEachSplitInfo);
            if (forEachBodyEndNode == null)
            {
                return null; // No executions paths left.
            }
            else
            {
                forEachBodyEndNode.CreatePathTo(endForEachNode);
            }

            return endForEachNode;
        }

        private ExecutionPath.Node SplitDo(DoStatementSyntax doStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginDoNode = statementNode;
            beginDoNode.Name = "Begin do";

            ExecutionPath.Node endDoNode = new ExecutionPath.Node();
            endDoNode.Name = "end do";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginDoNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPath.Node bodyEnd = SplitAndAnalyzeBlock(beginDoNode, doStatement.Statement, doSplitInfo);

            ExecutionPath.Node testNode = new ExecutionPath.Node();
            testNode.SyntaxNode = doStatement.Condition;
            bodyEnd?.CreatePathTo(testNode);

            testNode.CreatePathTo(endDoNode);
            testNode.CreatePathTo(beginDoNode);
            return endDoNode;
        }

        private ExecutionPath.Node SplitWhile(WhileStatementSyntax whileStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginWhileNode = statementNode;
            beginWhileNode.Name = "Begin while";
            beginWhileNode.SyntaxNode = whileStatement.Condition;

            ExecutionPath.Node endDoNode = new ExecutionPath.Node();
            endDoNode.Name = "end while";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginWhileNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPath.Node bodyEnd = SplitAndAnalyzeBlock(beginWhileNode, whileStatement.Statement, doSplitInfo);
            bodyEnd?.CreatePathTo(beginWhileNode);

            beginWhileNode.CreatePathTo(endDoNode);
            return endDoNode;
        }

        private ExecutionPath.Node SplitTry(TryStatementSyntax tryStatement, ExecutionPath.Node statementNode, SplitInfo splitInfo)
        {
            ExecutionPath.Node beginTryNode = statementNode;
            beginTryNode.Name = "Begin try";

            ExecutionPath.Node endTryNode = new ExecutionPath.Node();
            endTryNode.Name = "End try";

            ExecutionPath.Node lastNode;
            ExecutionPath.Node endTryOrFinallyNode = endTryNode;
            if (tryStatement.Finally != null)
            {
                ExecutionPath.Node finallyNode = new ExecutionPath.Node();
                finallyNode.Name = "finally";
                lastNode = SplitAndAnalyzeBlock(finallyNode, tryStatement.Finally.Block, splitInfo);
                lastNode?.CreatePathTo(endTryNode);
                endTryOrFinallyNode = finallyNode;
            }

            lastNode = SplitAndAnalyzeBlock(beginTryNode, tryStatement.Block, splitInfo);
            lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.

            foreach (var catchBlock in tryStatement.Catches)
            {
                lastNode = SplitAndAnalyzeBlock(beginTryNode, catchBlock.Block, splitInfo);
                lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.
            }

            return endTryNode;
        }

        private ExecutionPath.Node ExpressionSyntaxListToNodes(IReadOnlyCollection<ExpressionSyntax> statements, ExecutionPath.Node previousNode)
        {
            var result = previousNode;
            foreach (var expressionSyntax in statements)
            {
                ExecutionPath.Node node = new ExecutionPath.Node() { SyntaxNode = expressionSyntax };
                result.CreatePathTo(node);
                result = node;
            }

            return result;
        }

        private class SplitInfo
        {
            public ExecutionPath.Node MethodEndNode { get; set; }

            public ExecutionPath.Node LoopStartNode { get; set; }

            public ExecutionPath.Node LoopOrSwitchEndNode { get; set; }

            internal SplitInfo Clone()
            {
                return new SplitInfo()
                {
                    MethodEndNode = this.MethodEndNode,
                    LoopStartNode = this.LoopStartNode,
                    LoopOrSwitchEndNode = this.LoopOrSwitchEndNode
                };
            }
        }
    }
}
