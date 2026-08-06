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
    public class FieldSymbolEqualityComparer : IEqualityComparer<IFieldSymbol>
    {
        public static FieldSymbolEqualityComparer Instance => new FieldSymbolEqualityComparer();

        public bool Equals(IFieldSymbol x, IFieldSymbol y)
        {
            return x.Name == y.Name && NamedTypeEqualityComparer.Instance.Equals(x.Type as INamedTypeSymbol, y.Type as INamedTypeSymbol) && NamedTypeEqualityComparer.Instance.Equals(x.ContainingType, y.ContainingType);
        }

        public int GetHashCode([DisallowNull] IFieldSymbol obj)
        {
            return HashCode.Combine(obj.Name, obj.Type.Name, obj.Type.ContainingNamespace.NamespaceFullName(), obj.ContainingType.Name, obj.ContainingType.ContainingNamespace.NamespaceFullName());
        }
    }
}
