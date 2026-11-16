using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using DomainAnalyze.DependencyTree.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.SymbolTreeModels
{
    /// <summary>
    /// Модель - обертка над IFieldSymbol, хранящая позиции в дереве DI-зависимостей типа данных поля и типа, содержащего поле.
    /// </summary>
    public class FieldSymbolSubnode
    {
        public NamedTypeSymbolTree Tree { get; set; }
        public NamedTypeSymbolTreeNode ContainingTypeNode { get; set; }
        public NamedTypeSymbolTreeNode FieldTypeNode { get; set; }
        public IFieldSymbol FieldSymbol { get; set; }
        public override bool Equals(object obj)
        {
            if (obj == this)
                return true;

            if (obj == null)
                return false;

            if (obj is not FieldSymbolSubnode other)
                return false;

            return this.FieldSymbol.Name == other.FieldSymbol.Name && NamedTypeEqualityComparer.Instance.Equals(this.FieldSymbol.Type as INamedTypeSymbol, other.FieldSymbol.Type as INamedTypeSymbol) && NamedTypeEqualityComparer.Instance.Equals(this.FieldSymbol.ContainingType, other.FieldSymbol.ContainingType);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.FieldSymbol.Name, this.FieldSymbol.Type.Name, this.FieldSymbol.Type.ContainingNamespace.NamespaceFullName(), this.FieldSymbol.ContainingType.Name, this.FieldSymbol.ContainingType.ContainingNamespace.NamespaceFullName());
        }
    }
}
