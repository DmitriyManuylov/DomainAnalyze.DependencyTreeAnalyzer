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
    public class NamedTypeEqualityComparer : IEqualityComparer<INamedTypeSymbol>
    {
        private static readonly NamedTypeEqualityComparer instance = new();
        public bool Equals(INamedTypeSymbol x, INamedTypeSymbol y)
        {
            return x.Name == y.Name && x.ContainingNamespace.NamespaceFullName() == y.ContainingNamespace.NamespaceFullName();
        }

        public int GetHashCode([DisallowNull] INamedTypeSymbol obj)
        {
            return HashCode.Combine(obj.Name, obj.ContainingNamespace.NamespaceFullName());
        }

        public static NamedTypeEqualityComparer Instance => instance;
    }

}
