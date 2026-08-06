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
    public class FieldSymbolSubnode
    {
        public NamedTypeSymbolTree Tree { get; set; }
        public NamedTypeSymbolTreeNode ContainingTypeNode { get; set; }
        public NamedTypeSymbolTreeNode FieldTypeNode { get; set; }
        public IFieldSymbol FieldSymbol { get; set; }
        public bool Equals(IFieldSymbol fieldSymbol)
        {
            return this.FieldSymbol.Name == fieldSymbol.Name && NamedTypeEqualityComparer.Instance.Equals(this.FieldSymbol.Type as INamedTypeSymbol, fieldSymbol.Type as INamedTypeSymbol) && NamedTypeEqualityComparer.Instance.Equals(this.FieldSymbol.ContainingType, fieldSymbol.ContainingType);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.FieldSymbol.Name, this.FieldSymbol.Type.Name, this.FieldSymbol.Type.ContainingNamespace.NamespaceFullName(), this.FieldSymbol.ContainingType.Name, this.FieldSymbol.ContainingType.ContainingNamespace.NamespaceFullName());
        }
    }
}
