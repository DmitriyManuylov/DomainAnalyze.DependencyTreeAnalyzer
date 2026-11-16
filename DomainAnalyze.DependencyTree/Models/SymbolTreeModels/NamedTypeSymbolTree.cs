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
        public HashSet<NamedTypeSymbolTreeNode> Roots { get; private set; } = [];
        public HashSet<NamedTypeSymbolTreeNode> Nodes { get; private set; } = [];
        public HashSet<FieldSymbolSubnode> Fields { get; private set; } = [];
        public NamedTypeSymbolTreeNode AddRoot(NamedTypeCashModel rootSymbol)
        {
            var root = new NamedTypeSymbolTreeNode()
            {
                Symbol = rootSymbol,
                ChildrenList = [],
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

        public FieldSymbolSubnode this[IFieldSymbol fieldSymbol]
        {
            get => Fields.TryGetValue(new FieldSymbolSubnode
            {
                FieldSymbol = fieldSymbol,
            }, out var actualValue) ? actualValue : null;
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
                var children = parent.ContainingTypeNode.ChildrenList.FirstOrDefault(item => item.FieldTypeNode.Symbol == node);

                parent.ContainingTypeNode.ChildrenList.Remove(children);
                Fields.Remove(children);
            }

            Nodes.RemoveWhere(item => item.Symbol == node);
        }
    }
}
