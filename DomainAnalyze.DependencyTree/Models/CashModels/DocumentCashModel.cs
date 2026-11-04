using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.CashModels
{
    internal class DocumentCashModel
    {
        public int Generation { get; private set; }
        public string FilePath => SyntaxTree.FilePath;
        public HashSet<NamedTypeCashModel> NamedTypeList { get; set; } = [];

        public CompilationCashModel CompilationModel { get; set; }

        [JsonIgnore]
        public required SemanticModel SemanticModel { get; set; }
        [JsonIgnore]
        public SyntaxTree SyntaxTree => SemanticModel.SyntaxTree;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj is not DocumentCashModel compilationCashModel)
                return false;

            return this.FilePath.Equals(compilationCashModel.FilePath);
        }

        public override int GetHashCode()
        {
            return SyntaxTree?.FilePath.GetHashCode()
                ?? throw new Exception("Не задано синтаксическое дерево");
        }
    }
}
