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
    }
}
