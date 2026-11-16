using Microsoft.CodeAnalysis;

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
