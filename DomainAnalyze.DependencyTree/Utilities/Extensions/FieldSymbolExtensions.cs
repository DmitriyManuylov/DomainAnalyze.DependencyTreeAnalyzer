using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class FieldSymbolExtensions
    {
        public static IPropertySymbol GetAssociatedProperty(this IFieldSymbol fieldSymbol)
        {
            return fieldSymbol.AssociatedSymbol as IPropertySymbol;
        }

        public static bool CustomEquals(this IFieldSymbol fieldSymbol1, IFieldSymbol fieldSymbol2)
        {
            return FieldSymbolEqualityComparer.Instance.Equals(fieldSymbol1, fieldSymbol2);
        }
    }
}
