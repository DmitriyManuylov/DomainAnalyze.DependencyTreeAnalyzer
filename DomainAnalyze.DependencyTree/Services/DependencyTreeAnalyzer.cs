using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Models.SymbolTreeModels;
using DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices;
using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using DomainAnalyze.DependencyTree.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DomainAnalyze.DependencyTree.Services
{
    public abstract class DependencyTreeAnalyzer
    {
        private MefHostServices HostServices;
        protected Workspace Workspace { get; private set; }
        protected Solution Solution { get; private set; }

        public TimeStampModel TimeStampModel { get; private set; }

        public DependencyAnalyzerOptions AnalyzerOptions { get; set; }
        private FileSystemWatcher FileSystemWatcher;
        private Stopwatch Stopwatch = new Stopwatch();
        private Stack<INamedTypeSymbol> DependencyStack;

        protected string SolutionPath;
        protected HashSet<INamedTypeSymbol> SolutionClasses;
        protected HashSet<INamedTypeSymbol> DIImplementations;

        private HashSet<INamedTypeSymbol> ProcessedInjections;
        protected NamedTypeSymbolTree SymbolTree { get; private set; }
        protected Dictionary<IPropertySymbol, IFieldSymbol> PropertiesToFieldsMapping;
        protected Dictionary<IFieldSymbol, List<IInvocationOperation>> FieldsInvocations;
        protected Dictionary<SyntaxTree, SemanticModel> SyntaxTreeSemanticMapping;

        /// <summary>
        /// Вызовы собственных методов класса
        /// </summary>
        protected Dictionary<IMethodSymbol, List<IInvocationOperation>> OwnCallsMapping;

        protected Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> InterfacesImplementations;
        private List<IDependencyRegistrationsSearcher> DependencyRegistrationSearchersList;
        protected HashSet<INamedTypeSymbol> DIRoots;

        private NodesCash NodesCash;

        private object ChangedHadlerLocker = new object();
        private SolutionFilesWatcher SolutionFilesWatcher;

        private void Init()
        {
            AnalyzerOptions ??= new();
            TimeStampModel = new TimeStampModel();
            Stopwatch = new Stopwatch();
            DependencyStack = new Stack<INamedTypeSymbol>();
            SolutionClasses = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
            DIImplementations = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
            ProcessedInjections = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
            PropertiesToFieldsMapping = new Dictionary<IPropertySymbol, IFieldSymbol>(PropertySymbolEqualityComparer.Instance);
            FieldsInvocations = new Dictionary<IFieldSymbol, List<IInvocationOperation>>(FieldSymbolEqualityComparer.Instance);
            SyntaxTreeSemanticMapping = new Dictionary<SyntaxTree, SemanticModel>(SyntaxTreeEqualityComparer.Instance);
            OwnCallsMapping = new Dictionary<IMethodSymbol, List<IInvocationOperation>>(MethodSymbolEqualityComparer.Instance);
            InterfacesImplementations = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(NamedTypeEqualityComparer.Instance);
            DependencyRegistrationSearchersList = new List<IDependencyRegistrationsSearcher>();
            DIRoots = new HashSet<INamedTypeSymbol>(NamedTypeEqualityComparer.Instance);
            SymbolTree = new NamedTypeSymbolTree();
        }

        static DependencyTreeAnalyzer()
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }

        public DependencyTreeAnalyzer(string solutionPath, DependencyAnalyzerOptions analyzerOptions = null)
        {
            AnalyzerOptions = analyzerOptions;
            SolutionPath = solutionPath;
            Init();

            HostServices ??= MefHostServices.Create(MefHostServices.DefaultAssemblies);
            var msBuildWorkspace = MSBuildWorkspace.Create(HostServices);

            Stopwatch stopwatch = new();
            stopwatch.Start();
            var solution = Task.Run(() => msBuildWorkspace.OpenSolutionAsync(solutionPath)).GetAwaiter().GetResult();
            stopwatch.Stop();
            TimeStampModel.SolutionBuildTime = stopwatch.ElapsedMilliseconds;

            if (AnalyzerOptions.UseFileWatcher)
            {
                var reloadableWorkspace = new ReloadableWorkspace(HostServices);

                reloadableWorkspace.InitializeAsync(solution).GetAwaiter().GetResult();

                SolutionFilesWatcher = new SolutionFilesWatcher(reloadableWorkspace, NodesCash);
                SolutionFilesWatcher.AddFileWatcher();

                Workspace = reloadableWorkspace;
            }
            else
            {
                Workspace = msBuildWorkspace;
            }

            Solution = Workspace.CurrentSolution;

            Workspace.RegisterWorkspaceChangedHandler(WorkspaceChangeHandler);
        }

        public DependencyTreeAnalyzer(Solution solution, DependencyAnalyzerOptions analyzerOptions = null)
        {
            AnalyzerOptions = analyzerOptions;
            ReInit(solution);

            Workspace.RegisterWorkspaceChangedHandler(WorkspaceChangeHandler);
        }

        public void ReInit(Solution newSolution)
        {
            Workspace = newSolution.Workspace as ReloadableWorkspace;

            if (Workspace is null && AnalyzerOptions.UseFileWatcher)
            {
                var reloadableWorkspace = new ReloadableWorkspace(HostServices);

                reloadableWorkspace.InitializeAsync(newSolution).GetAwaiter().GetResult();

                SolutionFilesWatcher = new SolutionFilesWatcher(reloadableWorkspace, NodesCash);
                SolutionFilesWatcher.AddFileWatcher();

                Workspace = reloadableWorkspace;
            }
            else
            {
                Workspace = newSolution.Workspace;
            }
            
            Solution = newSolution;
            SolutionPath = Solution.FilePath;

            Init();

            Workspace.RegisterWorkspaceChangedHandler(WorkspaceChangeHandler);
        }

        public async Task ReAnalyze(Solution solution)
        {
            ReInit(solution);

            await Analyze();
        }

        public async Task Analyze()
        {
            await BuildSyntaxTreeSemanticModelMapping();

            List<INamedTypeSymbol> classes = new();
            foreach (var project in Solution.Projects)
            {
                classes.AddRange(await project.GetProjectTypes());
            }

            foreach(var item in classes)
                SolutionClasses.Add(item);

            Stopwatch stopwatch = new();
            stopwatch.Start();
            var binds = await GetNinjectRegistrations();
            foreach (var item in binds.Where(item => item.Implementation is not null))
                DIImplementations.Add(item.Implementation);
            stopwatch.Stop();
            TimeStampModel.DependenciesRegistrationSearchTime = stopwatch.ElapsedMilliseconds;

            stopwatch = new Stopwatch();
            stopwatch.Start();
            var list = GetNinjectInjections();
            stopwatch.Stop();
            TimeStampModel.DependenciesSearchTime = stopwatch.ElapsedMilliseconds;

            foreach (var fieldNode in SymbolTree.Fields)
            {
                var prop = fieldNode.FieldSymbol.GetAssociatedProperty();

                if (prop is null)
                    continue;

                PropertiesToFieldsMapping.TryAdd(prop, fieldNode.FieldSymbol);
            }

            stopwatch = new Stopwatch();
            stopwatch.Start();
            await SearchInvocations();
            await SearchOwnInvocationsAsync();
            stopwatch.Stop();
            TimeStampModel.InvocationsModelBuildTime = stopwatch.ElapsedMilliseconds;

            stopwatch = new Stopwatch();
            stopwatch.Start();
            await InnerAnalyze();
            stopwatch.Stop();
            TimeStampModel.AnalyzeTime = stopwatch.ElapsedMilliseconds;
        }

        protected abstract Task InnerAnalyze();

        protected virtual void WorkspaceChangeHandler(WorkspaceChangeEventArgs args)
        {
            lock (ChangedHadlerLocker)
            {
                //ReAnalyze(args.NewSolution).GetAwaiter().GetResult();

                foreach (var projectChange in args.NewSolution.GetChanges(args.OldSolution).GetProjectChanges())
                {
                    foreach (var doc in projectChange.GetChangedDocuments())
                    {
                        var document = projectChange.NewProject.GetDocument(doc);
                    }
                }
            }
        }

        private List<IFieldSymbol> BuildDependenciesList(INamedTypeSymbol type)
        {
            if (ProcessedInjections.Contains(type))
                return new List<IFieldSymbol>();

            if (type is null)
                return new List<IFieldSymbol>();

            DependencyStack.Push(type);

            var currentTreeNode = SymbolTree.FindNode(type);

            var result = new List<IFieldSymbol>();

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

                    if (DependencyStack.Contains(implement, NamedTypeEqualityComparer.Instance))
                        continue;

                    var innerDeps = BuildDependenciesList(implement);

                    result.AddRange(innerDeps);
                }
            }

            DependencyStack.Pop();
            ProcessedInjections.Add(type);
            return result;
        }

        private List<IFieldSymbol> GetNinjectInjections()
        {
            var types = GetControllers();

            foreach(var type in types)
                DIRoots.Add(type);

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

        private async Task SearchInvocations()
        {
            foreach (var field in SymbolTree.Fields)
            {
                if (field.ContainingTypeNode.Symbol.TypeKind == TypeKind.Interface)
                    continue;

                var methods = field
                    .ContainingTypeNode
                    .Symbol
                    .GetMembers()
                    .OfType<IMethodSymbol>();

                foreach (var method in methods.Where(item => !item.IsImplicitlyDeclared))
                {
                    if (!method.DeclaringSyntaxReferences.Any())
                        continue;

                    SyntaxTreeSemanticMapping.TryGetValue(method.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree, out var semanticModel);
                    var methodBody = method.GetMethodBodyOperation(semanticModel);

                    var propertiesRefInvocations = methodBody
                    .Descendants()
                    .OfType<IPropertyReferenceOperation>()
                    .Where(item =>
                    {
                        if (item.Property.Type is not INamedTypeSymbol propType)
                            return false;

                        if (!InterfacesImplementations.ContainsKey(propType))
                            return false;

                        if (!PropertiesToFieldsMapping.TryGetValue(item.Property, out var outField) || (outField is null))
                            return false;

                        return FieldSymbolEqualityComparer.Instance.Equals(outField, field.FieldSymbol);
                    })
                    .Select(item => item.Parent as IInvocationOperation)
                    .Where(item => item is not null).ToList();

                    foreach (var inv in propertiesRefInvocations)
                    {
                        if (FieldsInvocations.TryGetValue(field.FieldSymbol, out var invocations))
                        {
                            invocations.Add(inv);
                            continue;
                        }

                        invocations = new List<IInvocationOperation>();
                        invocations.Add(inv);
                        FieldsInvocations.TryAdd(field.FieldSymbol, invocations);
                    }

                    var fieldsRefInvocations = methodBody
                        .Descendants()
                        .OfType<IFieldReferenceOperation>()
                        .Where(item => FieldSymbolEqualityComparer.Instance.Equals(item.Field, field.FieldSymbol))
                        .Select(item => item.Parent as IInvocationOperation)
                        .Where(item => item is not null).ToList();

                    foreach (var inv in fieldsRefInvocations)
                    {
                        if (FieldsInvocations.TryGetValue(field.FieldSymbol, out var invocations))
                        {
                            invocations.Add(inv);
                            continue;
                        }

                        invocations = new List<IInvocationOperation>();
                        invocations.Add(inv);
                        FieldsInvocations.TryAdd(field.FieldSymbol, invocations);
                    }
                }
            }
        }

        private async Task SearchOwnInvocationsAsync()
        {
            foreach(var root in DIRoots)
                await TypeOwnMethodInvocations(root);

            foreach (var dep in DIImplementations)
            {
                if (dep.TypeKind == TypeKind.Interface)
                    continue;

                await TypeOwnMethodInvocations(dep);
            }
        }

        private async Task TypeOwnMethodInvocations(INamedTypeSymbol dep)
        {
            var methods = dep
                .GetMembers()
                .OfType<IMethodSymbol>();

            foreach (var method in methods.Where(item => !item.IsImplicitlyDeclared))
            {
                if (!method.DeclaringSyntaxReferences.Any())
                    continue;

                SyntaxTreeSemanticMapping.TryGetValue(method.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree, out var semanticModel);

                var allInvocations = method.GetMethodBodyOperation(semanticModel)
                    .Descendants()
                    .OfType<IInvocationOperation>()
                    .Where(invocation => NamedTypeEqualityComparer.Instance.Equals(dep, invocation.TargetMethod.ContainingType));

                foreach (var invocation in allInvocations)
                {
                    if (OwnCallsMapping.TryGetValue(invocation.TargetMethod, out var invocations))
                    {
                        invocations.AddRange(allInvocations);
                        continue;
                    }

                    invocations = new List<IInvocationOperation>();
                    invocations.Add(invocation);
                    OwnCallsMapping.TryAdd(invocation.TargetMethod, invocations);
                }
            }
        }

        private async Task BuildSyntaxTreeSemanticModelMapping()
        {
            foreach (var project in Solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                foreach (var doc in project.Documents)
                {
                    var syntaxTree = await doc.GetSyntaxTreeAsync();
                    SyntaxTreeSemanticMapping.TryAdd(syntaxTree, compilation.GetSemanticModel(syntaxTree));
                }
            }
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
