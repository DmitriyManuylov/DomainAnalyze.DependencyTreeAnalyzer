using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Models.CashModels;
using DomainAnalyze.DependencyTree.Models.SymbolTreeModels;
using DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices;
using DomainAnalyze.DependencyTree.Services.DIRootsProviders;
using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using DomainAnalyze.DependencyTree.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Operations;
using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;

namespace DomainAnalyze.DependencyTree.Services
{
    public abstract class DependencyTreeAnalyzer
    {
        private bool IsInitialAnanyzeDone = false;

        private static readonly MefHostServices HostServices = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        protected Workspace Workspace { get; private set; }
        protected Solution Solution => Workspace.CurrentSolution;

        public TimeStampModel TimeStampModel { get; private set; }

        public DependencyAnalyzerOptions AnalyzerOptions { get; set; }

        private Stack<NamedTypeCashModel> DependencyStack;

        protected string SolutionPath;
        protected HashSet<NamedTypeCashModel> SolutionClasses;
        protected HashSet<NamedTypeCashModel> DIImplementations;

        private HashSet<NamedTypeCashModel> ProcessedInjections;
        protected NamedTypeSymbolTree SymbolTree { get; private set; }
        protected Dictionary<IPropertySymbol, IFieldSymbol> PropertiesToFieldsMapping;

        protected Dictionary<SyntaxTree, (SemanticModel SemanticModel, Document Document)> SyntaxTreeSemanticMapping;

        protected Dictionary<NamedTypeCashModel, List<NamedTypeCashModel>> InterfacesImplementations;

        private List<IDIRootsProvider> DIRootsProvidersList;
        private List<IDependencyRegistrationsSearcher> DependencyRegistrationSearchersList;
        protected HashSet<NamedTypeCashModel> DIRoots;

        protected DependenciesTreeState DependenciesTreeState;

        private object ChangedHadlerLocker = new object();
        private SolutionFilesWatcher SolutionFilesWatcher;

        private void Init()
        {
            AnalyzerOptions ??= new();
            TimeStampModel = new TimeStampModel();
            DependencyStack = [];
            SolutionClasses = [];
            DIImplementations = [];
            ProcessedInjections = [];
            PropertiesToFieldsMapping = new Dictionary<IPropertySymbol, IFieldSymbol>(PropertySymbolEqualityComparer.Instance);
            SyntaxTreeSemanticMapping = new Dictionary<SyntaxTree, (SemanticModel SemanticModel, Document Document)>(SyntaxTreeEqualityComparer.Instance);
            InterfacesImplementations = new Dictionary<NamedTypeCashModel, List<NamedTypeCashModel>>();
            DIRootsProvidersList = [];
            DependencyRegistrationSearchersList = [];
            DIRoots = [];
            SymbolTree = new NamedTypeSymbolTree();
            DependenciesTreeState = new DependenciesTreeState();
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

                SolutionFilesWatcher = new SolutionFilesWatcher(reloadableWorkspace, DependenciesTreeState);
                SolutionFilesWatcher.AddFileWatcher();

                Workspace = reloadableWorkspace;
            }
            else
            {
                Workspace = msBuildWorkspace;
            }

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
            IsInitialAnanyzeDone = false;

            Workspace = newSolution.Workspace as ReloadableWorkspace;

            if (Workspace is null && AnalyzerOptions.UseFileWatcher)
            {
                var reloadableWorkspace = new ReloadableWorkspace(HostServices);

                reloadableWorkspace.InitializeAsync(newSolution).GetAwaiter().GetResult();

                SolutionFilesWatcher = new SolutionFilesWatcher(reloadableWorkspace, DependenciesTreeState);
                SolutionFilesWatcher.AddFileWatcher();

                Workspace = reloadableWorkspace;
            }
            else
            {
                Workspace = newSolution.Workspace;
            }
            
            SolutionPath = Solution.FilePath;

            Init();

            Workspace.RegisterWorkspaceChangedHandler(WorkspaceChangeHandler);
        }

        public async Task ReAnalyze(Solution solution = default)
        {
            ReInit(solution ?? Solution);

            await Analyze();
        }

        /// <summary>
        /// Обработчик события изменения рабочей области, после обновления дерева зависимостей.
        /// </summary>
        public virtual void WorkspaceChangeHandler()
        {

        }

        public async Task BuildDependencyTreeModel()
        {
            await BuildSyntaxTreeSemanticModelMapping();

            List<INamedTypeSymbol> classes = new();
            foreach (var project in Solution.Projects)
            {
                await GetProjectClasses(project);
            }

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
        }

        public async Task Analyze()
        {
            if (!IsInitialAnanyzeDone)
                await BuildDependencyTreeModel();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await InnerAnalyze();
            stopwatch.Stop();
            TimeStampModel.AnalyzeTime = stopwatch.ElapsedMilliseconds;
        }

        private async Task GetProjectClasses(Project project)
        {
            var compilation = await project.GetCompilationAsync();

            foreach (var doc in project.Documents)
            {
                var syntaxTree = await doc.GetSyntaxTreeAsync();

                var root = await syntaxTree!.GetRootAsync();

                var semanticModel = compilation!.GetSemanticModel(syntaxTree);

                var types = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>();

                var interfaces = root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>();

                var symbols = types.Select(item => semanticModel.GetDeclaredSymbol(item) as INamedTypeSymbol);

                var interfaceSymbols = interfaces.Select(item => semanticModel.GetDeclaredSymbol(item) as INamedTypeSymbol);

                foreach (var symbol in symbols)
                {
                    DependenciesTreeState.AddOrGetNamedType(project, semanticModel, doc, symbol);
                }

                foreach (var symbol in interfaceSymbols)
                {
                    DependenciesTreeState.AddOrGetNamedType(project, semanticModel, doc, symbol);
                }
            }
        }

        protected abstract Task InnerAnalyze();

        private void WorkspaceChangeHandler(WorkspaceChangeEventArgs args)
        {
            lock (ChangedHadlerLocker)
            {
                //ReAnalyze(args.NewSolution).GetAwaiter().GetResult();

                foreach (var projectChange in args.NewSolution.GetChanges(args.OldSolution).GetProjectChanges())
                {
                    CompilationCashModel compilationCashModel = DependenciesTreeState[projectChange.NewProject];

                    foreach (var doc in projectChange.GetChangedDocuments())
                    {
                        var document = projectChange.NewProject.GetDocument(doc);
                    }
                }
            }

            WorkspaceChangeHandler();
        }

        private List<IFieldSymbol> BuildDependenciesList(NamedTypeCashModel type)
        {
            if (ProcessedInjections.Contains(type))
                return [];

            if (type is null)
                return [];

            DependencyStack.Push(type);

            var currentTreeNode = SymbolTree.FindNode(type);

            var result = new List<IFieldSymbol>();

            var deps = FindTypeDependencies(type);

            result.AddRange(deps);

            foreach (var dep in deps)
            {
                List<NamedTypeCashModel> implementations = [];
                var node = DependenciesTreeState[dep.Type as INamedTypeSymbol];
                if (node is null)
                    continue;

                if (dep.Type.TypeKind == TypeKind.Interface)
                {
                    implementations.AddRange(FindInterfaceImplementation(node));
                }
                else
                {
                    implementations.Add(node);
                }

                if (implementations is null || implementations.Count == 0)
                    continue;

                foreach (var implement in implementations)
                {
                    currentTreeNode.AddChildrenNode(dep, implement);

                    if (DependencyStack.Contains(implement))
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
            foreach(var DIRootProvider in DIRootsProvidersList)
            {
                foreach(var type in DIRootProvider.GetDIRoots())
                {
                    DIRoots.Add(type);
                    SymbolTree.AddRoot(type);
                }
            }

            List<IFieldSymbol> injectedDeps = new();
            List<IFieldSymbol> controllersDeps = new();

            foreach (var type in DIRoots.AsEnumerable())
            {
                List<IFieldSymbol> allDeps = BuildDependenciesList(type);

                controllersDeps.AddRange(allDeps);
            }
            injectedDeps.AddRange(controllersDeps);

            injectedDeps = [.. injectedDeps.Distinct(FieldSymbolEqualityComparer.Instance)];

            return injectedDeps;
        }

        private List<IFieldSymbol> FindTypeDependencies(NamedTypeCashModel type)
        {
            var typeFields = type.InnerSymbol.GetMembers().OfType<IFieldSymbol>();
            var fieldsInjectedByAttributes = typeFields
                .Where(
                    item =>
                    item.AssociatedSymbol?
                    .GetAttributes()
                    .Any(item => item.AttributeClass?.Name?.Contains("Inject") == true) == true) ?? [];

            var constructor = type.InnerSymbol.Constructors.FirstOrDefault(item => item.Parameters.Length > 0);

            IEnumerable<AssignmentExpressionSyntax> injectingAssignmentsInConstructor = [];
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

        private List<NamedTypeCashModel> FindInterfaceImplementation(NamedTypeCashModel interfaceType)
        {
            List<NamedTypeCashModel> result = [];

            if (interfaceType.InnerSymbol.TypeKind != TypeKind.Interface)
            {
                throw new InvalidOperationException();
            }

            result = DependenciesTreeState.NamedTypeEnumerable.Where(item => item.InnerSymbol.Interfaces.Any(item => NamedTypeEqualityComparer.Instance.Equals(item, interfaceType.InnerSymbol))).ToList();

            InterfacesImplementations.TryAdd(interfaceType, result);

            return result;
        }

        private List<NamedTypeCashModel> GetControllers()
        {
            var controllers = DependenciesTreeState.NamedTypeEnumerable.Where(item =>
                item.Name.Contains("Controller") && item.InnerSymbol.BaseType.Name.Contains("Controller"));

            return controllers.ToList();
        }

        private async Task<List<DependencyRegistrationModel>> GetNinjectRegistrations()
        {
            List<DependencyRegistrationModel> dependencyRegistrations = [];

            foreach(var project in DependenciesTreeState.CompilationModelsEnumerable)
            {
                var projectRegistrations = await GetProjectNinjectRegistrations(project);

                dependencyRegistrations.AddRange(projectRegistrations);
            }

            return dependencyRegistrations;
        }

        private async Task<List<DependencyRegistrationModel>> GetProjectNinjectRegistrations(CompilationCashModel project)
        {
            List<DependencyRegistrationModel> dependencyRegistrations = [];

            foreach (var type in project.NamedTypesList.Where(item => item.InnerSymbol.TypeKind == TypeKind.Class))
            {
                foreach (var searcher in DependencyRegistrationSearchersList)
                {
                    if (!searcher.CheckType(type.InnerSymbol))
                        continue;

                    dependencyRegistrations.AddRange(await searcher.SearchRegistrations(type.InnerSymbol));
                }
            }

            return dependencyRegistrations;
        }

        private async Task SearchInvocations()
        {
            foreach (var field in SymbolTree.Fields)
            {
                if (field.ContainingTypeNode.Symbol.InnerSymbol.TypeKind == TypeKind.Interface)
                    continue;

                SearchFieldInvocations(field);
            }
        }

        private void SearchFieldInvocations(FieldSymbolSubnode field)
        {
            var fieldType = field.FieldTypeNode.Symbol;
            var containingTypeNode = field.ContainingTypeNode.Symbol;

            var methods = field
                .ContainingTypeNode
                .Symbol
                .InnerSymbol
                .GetMembers()
                .OfType<IMethodSymbol>();

            foreach (var method in methods.Where(item => !item.IsImplicitlyDeclared))
            {
                if (!method.DeclaringSyntaxReferences.Any())
                    continue;

                var syntaxTree = method.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree;

                SemanticModel semanticModel;
                Document document;

                if (!SyntaxTreeSemanticMapping.TryGetValue(syntaxTree, out var existedModel))
                {
                    semanticModel = containingTypeNode.CompilationModel.Compilation.GetSemanticModel(method.DeclaringSyntaxReferences.First().SyntaxTree);
                    document = Solution.GetDocument(syntaxTree);
                }
                else
                {
                    (semanticModel, document) = existedModel;
                }

                var methodBody = method.GetMethodBodyOperation(semanticModel);

                var propertiesRefInvocations = methodBody
                .Descendants()
                .OfType<IPropertyReferenceOperation>()
                .Where(item =>
                {
                    if (item.Property.Type is not INamedTypeSymbol propType)
                        return false;

                    var node = DependenciesTreeState[propType];
                    if (node is null || !InterfacesImplementations.ContainsKey(node))
                        return false;

                    if (!PropertiesToFieldsMapping.TryGetValue(item.Property, out var outField) || (outField is null))
                        return false;

                    return FieldSymbolEqualityComparer.Instance.Equals(outField, field.FieldSymbol);
                })
                .Select(item => item.Parent as IInvocationOperation)
                .Where(item => item is not null).ToList();

                foreach (var invocation in propertiesRefInvocations)
                {
                    var methodModel = DependenciesTreeState.AddOrGetMethod(fieldType.Project, semanticModel, document, invocation.TargetMethod);

                    if (methodModel.MethodOuterInvocations.TryGetValue(field.FieldSymbol, out var invocations))
                    {
                        invocations.Add(invocation);
                    }
                    else
                    {
                        invocations = new List<IInvocationOperation>()
                        {
                            invocation
                        };
                        methodModel.MethodOuterInvocations.Add(field.FieldSymbol, invocations);
                    }
                }

                var fieldsRefInvocations = methodBody
                    .Descendants()
                    .OfType<IFieldReferenceOperation>()
                    .Where(item => FieldSymbolEqualityComparer.Instance.Equals(item.Field, field.FieldSymbol))
                    .Select(item => item.Parent as IInvocationOperation)
                    .Where(item => item is not null).ToList();

                foreach (var invocation in fieldsRefInvocations)
                {
                    var methodModel = DependenciesTreeState.AddOrGetMethod(fieldType.Project, semanticModel, document, invocation.TargetMethod);

                    if (methodModel.MethodOuterInvocations.TryGetValue(field.FieldSymbol, out var invocations))
                    {
                        invocations.Add(invocation);
                    }
                    else
                    {
                        invocations = new List<IInvocationOperation>()
                        {
                            invocation
                        };

                        methodModel.MethodOuterInvocations.Add(field.FieldSymbol, invocations);
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
                if (dep.InnerSymbol.TypeKind == TypeKind.Interface)
                    continue;

                await TypeOwnMethodInvocations(dep);
            }
        }

        private async Task TypeOwnMethodInvocations(NamedTypeCashModel dep)
        {
            var methods = dep.InnerSymbol
                .GetMembers()
                .OfType<IMethodSymbol>();

            foreach (var method in methods.Where(item => !item.IsImplicitlyDeclared))
            {
                if (!method.DeclaringSyntaxReferences.Any())
                    continue;

                var syntaxTree = method.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree;

                SemanticModel semanticModel;
                Document document;

                if (!SyntaxTreeSemanticMapping.TryGetValue(syntaxTree, out var existedModel))
                {
                    semanticModel = dep.CompilationModel.Compilation.GetSemanticModel(syntaxTree);
                    document = Solution.GetDocument(syntaxTree);
                }
                else
                {
                    (semanticModel, document) = existedModel;
                }

                var allInvocations = method.GetMethodBodyOperation(semanticModel)
                    .Descendants()
                    .OfType<IInvocationOperation>()
                    .Where(invocation => NamedTypeEqualityComparer.Instance.Equals(dep.InnerSymbol, invocation.TargetMethod.ContainingType));

                foreach (var invocation in allInvocations)
                {
                    var methodModel = DependenciesTreeState.AddOrGetMethod(dep.Project, semanticModel, document, invocation.TargetMethod);

                    methodModel.OwnTypeMethodInvocations.Add(invocation);
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
                    SyntaxTreeSemanticMapping.TryAdd(syntaxTree, (compilation.GetSemanticModel(syntaxTree), doc));
                }
            }
        }

        protected void SetDependencyRegistrationsSearcher<TDependencySearcher>() where TDependencySearcher : class, IDependencyRegistrationsSearcher, new()
        {
            var instance = new TDependencySearcher()
            {
                Solution = Solution,
                DependenciesState = DependenciesTreeState,
            };

            DependencyRegistrationSearchersList.Add(instance);
        }

        protected void SetDIRootsProvider<TDIRootsProvider>() where TDIRootsProvider : class, IDIRootsProvider, new()
        {
            var instance = new TDIRootsProvider()
            {
                Solution = Solution,
                DependenciesState = DependenciesTreeState,
            };

            DIRootsProvidersList.Add(instance);
        }
    }
}