using DomainAnalyze.DependencyTree.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.EqualityComparers
{
    public class MethodSymbolEqualityComparer : IEqualityComparer<IMethodSymbol>
    {
        private static readonly MethodSymbolEqualityComparer instance = new MethodSymbolEqualityComparer();
        public static MethodSymbolEqualityComparer Instance => instance;

        public bool Equals(IMethodSymbol x, IMethodSymbol y)
        {
            return x.Name == y.Name && NamedTypeEqualityComparer.Instance.Equals(x.ContainingType as INamedTypeSymbol, y.ContainingType as INamedTypeSymbol);
        }

        public int GetHashCode([DisallowNull] IMethodSymbol obj)
        {
            return HashCode.Combine(obj.Name, obj.ContainingType.Name, obj.ContainingType.ContainingNamespace.NamespaceFullName(), obj.ContainingType.Name, obj.ContainingType.ContainingNamespace.NamespaceFullName());
        }
    }
}
