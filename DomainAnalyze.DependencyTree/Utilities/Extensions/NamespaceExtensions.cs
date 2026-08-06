using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class NamespaceExtensions
    {
        public static string NamespaceFullName(this INamespaceSymbol namespaceSymbol)
        {

            var parentNamespace = namespaceSymbol.ContainingNamespace;

            if (string.IsNullOrWhiteSpace(parentNamespace?.Name))
            {
                return namespaceSymbol.Name;
            }

            return $"{parentNamespace.NamespaceFullName()}.{namespaceSymbol.Name}";
        }
    }
}
