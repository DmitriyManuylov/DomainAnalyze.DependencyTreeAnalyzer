using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models
{
    public class DependencyRegistrationModel
    {
        public INamedTypeSymbol Interface {  get; set; }
        public INamedTypeSymbol Implementation { get; set; }
        public bool IsSelfImplemented {  get; set; }
        public SyntaxNode OperationSyntaxNode { get; set; }
    }
}
