using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Utilities.Extensions;

namespace DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices
{
    public class DefaultNinjectRegistrationsSearcher : IDependencyRegistrationsSearcher
    {
        public Solution Solution { get; init; }
        public HashSet<INamedTypeSymbol> SolutionTypes { get; init; }

        public bool CheckType(INamedTypeSymbol type)
        {
            if(type.BaseType.Name != "NinjectModule" || type.BaseType.ContainingNamespace.NamespaceFullName() != "Ninject.Modules")
                return false;

            return true;
        }

        public async Task<List<DependencyRegistrationModel>> SearchRegistrations(INamedTypeSymbol type)
        {
            List<DependencyRegistrationModel> dependencyRegistrations = new();
            var project = Solution.Projects.FirstOrDefault(item => item.AssemblyName == type.ContainingAssembly.Name);

            var compilation = await project.GetCompilationAsync();

            var syntaxNode = await type.DeclaringSyntaxReferences.First().GetSyntaxAsync();

            var semanticModel = compilation.GetSemanticModel(syntaxNode.SyntaxTree);

            var bindsSyntax = syntaxNode.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(item =>
                {
                    var symbol = semanticModel.GetSymbolInfo(item).Symbol;

                    return symbol.Name == "Bind" && symbol.ContainingType.Name == "BindingRoot" && symbol.ContainingType.ContainingNamespace.Name == "Syntax";
                });

            foreach (var bind in bindsSyntax)
            {
                var op = semanticModel.GetOperation(bind) as IInvocationOperation;
                var nextOp = op.Parent as IInvocationOperation;

                INamedTypeSymbol bindTypeSymbol = default;
                INamedTypeSymbol toTypeSymbol = default;
                bool isSelfImplemented = false;

                bindTypeSymbol = GetBindingType(op);

                if (bindTypeSymbol is null)
                    continue;

                switch (nextOp.TargetMethod.Name)
                {
                    case "To":
                        toTypeSymbol = GetBindingType(nextOp);
                        break;

                    case "ToSelf":
                        isSelfImplemented = true;

                        toTypeSymbol = bindTypeSymbol;
                        break;

                    case "ToMethod":
                        toTypeSymbol = (
                            (nextOp.Arguments.FirstOrDefault().Value as IDelegateCreationOperation)
                            .Target.ChildOperations.FirstOrDefault()
                            .ChildOperations
                            .FirstOrDefault(item => item.Kind is OperationKind.Return) as IReturnOperation)
                            .ReturnedValue.Type as INamedTypeSymbol;
                        break;

                    case "ToConstructor":
                        toTypeSymbol = (
                            nextOp.Arguments.FirstOrDefault().Value
                            .ChildOperations.FirstOrDefault()
                            .ChildOperations.FirstOrDefault()
                            .ChildOperations.FirstOrDefault() as IReturnOperation)
                            .ReturnedValue.Type as INamedTypeSymbol;
                        break;
                }

                dependencyRegistrations.Add(new DependencyRegistrationModel
                {
                    Interface = bindTypeSymbol,
                    Implementation = toTypeSymbol,
                    IsSelfImplemented = isSelfImplemented,
                    OperationSyntaxNode = op.Syntax
                });
            }

            return dependencyRegistrations;
        }

        private INamedTypeSymbol GetBindingType(IInvocationOperation op)
        {
            INamedTypeSymbol bindTypeSymbol = op.TargetMethod.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            if (bindTypeSymbol is null)
            {
                var opArg = op.Arguments.FirstOrDefault();
                if (opArg is null)
                    return null;

                switch (opArg.ChildOperations.FirstOrDefault().Kind)
                {
                    case OperationKind.ArrayCreation:
                        bindTypeSymbol = ((opArg.ChildOperations.FirstOrDefault() as IArrayCreationOperation).ChildOperations.ToList()[1].ChildOperations.FirstOrDefault() as ITypeOfOperation).TypeOperand as INamedTypeSymbol;
                        break;
                    case OperationKind.TypeOf:
                        bindTypeSymbol = (opArg.ChildOperations.FirstOrDefault() as ITypeOfOperation).TypeOperand as INamedTypeSymbol;
                        break;
                }
            }

            return bindTypeSymbol;
        }
    }
}
