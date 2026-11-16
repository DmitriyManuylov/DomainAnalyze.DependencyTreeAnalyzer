using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class OperationExtensions
    {
        public static IMethodBodyOperation GetOuterMethodOperation(this IOperation operation)
        {
            var op = operation;

            while (op.Kind != OperationKind.MethodBody)
            {
                op = op.Parent;
            }

            return op as IMethodBodyOperation;
        }

        public static IMethodSymbol GetOuterMethodSymbol(this IOperation operation)
        {
            var op = operation;

            while (op.Kind != OperationKind.MethodBody)
            {
                op = op.Parent;
            }

            return (op as IMethodBodyOperation).GetMethodSymbol();
        }

        public static IMethodSymbol GetMethodSymbol(this IMethodBodyOperation operation)
        {
            var methodSyntax = operation.Syntax;

            var methodSymbol = operation.SemanticModel.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;

            return methodSymbol;
        }
    }
}
