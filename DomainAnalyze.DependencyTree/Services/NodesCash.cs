using DomainAnalyze.DependencyTree.Models.CashModels;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Services
{
    internal class NodesCash
    {
        private HashSet<CompilationCashModel> CompilationCashModelList { get; set; } = [];
        private HashSet<DocumentCashModel> SyntaxTriesPlainList { get; set; } = [];
        private HashSet<NamedTypeCashModel> NamedTypesPlainList { get; set; } = [];
        private HashSet<MethodCashModel> MethodsPlainList { get; set; } = [];

        public DocumentCashModel GetDocument(string filePath)
        {
            SyntaxTriesPlainList.TryGetValue(new DocumentCashModel
            {
                FilePath = filePath,
            }, out var actualValue);

            return actualValue;
        }

        public MethodCashModel AddOrGetMethod(SemanticModel semanticModel, Document document, IMethodSymbol methodSymbol, bool rewrite = false)
        {
            var type = AddOrGetNamedType(semanticModel, document, methodSymbol.ContainingType);

            var methodModel = new MethodCashModel
            {
                InnerSymbol = methodSymbol,
                ContainingType = type,
            };

            if (type.MethodList.TryGetValue(methodModel, out var existedMethodModel))
            {
                methodModel = existedMethodModel;

                if (rewrite)
                {
                    methodModel.InnerSymbol = methodSymbol;
                }
            }
            else
            {
                type.MethodList.Add(methodModel);
            }

            MethodsPlainList.Add(methodModel);

            return methodModel;
        }

        public NamedTypeCashModel AddOrGetNamedType(SemanticModel semanticModel, Document document, INamedTypeSymbol namedTypeSymbol, bool rewrite = false)
        {
            var compilation = AddOrGetCompilation(semanticModel, document);
            var syntaxTree = AddOrGetSyntaxTree(semanticModel, document);

            var namedTypeCashModel = new NamedTypeCashModel
            {
                InnerSymbol = namedTypeSymbol,
                CompilationModel = compilation,
            };

            if (NamedTypesPlainList.TryGetValue(namedTypeCashModel, out var existed))
            {
                namedTypeCashModel = existed;

                if (rewrite)
                {
                    namedTypeCashModel.InnerSymbol = namedTypeSymbol;
                }
            }
            else
            {
                NamedTypesPlainList.Add(namedTypeCashModel);
            }

            syntaxTree.NamedTypeList.Add(namedTypeCashModel);

            compilation.NamedTypesList.Add(namedTypeCashModel);

            return namedTypeCashModel;
        }

        private CompilationCashModel AddOrGetCompilation(SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            CompilationCashModel compilation = new()
            {
                Compilation = semanticModel.Compilation
            };

            if (CompilationCashModelList.TryGetValue(compilation, out var existedCompilation))
            {
                if (!rewrite)
                    compilation = existedCompilation;
            }
            else
            {
                CompilationCashModelList.Add(compilation);
            }

            return compilation;
        }

        private DocumentCashModel AddOrGetSyntaxTree(SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            var compilation = AddOrGetCompilation(semanticModel, document);

            var syntaxTreeModel = new DocumentCashModel()
            {
                SemanticModel = semanticModel,
                CompilationModel = compilation,
                Document = document,
            };

            if (this.SyntaxTriesPlainList.TryGetValue(syntaxTreeModel, out var existedSyntaxTree))
            {
                if (!rewrite)
                    syntaxTreeModel = existedSyntaxTree;
            }
            else
            {
                SyntaxTriesPlainList.Add(syntaxTreeModel);
            }

            compilation.DocumentsList.Add(syntaxTreeModel);

            return syntaxTreeModel;
        }
    }
}
