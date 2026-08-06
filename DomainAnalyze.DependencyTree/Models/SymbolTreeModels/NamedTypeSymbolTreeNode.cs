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
    public class NamedTypeSymbolTreeNode
    {
        public NamedTypeSymbolTree Tree { get; set; }
        //public HashSet<NamedTypeSymbolTreeNode> Roots { get; set; } = new HashSet<NamedTypeSymbolTreeNode>();
        public List<FieldSymbolSubnode> ParentList { get; set; } = new List<FieldSymbolSubnode>();
        public List<FieldSymbolSubnode> ChildrenList { get; set; } = new List<FieldSymbolSubnode>();
        public INamedTypeSymbol Symbol { get; set; }

        public NamedTypeSymbolTreeNode AddChildrenNode(IFieldSymbol fieldSymbol, INamedTypeSymbol implementation)
        {

            var childNode = new NamedTypeSymbolTreeNode()
            {
                Symbol = implementation,
                //Roots = this.Roots,
                Tree = this.Tree
            };

            if (Tree.Nodes.TryGetValue(childNode, out var existingNode))
            {
                childNode = existingNode;
            }
            else
            {
                Tree.Nodes.Add(childNode);
            }

            var field = new FieldSymbolSubnode
            {
                Tree = this.Tree,
                FieldSymbol = fieldSymbol,
                ContainingTypeNode = this,
                FieldTypeNode = childNode
            };

            this.Tree.Fields.Add(field);

            childNode.ParentList.Add(field);

            ChildrenList.Add(field);

            return childNode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Symbol.Name, this.Symbol.ContainingNamespace.NamespaceFullName());
        }
        public override bool Equals(object symbol)
        {
            if(symbol is not NamedTypeSymbolTreeNode node)
                return false;

            if (node.Symbol is null)
                return false;

            return this.Symbol.Name == node.Symbol.Name && this.Symbol.ContainingNamespace.NamespaceFullName() == node.Symbol.ContainingNamespace.NamespaceFullName();
        }
    }
}
