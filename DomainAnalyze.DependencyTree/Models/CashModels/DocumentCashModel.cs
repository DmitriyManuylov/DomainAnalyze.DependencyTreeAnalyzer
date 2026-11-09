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
        private string filePath;
        private SyntaxTree syntaxTree;
        private SemanticModel semanticModel;
        private Document document;
        public int Generation { get; private set; }
        public string FilePath
        {
            get { return filePath; }
            set { filePath = value; }
        }
        public HashSet<NamedTypeCashModel> NamedTypeList { get; set; } = [];

        public CompilationCashModel CompilationModel { get; set; }

        [JsonIgnore]
        public SyntaxTree SyntaxTree
        {
            get { return syntaxTree; }
            set
            {
                SyntaxTree = value;
                FilePath = value.FilePath;
            }
        }

        [JsonIgnore]
        public SemanticModel SemanticModel
        {
            get { return semanticModel; }
            set
            {
                semanticModel = value;
                SyntaxTree = semanticModel.SyntaxTree;
            }
        }

        [JsonIgnore]
        public Document Document
        {
            get { return document; }
            set { document = value; }
        }

        public void UpGeneration()
        {
            Generation++;
        }

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
