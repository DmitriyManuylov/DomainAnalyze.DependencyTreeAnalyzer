using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Models.SymbolTreeModels;
using DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using DomainAnalyze.DependencyTree.Utilities.Extensions;

namespace DomainAnalyze.DependencyTree.Services
{
    public abstract class DependencyTreeAnalyzer
    {
        private Stack<INamedTypeSymbol> DependencyStack = new Stack<INamedTypeSymbol>();
        protected MSBuildWorkspace Workspace { get; private init; }
        protected Solution Solution { get; private init; }
        protected HashSet<INamedTypeSymbol> SolutionClasses = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
        protected HashSet<INamedTypeSymbol> DIImplementations = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
        protected NamedTypeSymbolTree SymbolTree { get; private init; }

        protected Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> InterfacesImplementations = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(NamedTypeEqualityComparer.Instance);
        private List<IDependencyRegistrationsSearcher> DependencyRegistrationSearchersList = new List<IDependencyRegistrationsSearcher>();

        public DependencyTreeAnalyzer(string solutionPath)
        {
            Workspace = MSBuildWorkspace.Create();

            Solution = Task.Run(() => Workspace.OpenSolutionAsync(solutionPath)).GetAwaiter().GetResult();

            SolutionClasses = new();

            SymbolTree = new NamedTypeSymbolTree();
        }

        public async Task Analyze()
        {
            List<INamedTypeSymbol> classes = new();
            foreach (var project in Solution.Projects)
            {
                classes.AddRange(await project.GetProjectTypes());
            }

            foreach(var item in classes)
                this.SolutionClasses.Add(item);

            var binds = await GetNinjectRegistrations();
            foreach (var item in binds.Where(item => item.Implementation is not null))
                DIImplementations.Add(item.Implementation);

            var list = GetNinjectInjections();

            var distinctedDependencies = list
                .Select(item => item.Type as INamedTypeSymbol)
                .Distinct(NamedTypeEqualityComparer.Instance)
                .Select(item => item as INamedTypeSymbol)
                .ToList();

            var orderedList = list.OrderBy(item => item.Type.Name);

            List<(IFieldSymbol, DependencyRegistrationModel)> mapping = list
                .Select(item => (item, 
                    binds.FirstOrDefault(bind => 
                    NamedTypeEqualityComparer.Instance.Equals(bind.Interface, item.Type as INamedTypeSymbol))))
                .ToList();

            await InnerAnalyze();

        }

        protected abstract Task InnerAnalyze();

        private List<IFieldSymbol> BuildDependenciesList(INamedTypeSymbol type)
        {
            DependencyStack.Push(type);
            if (type is null)
            {
                return new List<IFieldSymbol>();
            }

            var currentTreeNode = SymbolTree.FindNode(type);

            var result = new List<IFieldSymbol>();
            currentTreeNode = SymbolTree.FindNode(type);
            var deps = FindTypeDependencies(type);

            result.AddRange(deps);

            foreach (var dep in deps)
            {
                List<INamedTypeSymbol> implementations = new List<INamedTypeSymbol>();

                if (dep.Type.TypeKind == TypeKind.Interface)
                {
                    implementations.AddRange(FindInterfaceImplementation(dep.Type as INamedTypeSymbol));
                }
                else
                {
                    implementations.Add(dep.Type as INamedTypeSymbol);
                }

                if (implementations is null || !implementations.Any())
                    continue;

                foreach (var implement in implementations)
                {
                    currentTreeNode.AddChildrenNode(dep, implement);

                    var dependencyNode = SymbolTree.FindNode(implement);
                    if (dependencyNode is not null)
                        continue;

                    if (DependencyStack.Contains(implement, NamedTypeEqualityComparer.Instance))
                        continue;

                    var innerDeps = BuildDependenciesList(implement);

                    result.AddRange(innerDeps);
                }
            }

            DependencyStack.Pop();

            return result;
        }

        private List<IFieldSymbol> GetNinjectInjections()
        {
            var types = GetControllers();

            types.ForEach(type =>
            {
                SymbolTree.AddRoot(type);
            });

            List<IFieldSymbol> injectedDeps = new();
            List<IFieldSymbol> controllersDeps = new();

            foreach (var type in types)
            {
                List<IFieldSymbol> allDeps = BuildDependenciesList(type);

                controllersDeps.AddRange(allDeps);
            }
            injectedDeps.AddRange(controllersDeps);

            injectedDeps = injectedDeps.Distinct(FieldSymbolEqualityComparer.Instance).ToList();

            return injectedDeps;
        }

        private List<IFieldSymbol> FindTypeDependencies(INamedTypeSymbol type)
        {
            var typeFields = type.GetMembers().OfType<IFieldSymbol>();
            var fieldsInjectedByAttributes = typeFields
                .Where(
                    item =>
                    item.AssociatedSymbol?
                    .GetAttributes()
                    .Any(item => item.AttributeClass?.Name?.Contains("Inject") == true) == true) ?? [];

            var constructor = type.Constructors.FirstOrDefault(item => item.Parameters.Count() > 0);

            IEnumerable<AssignmentExpressionSyntax> injectingAssignmentsInConstructor = new List<AssignmentExpressionSyntax>();
            if (constructor is not null)
            {
                var constructorParams = constructor.Parameters;

                var constructorDeclSyntax = constructor?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ConstructorDeclarationSyntax;

                var constrBody = constructorDeclSyntax?.Body;

                injectingAssignmentsInConstructor = constrBody?
                    .DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(item => constructorParams.Any(param =>
                        {
                            return
                        param.Name == (item.Right as IdentifierNameSyntax)?.Identifier.Text
                                &&
                            typeFields.Any(field => field.Name == GetFieldNameByAssignment(item));
                        })) ?? [];
            }

            var fieldsInjectedByConstructor = injectingAssignmentsInConstructor.Select(assign =>
                typeFields.FirstOrDefault(field => field.Name == GetFieldNameByAssignment(assign))
            );

            var allDeps = fieldsInjectedByAttributes
                .Union(fieldsInjectedByConstructor, FieldSymbolEqualityComparer.Instance).ToList();
            return allDeps;
        }

        private string GetFieldNameByAssignment(AssignmentExpressionSyntax assignmentExpression)
        {
            string result = default;
            var left = assignmentExpression.Left;

            switch (left.Kind())
            {
                case Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleMemberAccessExpression:
                    result = ((left as MemberAccessExpressionSyntax).Name as IdentifierNameSyntax).Identifier.Text;
                    break;
                case Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierName:
                    result = (left as IdentifierNameSyntax).Identifier.Text;
                    break;
            }

            return result;
        }

        private List<INamedTypeSymbol> FindInterfaceImplementation(INamedTypeSymbol interfaceType)
        {
            List<INamedTypeSymbol> result = new();

            if (interfaceType.TypeKind != TypeKind.Interface)
            {
                throw new InvalidOperationException();
            }

            result = SolutionClasses.Where(item => item.Interfaces.Any(item => NamedTypeEqualityComparer.Instance.Equals(item, interfaceType))).ToList();

            InterfacesImplementations.TryAdd(interfaceType, result);

            return result;
        }

        private List<INamedTypeSymbol> GetControllers()
        {
            var controllers = SolutionClasses.Where(item =>
                item.Name.Contains("Controller") && item.BaseType.Name.Contains("Controller"));

            return controllers.ToList();
        }

        private async Task<List<DependencyRegistrationModel>> GetNinjectRegistrations()
        {
            List<DependencyRegistrationModel> dependencyRegistrations = new();

            foreach (var type in SolutionClasses)
            {
                foreach(var searcher in DependencyRegistrationSearchersList)
                {
                    if (!searcher.CheckType(type))
                        continue;

                    dependencyRegistrations.AddRange(await searcher.SearchRegistrations(type));
                }
            }

            return dependencyRegistrations;
        }

        public void SetDependencyRegistrationsSearcher<TType>() where TType : class, IDependencyRegistrationsSearcher, new()
        {
            var instance = new TType()
            {
                Solution = Solution,
                SolutionTypes = SolutionClasses
            };

            DependencyRegistrationSearchersList.Add(instance);
        }
    }
}
