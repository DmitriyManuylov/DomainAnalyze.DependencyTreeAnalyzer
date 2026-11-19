using DomainAnalyze.DependencyTree.Exceptions;
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

namespace DomainAnalyze.DependencyTree.Services.DependencyTreeAnalyzer
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

        protected Dictionary<NamedTypeCashModel, List<NamedTypeCashModel>> InterfacesImplementations;

        private List<IDIRootsProvider> DIRootsProvidersList;
        private List<IDependencyRegistrationsSearcher> DependencyRegistrationSearchersList;
        protected HashSet<NamedTypeCashModel> DIRoots;

        protected DependenciesTreeState DependenciesTreeState;

        private SemaphoreSlim ChangedHadlerLocker = new SemaphoreSlim(1, 1);
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
            InterfacesImplementations = [];
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

            Workspace.RegisterWorkspaceChangedHandler((args) => WorkspaceChangeHandler(args).GetAwaiter().GetResult());
        }

        public DependencyTreeAnalyzer(Solution solution, DependencyAnalyzerOptions analyzerOptions = null)
        {
            AnalyzerOptions = analyzerOptions;
            ReInitAsync(solution).GetAwaiter().GetResult();

            Workspace.RegisterWorkspaceChangedHandler((args) => WorkspaceChangeHandler(args).GetAwaiter().GetResult());
        }

        public async Task ReInitAsync(Solution newSolution)
        {
            IsInitialAnanyzeDone = false;

            Workspace = newSolution.Workspace as ReloadableWorkspace;

            if (Workspace is null && AnalyzerOptions.UseFileWatcher)
            {
                var reloadableWorkspace = new ReloadableWorkspace(HostServices);

                await reloadableWorkspace.InitializeAsync(newSolution);

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

            Workspace.RegisterWorkspaceChangedHandler((args) => WorkspaceChangeHandler(args).GetAwaiter().GetResult());
        }

        public async Task ReAnalyze(Solution solution = default)
        {
            await ReInitAsync(solution ?? Solution);

            await AnalyzeAsync();
        }

        /// <summary>
        /// Обработчик события изменения рабочей области, после обновления дерева зависимостей.
        /// </summary>
        public event Action OnWorkplaceChanged;

        public async Task BuildDependencyTreeModelAsync()
        {
            foreach (var project in Solution.Projects)
            {
                await ExtractProjectTypes(project);
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            var binds = await GetNinjectRegistrationsAsync();
            foreach (var item in binds.Where(item => item.Implementation is not null))
                DIImplementations.Add(item.Implementation);
            stopwatch.Stop();
            TimeStampModel.DependenciesRegistrationSearchTime = stopwatch.ElapsedMilliseconds;

            stopwatch = new Stopwatch();
            stopwatch.Start();
            GetNinjectInjections();
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
            SearchInvocations();
            SearchOwnInvocations();
            stopwatch.Stop();
            TimeStampModel.InvocationsModelBuildTime = stopwatch.ElapsedMilliseconds;
        }

        public async Task AnalyzeAsync()
        {
            if (!IsInitialAnanyzeDone)
                await BuildDependencyTreeModelAsync();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await InnerAnalyze();
            stopwatch.Stop();
            TimeStampModel.AnalyzeTime = stopwatch.ElapsedMilliseconds;
        }

        private async Task ExtractProjectTypes(Project project, bool rewrite = false)
        {
            var compilation = await project.GetCompilationAsync();

            foreach (var diagnostic in compilation.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    throw new Exception("Для первоначального построения модели анализа решение не должно содержать ошибок компиляции");
            }

            foreach (var doc in project.Documents)
            {
                await ExtractDocumentTypes(project, compilation, doc, rewrite);
            }
        }

        private async Task ExtractDocumentTypes(Project project, Compilation compilation, Document doc, bool rewrite = false)
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
                DependenciesTreeState.AddOrGetNamedType(project, semanticModel, doc, symbol, rewrite);
            }

            foreach (var symbol in interfaceSymbols)
            {
                DependenciesTreeState.AddOrGetNamedType(project, semanticModel, doc, symbol, rewrite);
            }
        }

        protected abstract Task InnerAnalyze();

        private async Task WorkspaceChangeHandler(WorkspaceChangeEventArgs args)
        {
            try
            {
                await ChangedHadlerLocker.WaitAsync();

                FileInfo solutionFileInfo = new FileInfo(Solution.FilePath);

                if (solutionFileInfo.LastWriteTime > DependenciesTreeState.LastUpdate)
                {
                    await OnSolutionChangedInnerAsync(args.OldSolution, args.NewSolution);

                    return;
                }

                foreach (var projectChange in args.NewSolution.GetChanges(args.OldSolution).GetProjectChanges())
                {
                    CompilationCashModel compilationCashModel = DependenciesTreeState[projectChange.NewProject];
                    var newCompilation = await projectChange.NewProject.GetCompilationAsync();

                    FileInfo projectFileInfo = new FileInfo(projectChange.OldProject.FilePath);
                    DateTime lastProjectFileUpdate = projectFileInfo.LastWriteTime;

                    if (projectFileInfo.LastWriteTime > compilationCashModel.LastProjectFileUpdate)
                    {
                        compilationCashModel.ResetCompilation(newCompilation, projectChange.NewProject, lastProjectFileUpdate);

                        await OnProjectChangedInnerAsync(compilationCashModel, projectChange.OldProject, projectChange.NewProject, projectFileInfo.LastWriteTime);

                        continue;
                    }

                    compilationCashModel.ResetCompilation(newCompilation, projectChange.NewProject, null);

                    foreach (var doc in projectChange.GetChangedDocuments())
                    {
                        var newDocument = projectChange.NewProject.GetDocument(doc);
                        var oldDocumentModel = DependenciesTreeState[newDocument];

                        if (oldDocumentModel is null)
                            continue;

                        FileInfo documentFileInfo = new FileInfo(newDocument.FilePath);

                        if (documentFileInfo.LastAccessTime > oldDocumentModel.LastUpdate)
                        {
                            try
                            {
                                await OnDocumentChangedInnerAsync(compilationCashModel, oldDocumentModel, newDocument);
                            }
                            catch (DocumentCompilationException ex)
                            {

                            }
                            catch
                            {

                            }
                        }
                    }
                }
            }
            catch
            {

            }
            finally
            {
                ChangedHadlerLocker.Release();
            }

            OnWorkplaceChanged();
        }

        private async Task OnSolutionChangedInnerAsync(Solution oldSolution, Solution newSolution)
        {
            await ReAnalyze();

            OnSolutionChanged(oldSolution, newSolution);
        }

        private async Task OnProjectChangedInnerAsync(CompilationCashModel newCompilationCashModel, Project oldProject, Project newProject, DateTime lastWriteTime)
        {
            foreach (var newDoc in newProject.Documents)
            {
                var docModel = DependenciesTreeState[newDoc];

                try
                {
                    await OnDocumentChangedInnerAsync(newCompilationCashModel, docModel, newDoc);
                }
                catch (DocumentCompilationException ex)
                {

                }
                catch
                {

                }
            }

            OnProjectChanged(newCompilationCashModel, oldProject, newProject);
        }

        private async Task OnDocumentChangedInnerAsync(CompilationCashModel newCompilationCashModel, DocumentCashModel existDocumentModel, Document newDocument)
        {
            var document = existDocumentModel.Document;

            var syntaxTree = await newDocument.GetSyntaxTreeAsync();

            FileInfo documentFileInfo = new FileInfo(document.FilePath);

            var semanticModel = newCompilationCashModel.Compilation.GetSemanticModel(syntaxTree);

            var errors = semanticModel.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error);

            if (errors.Any())
            {
                throw new DocumentCompilationException()
                {
                    FilePath = newDocument.FilePath,
                    Errors = [.. errors.Select(item => item.ToString())],
                };
            }

            await ExtractDocumentTypes(newCompilationCashModel.Project, newCompilationCashModel.Compilation, newDocument, true);

            existDocumentModel.ResetDocumentModel(semanticModel, newDocument, documentFileInfo.LastWriteTime);

            List<NamedTypeCashModel> typesToRemove = [];
            List<NamedTypeCashModel> projectDIImplementations = [];

            foreach (var type in existDocumentModel.NamedTypeList.AsEnumerable())
            {
                if (type.DocumentModelSet.First().Generation < existDocumentModel.Generation)
                {
                    typesToRemove.Add(type);

                    continue;
                }

                if (DIImplementations.Contains(type))
                    projectDIImplementations.Add(type);
            }

            foreach (var type in typesToRemove)
            {
                SymbolTree.RemoveNode(type);

                DependenciesTreeState.RemoveType(type);
            }

            foreach (var type in projectDIImplementations)
            {
                var dependencies = FindTypeDependencies(type);

                foreach (var dependencyField in dependencies)
                {
                    var dependencyTypeModel = DependenciesTreeState[dependencyField.Type as INamedTypeSymbol];
                    var dependencyModel = SymbolTree[dependencyField];

                    if (dependencyModel is not null)
                        SearchFieldInvocations(dependencyModel);

                    if (InterfacesImplementations.ContainsKey(dependencyTypeModel))
                        continue;

                    BuildDependenciesList(dependencyTypeModel);
                }

                SearchOwnMethodInvocations(type);
            }

            OnDocumentChanged(existDocumentModel, newDocument);
        }

        protected abstract void OnSolutionChanged(Solution oldSolution, Solution newSolution);
        protected abstract void OnProjectChanged(CompilationCashModel compilationCashModel, Project oldProject, Project newProject);
        protected abstract void OnDocumentChanged(DocumentCashModel documentCashModel, Document newDocument);

        private void BuildDependenciesList(NamedTypeCashModel type)
        {
            if (ProcessedInjections.Contains(type))
                return;

            if (type is null)
                return;

            DependencyStack.Push(type);

            var currentTreeNode = SymbolTree.FindNode(type);

            var dependencies = FindTypeDependencies(type);

            foreach (var dependency in dependencies)
            {
                List<NamedTypeCashModel> implementations = GetDependencyImplementations(dependency);

                if (implementations is null || implementations.Count == 0)
                    continue;

                foreach (var implementation in implementations)
                {
                    currentTreeNode.AddChildrenNode(dependency, implementation);

                    if (DependencyStack.Contains(implementation))
                        continue;

                    BuildDependenciesList(implementation);
                }
            }

            DependencyStack.Pop();
            ProcessedInjections.Add(type);
        }

        private List<NamedTypeCashModel> GetDependencyImplementations(IFieldSymbol dep)
        {
            List<NamedTypeCashModel> implementations = [];

            var node = DependenciesTreeState[dep.Type as INamedTypeSymbol];

            if (node is null)
                return implementations;

            if (dep.Type.TypeKind == TypeKind.Interface)
            {
                implementations.AddRange(FindInterfaceImplementation(node));
            }
            else
            {
                implementations.Add(node);
            }

            return implementations;
        }

        private void GetNinjectInjections()
        {
            foreach(var DIRootProvider in DIRootsProvidersList)
            {
                foreach(var type in DIRootProvider.GetDIRoots())
                {
                    DIRoots.Add(type);
                    SymbolTree.AddRoot(type);
                }
            }

            foreach (var type in DIRoots.AsEnumerable())
            {
                BuildDependenciesList(type);
            }
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

        private async Task<List<DependencyRegistrationModel>> GetNinjectRegistrationsAsync()
        {
            List<DependencyRegistrationModel> dependencyRegistrations = [];

            foreach(var project in DependenciesTreeState.CompilationModelsEnumerable)
            {
                var projectRegistrations = await GetProjectNinjectRegistrationsAsync(project);

                dependencyRegistrations.AddRange(projectRegistrations);
            }

            return dependencyRegistrations;
        }

        private async Task<List<DependencyRegistrationModel>> GetProjectNinjectRegistrationsAsync(CompilationCashModel project)
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

        private void SearchInvocations()
        {
            foreach (var field in SymbolTree.Fields)
            {
                if (field.ContainingTypeNode.Symbol.InnerSymbol.TypeKind == TypeKind.Interface)
                    continue;

                SearchFieldInvocations(field);
            }
        }

        private void SearchFieldInvocations(FieldSymbolSubnode field, bool rewrite = false)
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

                var documentModel = DependenciesTreeState[syntaxTree];

                var methodBody = method.GetMethodBodyOperation(documentModel.SemanticModel);

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

                    if (!PropertiesToFieldsMapping.TryGetValue(item.Property, out var outField) || outField is null)
                        return false;

                    return FieldSymbolEqualityComparer.Instance.Equals(outField, field.FieldSymbol);
                })
                .Select(item => item.Parent as IInvocationOperation)
                .Where(item => item is not null).ToList();

                foreach (var invocation in propertiesRefInvocations)
                {
                    var methodModel = DependenciesTreeState.AddOrGetMethod(fieldType.Project, documentModel.SemanticModel, documentModel.Document, invocation.TargetMethod, rewrite);

                    if (rewrite)
                    {
                        methodModel.MethodOuterInvocations.Remove(field.FieldSymbol);
                    }

                    if (!rewrite && methodModel.MethodOuterInvocations.TryGetValue(field.FieldSymbol, out var invocations))
                    {
                        invocations.Add(invocation);
                    }
                    else
                    {
                        invocations = [invocation];
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
                    var methodModel = DependenciesTreeState.AddOrGetMethod(fieldType.Project, documentModel.SemanticModel, documentModel.Document, invocation.TargetMethod);

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

        private void SearchOwnInvocations()
        {
            foreach(var root in DIRoots)
                SearchOwnMethodInvocations(root);

            foreach (var dep in DIImplementations)
            {
                if (dep.InnerSymbol.TypeKind == TypeKind.Interface)
                    continue;

                SearchOwnMethodInvocations(dep);
            }
        }

        private void SearchOwnMethodInvocations(NamedTypeCashModel dep, bool rewrite = false)
        {
            var methods = dep.InnerSymbol
                .GetMembers()
                .OfType<IMethodSymbol>();

            foreach (var method in methods.Where(item => !item.IsImplicitlyDeclared))
            {
                if (!method.DeclaringSyntaxReferences.Any())
                    continue;

                var syntaxTree = method.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree;

                var documentModel = DependenciesTreeState[syntaxTree];

                var allInvocations = method.GetMethodBodyOperation(documentModel.SemanticModel)
                    .Descendants()
                    .OfType<IInvocationOperation>()
                    .Where(invocation => NamedTypeEqualityComparer.Instance.Equals(dep.InnerSymbol, invocation.TargetMethod.ContainingType));

                foreach (var invocation in allInvocations)
                {
                    var methodModel = DependenciesTreeState.AddOrGetMethod(dep.Project, documentModel.SemanticModel, documentModel.Document, invocation.TargetMethod, rewrite);

                    methodModel.OwnTypeMethodInvocations.Add(invocation);
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