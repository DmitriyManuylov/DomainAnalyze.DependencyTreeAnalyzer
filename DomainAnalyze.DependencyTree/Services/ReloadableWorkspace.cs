using DomainAnalyze.DependencyTree.Services.Factories;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using System.Reflection.Metadata;
using System.Threading;

namespace DomainAnalyze.DependencyTree.Services
{
    public sealed class ReloadableWorkspace : Workspace
    {
        public ReloadableWorkspace(HostServices hostServices)
            : base(hostServices, "ReloadableWorkspace")
        {

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
    }
}
