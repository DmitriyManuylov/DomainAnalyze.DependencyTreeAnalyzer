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
    internal class PropertySymbolEqualityComparer : IEqualityComparer<IPropertySymbol>
    {
        private static readonly PropertySymbolEqualityComparer instance = new PropertySymbolEqualityComparer();
        public static PropertySymbolEqualityComparer Instance => instance;

        public bool Equals(IPropertySymbol x, IPropertySymbol y)
        {
            return x.Name == y.Name && NamedTypeEqualityComparer.Instance.Equals(x.Type as INamedTypeSymbol, y.Type as INamedTypeSymbol) && NamedTypeEqualityComparer.Instance.Equals(x.ContainingType, y.ContainingType);
        }

        public int GetHashCode([DisallowNull] IPropertySymbol obj)
        {
            return HashCode.Combine(obj.Name, obj.Type.Name, obj.Type.ContainingNamespace.NamespaceFullName(), obj.ContainingType.Name, obj.ContainingType.ContainingNamespace.NamespaceFullName());
        }
    }
}
