using DomainAnalyze.DependencyTree.Models.CashModels;
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
        public NamedTypeCashModel Interface {  get; set; }
        public NamedTypeCashModel Implementation { get; set; }
        public bool IsSelfImplemented {  get; set; }
        public SyntaxNode OperationSyntaxNode { get; set; }
    }
}
