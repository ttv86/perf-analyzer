// <copyright file="PathAnalyzer.cs" company="Timo Virkki">
// Copyright (c) Timo Virkki. All rights reserved.
// </copyright>

namespace PerformanceAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public abstract class PathAnalyzer : AnalyzerBase
    {
        protected abstract void AnalyzeMethod(MethodDeclarationSyntax method, ExecutionPath path, Action<Diagnostic> callback);

        protected override void AnalyzeMethod(MethodDeclarationSyntax method, Action<Diagnostic> callback)
        {
            var executionPath = this.SplitMethod(method);
            this.AnalyzeMethod(method, executionPath, callback);
        }

        /// <summary>
        /// Splits method execution to a path where code execution order is uniformally defined.
        /// </summary>
        /// <param name="method">Method to be split.</param>
        /// <returns>Execution path for the method.</returns>
        protected ExecutionPath SplitMethod(MethodDeclarationSyntax method)
        {
            ExecutionPath path = new ExecutionPath();
            ExecutionPathNode startOfMethod = path.Root;
            startOfMethod.Name = "Start of method";
            ExecutionPathNode endOfMethod = new ExecutionPathNode()
            {
                Name = "End of method"
            };

            SplitInfo splitInfo = new SplitInfo() { MethodEndNode = endOfMethod };
            ExecutionPathNode lastNode = this.SplitAndAnalyzeBlock(startOfMethod, method.Body, splitInfo);
            if (lastNode != null)
            {
                lastNode.CreatePathTo(endOfMethod);
            }

            return path;
        }

        private ExecutionPathNode SplitAndAnalyzeBlock(ExecutionPathNode prevNode, StatementSyntax statement, SplitInfo splitInfo)
        {
            if (statement is BlockSyntax block)
            {
                return this.SplitAndAnalyzeBlock(prevNode, block.Statements, splitInfo);
            }
            else
            {
                return this.SplitAndAnalyzeBlock(prevNode, new StatementSyntax[] { statement }, splitInfo);
            }
        }

        /// <summary>
        /// Returns new end node for a block of statements, or a null if all code paths leave the block.
        /// </summary>
        private ExecutionPathNode SplitAndAnalyzeBlock(ExecutionPathNode startNode, IEnumerable<StatementSyntax> statements, SplitInfo splitInfo)
        {
            ExecutionPathNode prevNode = startNode;
            foreach (StatementSyntax statement in statements)
            {
                ExecutionPathNode statementNode = new ExecutionPathNode() { SyntaxNode = statement };
                prevNode.CreatePathTo(statementNode);
                switch (statement)
                {
                    // If & switch:
                    case IfStatementSyntax ifStatement:
                        statementNode = this.SplitIf(ifStatement, statementNode, splitInfo);
                        break;
                    case SwitchStatementSyntax switchStatement:
                        statementNode = this.SplitSwitch(switchStatement, statementNode, splitInfo);
                        break;

                    // Loops:
                    case ForStatementSyntax forStatement:
                        statementNode = this.SplitFor(forStatement, statementNode, splitInfo);
                        break;
                    case ForEachStatementSyntax forEachStatement:
                        statementNode = this.SplitForeach(forEachStatement, statementNode, splitInfo);
                        break;
                    case DoStatementSyntax doStatement:
                        statementNode = this.SplitDo(doStatement, statementNode, splitInfo);
                        break;
                    case WhileStatementSyntax whileStatement:
                        statementNode = this.SplitWhile(whileStatement, statementNode, splitInfo);
                        break;
                    case TryStatementSyntax tryStatement:
                        statementNode = this.SplitTry(tryStatement, statementNode, splitInfo);
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

        /// <summary>
        /// Analyses if-else -block.
        /// </summary>
        /// <param name="ifStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitIf(IfStatementSyntax ifStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginIfNode = statementNode;
            beginIfNode.SyntaxNode = ifStatement.Condition;
            statementNode.Name = "Begin if";
            ExecutionPathNode endIfNode = new ExecutionPathNode() { Name = "End if" };

            ExecutionPathNode ifBodyEndNode = this.SplitAndAnalyzeBlock(beginIfNode, ifStatement.Statement, splitInfo);
            ifBodyEndNode?.CreatePathTo(endIfNode);

            if (ifStatement.Else?.Statement != null)
            {
                ExecutionPathNode elseBodyEndNode = this.SplitAndAnalyzeBlock(beginIfNode, ifStatement.Else.Statement, splitInfo);
                elseBodyEndNode?.CreatePathTo(endIfNode);
            }
            else
            {
                // If there is no else statement, jump from the if to the end of its body.
                beginIfNode.CreatePathTo(endIfNode);
            }

            return endIfNode;
        }

        /// <summary>
        /// Analyses switch-block.
        /// </summary>
        /// <param name="switchStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitSwitch(SwitchStatementSyntax switchStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginSwitchNode = statementNode;
            beginSwitchNode.SyntaxNode = switchStatement.Expression;
            statementNode.Name = "Begin switch";
            bool hasDefault = false;
            ExecutionPathNode endSwitchNode = new ExecutionPathNode() { Name = "End switch" };
            foreach (SwitchSectionSyntax section in switchStatement.Sections)
            {
                ExecutionPathNode startOfBlock = beginSwitchNode;
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

        /// <summary>
        /// Analyses for-block.
        /// </summary>
        /// <param name="forStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitFor(ForStatementSyntax forStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginForNode = statementNode;
            beginForNode.Name = "Begin for";
            beginForNode.SyntaxNode = forStatement.Declaration;

            ExecutionPathNode endForNode = new ExecutionPathNode() { Name = "End for" };

            ExecutionPathNode forTestNode = new ExecutionPathNode() { SyntaxNode = forStatement.Condition };
            beginForNode.CreatePathTo(forTestNode);

            SplitInfo forSplitInfo = splitInfo.Clone();
            forSplitInfo.LoopStartNode = beginForNode;
            forSplitInfo.LoopOrSwitchEndNode = endForNode;

            ExecutionPathNode forBodyEndNode = this.SplitAndAnalyzeBlock(forTestNode, forStatement.Statement, forSplitInfo);
            if (forBodyEndNode == null)
            {
                return null; // No executions paths left.
            }

            ExecutionPathNode forIncrementNode = this.ExpressionSyntaxListToNodes(forStatement.Incrementors, forBodyEndNode);
            forIncrementNode?.CreatePathTo(forTestNode);

            forTestNode.CreatePathTo(endForNode);
            return endForNode;
        }

        /// <summary>
        /// Analyses foreach-block.
        /// </summary>
        /// <param name="forEachStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitForeach(ForEachStatementSyntax forEachStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginForEachNode = statementNode;
            beginForEachNode.Name = "Begin foreach";
            beginForEachNode.SyntaxNode = forEachStatement.Expression;

            ExecutionPathNode endForEachNode = new ExecutionPathNode() { Name = "End foreach" };
            beginForEachNode.CreatePathTo(endForEachNode);

            SplitInfo forEachSplitInfo = splitInfo.Clone();
            forEachSplitInfo.LoopStartNode = beginForEachNode;
            forEachSplitInfo.LoopOrSwitchEndNode = endForEachNode;

            ExecutionPathNode forEachBodyEndNode = this.SplitAndAnalyzeBlock(beginForEachNode, forEachStatement.Statement, forEachSplitInfo);
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

        /// <summary>
        /// Analyses do-block.
        /// </summary>
        /// <param name="doStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitDo(DoStatementSyntax doStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginDoNode = statementNode;
            beginDoNode.Name = "Begin do";

            ExecutionPathNode endDoNode = new ExecutionPathNode();
            endDoNode.Name = "end do";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginDoNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPathNode bodyEnd = this.SplitAndAnalyzeBlock(beginDoNode, doStatement.Statement, doSplitInfo);

            ExecutionPathNode testNode = new ExecutionPathNode();
            testNode.SyntaxNode = doStatement.Condition;
            bodyEnd?.CreatePathTo(testNode);

            testNode.CreatePathTo(endDoNode);
            testNode.CreatePathTo(beginDoNode);
            return endDoNode;
        }

        /// <summary>
        /// Analyses do-while -block.
        /// </summary>
        /// <param name="whileStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitWhile(WhileStatementSyntax whileStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginWhileNode = statementNode;
            beginWhileNode.Name = "Begin while";
            beginWhileNode.SyntaxNode = whileStatement.Condition;

            ExecutionPathNode endDoNode = new ExecutionPathNode();
            endDoNode.Name = "end while";

            SplitInfo doSplitInfo = splitInfo.Clone();
            doSplitInfo.LoopStartNode = beginWhileNode;
            doSplitInfo.LoopOrSwitchEndNode = endDoNode;

            ExecutionPathNode bodyEnd = this.SplitAndAnalyzeBlock(beginWhileNode, whileStatement.Statement, doSplitInfo);
            bodyEnd?.CreatePathTo(beginWhileNode);

            beginWhileNode.CreatePathTo(endDoNode);
            return endDoNode;
        }

        /// <summary>
        /// Analyses try-catch-finally -block.
        /// </summary>
        /// <param name="tryStatement">Current statement</param>
        /// <param name="statementNode">Previous known execution path node.</param>
        /// <param name="splitInfo">Analyser context.</param>
        /// <returns>Final execution path node after the code block.</returns>
        private ExecutionPathNode SplitTry(TryStatementSyntax tryStatement, ExecutionPathNode statementNode, SplitInfo splitInfo)
        {
            ExecutionPathNode beginTryNode = statementNode;
            beginTryNode.Name = "Begin try";

            ExecutionPathNode endTryNode = new ExecutionPathNode();
            endTryNode.Name = "End try";

            ExecutionPathNode lastNode;
            ExecutionPathNode endTryOrFinallyNode = endTryNode; // Initially assume the end try-block node.
            if (tryStatement.Finally != null)
            {
                ExecutionPathNode finallyNode = new ExecutionPathNode();
                finallyNode.Name = "finally";
                lastNode = this.SplitAndAnalyzeBlock(finallyNode, tryStatement.Finally.Block, splitInfo);
                lastNode?.CreatePathTo(endTryNode);
                endTryOrFinallyNode = finallyNode; // There was a finally block. Go there instead.
            }

            lastNode = this.SplitAndAnalyzeBlock(beginTryNode, tryStatement.Block, splitInfo);
            lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.

            // There might be 0 or more catch blocks.
            if (tryStatement.Catches.Count > 0)
            {
                foreach (var catchBlock in tryStatement.Catches)
                {
                    lastNode = this.SplitAndAnalyzeBlock(beginTryNode, catchBlock.Block, splitInfo);
                    lastNode?.CreatePathTo(endTryOrFinallyNode); // After all statements, go to finally or if it does not exist, go to end.
                }
            }
            else if (endTryOrFinallyNode != endTryNode)
            {
                // When there are no catch blocks, but there is a finally block, move execution straight to finally block.
                beginTryNode.CreatePathTo(endTryOrFinallyNode);
            }

            return endTryNode;
        }

        private ExecutionPathNode ExpressionSyntaxListToNodes(IReadOnlyCollection<ExpressionSyntax> statements, ExecutionPathNode previousNode)
        {
            var result = previousNode;
            foreach (var expressionSyntax in statements)
            {
                ExecutionPathNode node = new ExecutionPathNode() { SyntaxNode = expressionSyntax };
                result.CreatePathTo(node);
                result = node;
            }

            return result;
        }

        private class SplitInfo
        {
            public ExecutionPathNode MethodEndNode { get; set; }

            public ExecutionPathNode LoopStartNode { get; set; }

            public ExecutionPathNode LoopOrSwitchEndNode { get; set; }

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
