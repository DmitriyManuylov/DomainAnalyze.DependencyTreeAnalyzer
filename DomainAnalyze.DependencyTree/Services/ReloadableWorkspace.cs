using DomainAnalyze.DependencyTree.Services.Factories;
using DomainAnalyze.DependencyTree.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using System.Reflection.Metadata;
using System.Threading;

namespace DomainAnalyze.DependencyTree.Services
{
    public sealed class ReloadableWorkspace : Workspace
    {
        private HostServices HostServices;
        public ReloadableWorkspace(HostServices hostServices)
            : base(hostServices, "ReloadableWorkspace")
        {
            HostServices = hostServices;
        }

        public async Task ReloadSolution()
        {
            using MSBuildWorkspace tempWorkspace = MSBuildWorkspace.Create(HostServices);
            var newSolution = await tempWorkspace.OpenSolutionAsync(CurrentSolution.FilePath);
            
            await InitializeAsync(newSolution);
        }

        public static ReloadableWorkspace Create(HostServices hostServices)
        {
            return new ReloadableWorkspace(hostServices);
        }

        public async Task InitializeAsync(Solution sourceSolution, CancellationToken cancellationToken = default)
        {
            SolutionInfo solutionInfo = await SolutionInfoFactory.ExtractSolutionInfoAsync(sourceSolution, cancellationToken);

            OnSolutionAdded(solutionInfo);
        }

        public void ReloadProject(ProjectInfo projectInfo)
        {
            OnProjectReloaded(projectInfo);
        }

        public void AddProject(ProjectInfo projectInfo)
        {
            OnProjectAdded(projectInfo);
        }

        public void RemoveProject(ProjectId projectId)
        {
            OnProjectRemoved(projectId);
        }

        public event EventHandler OnSolutionChanged;
        public event EventHandler OnProjectChanged;
        public event EventHandler OnDocumentChanged;
    }
}
