using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Services.Factories;
using DomainAnalyze.DependencyTree.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DomainAnalyze.DependencyTree.Services
{
    internal class SolutionFilesWatcher
    {
        private Solution Solution => Workspace.CurrentSolution;
        private ReloadableWorkspace Workspace;
        private FileSystemWatcher FileSystemWatcher;
        private SemaphoreSlim locker = new(1, 1);
        private DependenciesState DependenciesState;

        ConcurrentQueue<FileChangeModel> FilesQueue = new();

        public SolutionFilesWatcher(ReloadableWorkspace workspace, DependenciesState dependenciesState)
        {
            DependenciesState = dependenciesState;
            Workspace = workspace;
        }

        public void AddFileWatcher()
        {
            string solutionFolder = Path.GetDirectoryName(Solution.FilePath);
            FileSystemWatcher = new FileSystemWatcher(solutionFolder, "*.cs")
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            FileSystemWatcher.Changed += HandleFileEdit(WatcherChangeTypes.Changed);

            FileSystemWatcher.Created += HandleFileEdit(WatcherChangeTypes.Created);

            FileSystemWatcher.Deleted += HandleFileEdit(WatcherChangeTypes.Deleted);

            FileSystemWatcher.Renamed += RenameHandler(solutionFolder);

            Task.Run(async () =>
            {
                while (true)
                {
                    await locker.WaitAsync();

                    try
                    {
                        Solution newSolution = Solution;

                        HashSet<FileChangeModel> files = new();

                        while (FilesQueue.TryDequeue(out var file))
                        {
                            if (files.TryGetValue(file, out var existedRecord))
                            {
                                files.Remove(existedRecord);

                                switch (existedRecord.ChangeType)
                                {
                                    case WatcherChangeTypes.Created:
                                        if (file.ChangeType == WatcherChangeTypes.Deleted)
                                            continue;

                                        file.ChangeType = WatcherChangeTypes.Created;
                                        break;
                                    case WatcherChangeTypes.Changed:
                                        if (file.ChangeType == WatcherChangeTypes.Deleted)
                                            file.ChangeType = WatcherChangeTypes.Deleted;
                                        break;
                                    case WatcherChangeTypes.Renamed:

                                        break;
                                    case WatcherChangeTypes.Deleted:
                                        switch (file.ChangeType)
                                        {
                                            case WatcherChangeTypes.Created:
                                            case WatcherChangeTypes.Changed:
                                                file.ChangeType = WatcherChangeTypes.Created;
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                }
                            }

                            files.Add(file);
                        }

                        foreach (var project in files.GroupBy(file => file.Project))
                        {
                            await ReloadProjectAsync(Workspace, FileSystemUtils.SearchProjectFile(project.Key));

                            //switch (file.ChangeType)
                            //{
                            //    case WatcherChangeTypes.Changed:
                            //        var document = NodesCash.GetDocument(file.Key);
                            //        var text = File.ReadAllText(file.Key);
                            //        newSolution = newSolution.WithDocumentText(document.Document.Id, Microsoft.CodeAnalysis.Text.SourceText.From(text));
                            //        break;
                            //    default:
                            //        await ReloadProjectAsync(Workspace, FileSystemUtils.SearchProjectFile(file.Key));
                            //        if (!string.IsNullOrWhiteSpace(file.Value.NewName))
                            //        {
                            //            await ReloadProjectAsync(Workspace, FileSystemUtils.SearchProjectFile(file.Value.NewName));
                            //        }
                            //        break;
                            //}
                        }

                        Workspace.TryApplyChanges(newSolution);

                        await Task.Delay(200);
                    }
                    catch
                    {

                    }
                    finally
                    {
                        locker.Release();
                    }
                }
            });
        }

        private RenamedEventHandler RenameHandler(string solutionFolder)
        {
            return (sender, e) =>
            {
                // являются ли файлы исходниками .cs
                bool isOldCS = e.OldName.EndsWith(".cs");
                bool isNewCS = e.FullPath.EndsWith(".cs");

                if (e.FullPath.Contains("\\bin\\") || e.FullPath.Contains("\\obj\\"))
                    return;

                if (isOldCS && isNewCS || !isOldCS && !isNewCS)
                {
                    FilesQueue.Enqueue(new FileChangeModel
                    {
                        FilePath = Path.Combine(solutionFolder, e.OldName),
                        ChangeType = e.ChangeType,
                        Project = FileSystemUtils.SearchProjectFile(e.OldName),
                    });

                    FilesQueue.Enqueue(new FileChangeModel
                    {
                        FilePath = e.FullPath,
                        ChangeType = e.ChangeType,
                        Project = FileSystemUtils.SearchProjectFile(e.FullPath)
                    });
                }
                else if (isNewCS && !isOldCS)
                {
                    FilesQueue.Enqueue(new FileChangeModel
                    {
                        OldPath = Path.Combine(solutionFolder, e.OldName),
                        ChangeType = WatcherChangeTypes.Changed,
                        FilePath = e.FullPath,
                        Project = FileSystemUtils.SearchProjectFile(e.FullPath),
                        OldProject = FileSystemUtils.SearchProjectFile(Path.Combine(solutionFolder, e.OldName))
                    });
                }
            };
        }

        private FileSystemEventHandler HandleFileEdit(WatcherChangeTypes operationType)
        {
            return (sender, e) =>
            {
                if (e.FullPath.Contains("bin") || e.FullPath.Contains("obj"))
                    return;

                FilesQueue.Enqueue(new FileChangeModel
                {
                    FilePath = e.FullPath,
                    ChangeType = operationType,
                    Project = FileSystemUtils.SearchProjectFile(e.FullPath)
                });
            };
        }

        public async Task ReloadProjectAsync(
            ReloadableWorkspace workspace,
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            var currentSolution = workspace.CurrentSolution;

            var normalizedPath = SolutionInfoFactory.NormalizePath(projectPath);

            var existingProject = currentSolution.Projects
                .FirstOrDefault(p =>
                    p.FilePath != null &&
                    SolutionInfoFactory.NormalizePath(p.FilePath) == normalizedPath);

            if (existingProject is null)
                return;

            using var loader = MSBuildWorkspace.Create();

            loader.LoadMetadataForReferencedProjects = false;

            var loadedProject = await loader.OpenProjectAsync(
                projectPath,
                cancellationToken: cancellationToken);

            var loadedSolution = loadedProject.Solution;

            // Сопоставляем ProjectId загруженных проектов
            // с идентификаторами основного Workspace.
            var projectIds = new Dictionary<string, ProjectId>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var project in loadedSolution.Projects)
            {
                if (project.FilePath == null)
                    continue;

                projectIds[SolutionInfoFactory.NormalizePath(project.FilePath)] =
                    SolutionInfoFactory.GetStableProjectId(
                        currentSolution,
                        project.FilePath);
            }

            // Для перезагружаемого проекта гарантируем
            // сохранение прежнего ProjectId.
            projectIds[normalizedPath] = existingProject.Id;

            var newProjectInfo = await SolutionInfoFactory.CreateProjectInfoAsync(
                loadedProject,
                existingProject,
                existingProject.Id,
                projectIds,
                cancellationToken);

            // Единственная операция изменения основного Workspace.
            workspace.ReloadProject(newProjectInfo);
        }
    }
}
