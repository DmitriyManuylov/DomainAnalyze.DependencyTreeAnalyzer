using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.CashModels
{
    public class MethodCashModel
    {
        public int Generation { get; private set; }
        private IMethodSymbol _innerSymbol;

        public string MethodName { get; private set; }
        public IMethodSymbol InnerSymbol
        {
            get { return _innerSymbol; }
            set
            {
                _innerSymbol = value;
                MethodName = _innerSymbol.Name;
            }
        }

        /// <summary>
        /// Вызовы метода из другого типа
        /// </summary>
        public Dictionary<IFieldSymbol, List<IInvocationOperation>> MethodOuterInvocations { get; private set; }
            = new Dictionary<IFieldSymbol, List<IInvocationOperation>>(FieldSymbolEqualityComparer.Instance);

        /// <summary>
        /// Внутренние вызовы в других методах типа
        /// </summary>
        public List<IInvocationOperation> OwnTypeMethodInvocations { get; set; } = [];

        public NamedTypeCashModel ContainingType { get; set; }

        public void UpGeneration()
        {
            Generation++;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not MethodCashModel other)
                return false;

            return MethodSymbolEqualityComparer.Instance.Equals(this.InnerSymbol, other.InnerSymbol);
        }

        public override int GetHashCode()
        {
            return MethodSymbolEqualityComparer.Instance.GetHashCode(this.InnerSymbol);
        }
    }
}
