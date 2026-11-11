using DomainAnalyze.DependencyTree.Models.CashModels;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Services
{
    public class DependenciesTreeState
    {
        private int Generation;
        private HashSet<CompilationCashModel> CompilationCashModelList { get; set; } = [];
        private HashSet<DocumentCashModel> SyntaxTriesPlainList { get; set; } = [];
        private HashSet<NamedTypeCashModel> NamedTypesPlainList { get; set; } = [];
        private HashSet<MethodCashModel> MethodsPlainList { get; set; } = [];

        public ImmutableArray<NamedTypeCashModel> NamedTypeEnumerable => NamedTypesPlainList.ToImmutableArray();

        public NamedTypeCashModel GetNamedType(INamedTypeSymbol namedTypeSymbol)
        {
            return NamedTypesPlainList.TryGetValue(new NamedTypeCashModel
            {
                InnerSymbol = namedTypeSymbol,
            }, out var namedTypeModel) ? namedTypeModel : null;
        }

        public CompilationCashModel GetCompilation(Project project)
        {
            CompilationCashModelList.TryGetValue(new CompilationCashModel
            {
                Project = project
            }, out var value);

            return value;
        }

        public CompilationCashModel this[Project project]
        {
            get => CompilationCashModelList.TryGetValue(new CompilationCashModel
            {
                Project = project
            }, out var value) ? value : null;
        }

        public DocumentCashModel this[SyntaxTree syntaxTree]
        {
            get => SyntaxTriesPlainList.TryGetValue(new DocumentCashModel
            {
                SyntaxTree = syntaxTree
            }, out var value) ? value : null;
        }

        public NamedTypeCashModel this[INamedTypeSymbol namedTypeSymbol]
        {
            get => NamedTypesPlainList.TryGetValue(new NamedTypeCashModel
            {
                InnerSymbol = namedTypeSymbol
            }, out var value) ? value: null;
        }

        public MethodCashModel this[IMethodSymbol methodSymbol]
        {
            get => MethodsPlainList.TryGetValue(new MethodCashModel
            {
                InnerSymbol = methodSymbol
            }, out var value) ? value : null;
        }

        public DocumentCashModel GetDocument(string filePath)
        {
            SyntaxTriesPlainList.TryGetValue(new DocumentCashModel
            {
                FilePath = filePath,
            }, out var actualValue);

            return actualValue;
        }

        public MethodCashModel AddOrGetMethod(Project project, SemanticModel semanticModel, Document document, IMethodSymbol methodSymbol, bool rewrite = false)
        {
            var type = AddOrGetNamedType(project, semanticModel, document, methodSymbol.ContainingType, rewrite);

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

        public NamedTypeCashModel AddOrGetNamedType(Project project, SemanticModel semanticModel, Document document, INamedTypeSymbol namedTypeSymbol, bool rewrite = false)
        {
            var documentModel = AddOrGetDocumentModel(project, semanticModel, document);

            var namedTypeCashModel = new NamedTypeCashModel
            {
                InnerSymbol = namedTypeSymbol,
                CompilationModel = documentModel.CompilationModel,
                DocumentModel = documentModel
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

            documentModel.NamedTypeList.Add(namedTypeCashModel);

            documentModel.CompilationModel.NamedTypesList.Add(namedTypeCashModel);

            return namedTypeCashModel;
        }

        private CompilationCashModel AddOrGetCompilation(Project project, SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            CompilationCashModel compilation = new()
            {
                Compilation = semanticModel.Compilation,
                Project = project,
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

        private DocumentCashModel AddOrGetDocumentModel(Project project, SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            var compilation = AddOrGetCompilation(project, semanticModel, document);

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
