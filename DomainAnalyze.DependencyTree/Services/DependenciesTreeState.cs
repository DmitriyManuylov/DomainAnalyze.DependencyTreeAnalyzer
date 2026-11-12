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
        private HashSet<DocumentCashModel> DocumentCashPlainList { get; set; } = [];
        private HashSet<NamedTypeCashModel> NamedTypesPlainList { get; set; } = [];
        private HashSet<MethodCashModel> MethodsPlainList { get; set; } = [];

        public ImmutableArray<CompilationCashModel> CompilationModelsEnumerable => CompilationCashModelList.ToImmutableArray();
        public ImmutableArray<DocumentCashModel> DocumentModelsEnumerable => DocumentCashPlainList.ToImmutableArray();
        public ImmutableArray<NamedTypeCashModel> NamedTypeEnumerable => NamedTypesPlainList.ToImmutableArray();
        public ImmutableArray<MethodCashModel> MethodModelsEnumerable => MethodsPlainList.ToImmutableArray();

        public CompilationCashModel this[Project project]
        {
            get => CompilationCashModelList.TryGetValue(new CompilationCashModel
            {
                Project = project
            }, out var value) ? value : null;
        }

        public DocumentCashModel this[SyntaxTree syntaxTree]
        {
            get => DocumentCashPlainList.TryGetValue(new DocumentCashModel
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

        public MethodCashModel AddOrGetMethod(Project project, SemanticModel semanticModel, Document document, IMethodSymbol methodSymbol, bool rewrite = false)
        {
            var methodModel = this[methodSymbol];

            if (methodModel is not null && !rewrite)
            {
                return methodModel;
            }

            var type = AddOrGetNamedType(project, semanticModel, document, methodSymbol.ContainingType, rewrite);

            methodModel ??= new MethodCashModel();
            methodModel.InnerSymbol = methodSymbol;
            methodModel.ContainingType = type;

            MethodsPlainList.Add(methodModel);
            type.MethodList.Add(methodModel);

            return methodModel;
        }

        public NamedTypeCashModel AddOrGetNamedType(Project project, SemanticModel semanticModel, Document document, INamedTypeSymbol namedTypeSymbol, bool rewrite = false)
        {
            var typeModel = this[namedTypeSymbol];

            var documentModel = AddOrGetDocumentModel(project, semanticModel, document);

            if (typeModel is not null && !rewrite)
            {
                typeModel.DocumentModelSet.Add(documentModel);
                return typeModel;
            }

            typeModel ??= new NamedTypeCashModel();
            typeModel.InnerSymbol = namedTypeSymbol;
            typeModel.CompilationModel = documentModel.CompilationModel;

            NamedTypesPlainList.Add(typeModel);
            typeModel.DocumentModelSet.Add(documentModel);
            documentModel.NamedTypeList.Add(typeModel);
            documentModel.CompilationModel.NamedTypesList.Add(typeModel);

            return typeModel;
        }

        private CompilationCashModel AddOrGetCompilation(Project project, SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            var compilation = this[project];

            if (compilation is not null && !rewrite)
            {
                return compilation;
            }

            compilation ??= new CompilationCashModel();
            compilation.Compilation = semanticModel.Compilation;
            compilation.Project = project;

            CompilationCashModelList.Add(compilation);

            return compilation;
        }

        private DocumentCashModel AddOrGetDocumentModel(Project project, SemanticModel semanticModel, Document document, bool rewrite = false)
        {
            var documentModel = this[semanticModel.SyntaxTree];

            if (documentModel is not null && !rewrite)
            {
                return documentModel;
            }

            var compilation = AddOrGetCompilation(project, semanticModel, document);

            documentModel ??= new DocumentCashModel();
            documentModel.SemanticModel = semanticModel;
            documentModel.CompilationModel = compilation;
            documentModel.Document = document;

            DocumentCashPlainList.Add(documentModel);
            compilation.DocumentsList.Add(documentModel);

            return documentModel;
        }
    }
}
