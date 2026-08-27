using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.EqualityComparers
{
    public class SyntaxTreeEqualityComparer : IEqualityComparer<SyntaxTree>
    {
        private static readonly SyntaxTreeEqualityComparer instance = new SyntaxTreeEqualityComparer();
        public static SyntaxTreeEqualityComparer Instance => instance;
        public bool Equals(SyntaxTree x, SyntaxTree y)
        {
            return x.FilePath == y.FilePath;
        }

        public int GetHashCode([DisallowNull] SyntaxTree obj)
        {
            return obj.FilePath.GetHashCode();
        }
    }
}
