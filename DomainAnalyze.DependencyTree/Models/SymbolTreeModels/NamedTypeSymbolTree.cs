using DomainAnalyze.DependencyTree.Models.CashModels;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.SymbolTreeModels
{
    public class NamedTypeSymbolTree
    {
        public HashSet<NamedTypeSymbolTreeNode> Roots { get; private set; } = new HashSet<NamedTypeSymbolTreeNode>();
        public HashSet<NamedTypeSymbolTreeNode> Nodes { get; private set; } = new HashSet<NamedTypeSymbolTreeNode>();
        public HashSet<FieldSymbolSubnode> Fields { get; private set; } = new HashSet<FieldSymbolSubnode>();
        public NamedTypeSymbolTreeNode AddRoot(NamedTypeCashModel rootSymbol)
        {
            var root = new NamedTypeSymbolTreeNode()
            {
                Symbol = rootSymbol,
                ChildrenList = new List<FieldSymbolSubnode>(),
                ParentList = null,
                Tree = this
            };

            if (Roots.TryGetValue(root, out var existingRoot))
            {
                return existingRoot;
            }

            //root.Roots.Add(root);

            Roots.Add(root);
            Nodes.Add(root);

            return root;
        }

        public NamedTypeSymbolTreeNode FindNode(NamedTypeCashModel symbol)
        {
            var node = new NamedTypeSymbolTreeNode { Symbol = symbol };

            return Nodes.TryGetValue(node, out var outNode) ? outNode : null;
        }

        public void RemoveNode(NamedTypeCashModel node)
        {
            var treeNode = FindNode(node);

            foreach (var parent in treeNode.ParentList)
            {
                parent.ContainingTypeNode.ChildrenList.Remove(treeNode)
            }
        }
    }
}
