using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class MethodSymbolExtensions
    {
        public static IMethodBodyOperation GetMethodBodyOperation(this IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            var methodSyntax = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

            if (methodSyntax is null)
                return null;

            IOperation op = null;

            try
            {
                op = semanticModel.GetOperation(methodSyntax);
            }
            catch(ArgumentException ex)
            {
                return null;
            }

            return op as IMethodBodyOperation;
        }
    }
}
