using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DomainAnalyze.DependencyTree.Services
{
    public sealed class ProjectLoader
    {
        public async Task<Project> LoadAsync(
            string projectPath,
            CancellationToken cancellationToken)
        {
            using var workspace = MSBuildWorkspace.Create();

            workspace.LoadMetadataForReferencedProjects = false;

            workspace.WorkspaceFailed += (_, e) =>
            {
                Console.WriteLine(e.Diagnostic.Message);
            };

            return await workspace.OpenProjectAsync(
                projectPath,
                cancellationToken: cancellationToken);
        }
    }
}
