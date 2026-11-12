using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using DomainAnalyze.DependencyTree.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.CashModels
{
    public class NamedTypeCashModel
    {
        private INamedTypeSymbol _innerSymbol;
        public int Generation { get; private set; }
        public string ProjectName {get;set;}
        public string NamespaceFullName { get; private set; }
        public string Name { get; private set; }

        public HashSet<MethodCashModel> MethodList { get; set; } = [];
        public HashSet<DocumentCashModel> DocumentModelSet { get; set; } = [];
        public CompilationCashModel CompilationModel { get; set; }

        public void UpGeneration()
        {
            Generation++;
        }

        [JsonIgnore]
        public INamedTypeSymbol InnerSymbol
        {
            get
            {
                return _innerSymbol;
            }
            set
            {
                _innerSymbol = value;
                NamespaceFullName = _innerSymbol.ContainingNamespace.NamespaceFullName();
                Name = _innerSymbol.Name;
            }
        }

        public Project Project => CompilationModel.Project;

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not NamedTypeCashModel other)
                return false;

            return NamedTypeEqualityComparer.Instance.Equals(this.InnerSymbol, other.InnerSymbol);
        }

        public override int GetHashCode()
        {
            return NamedTypeEqualityComparer.Instance.GetHashCode(this.InnerSymbol);
        }
    }
}
