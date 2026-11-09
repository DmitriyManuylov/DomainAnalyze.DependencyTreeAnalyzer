using Microsoft.CodeAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DomainAnalyze.DependencyTree.Services
{
    internal enum FileOperationTypeEnum
    {
        Created,
        Changed,
        Renamed,
        Deleted,
    }

    internal class SolutionFilesWatcher
    {
        private Solution Solution;
        private FileSystemWatcher FileSystemWatcher;
        private SemaphoreSlim locker = new SemaphoreSlim(1, 1);
        private NodesCash NodesCash;

        ConcurrentDictionary<string, FileOperationTypeEnum> filesDictionary = new ConcurrentDictionary<string, FileOperationTypeEnum>();

        public SolutionFilesWatcher(Solution solution, NodesCash nodesCash)
        {
            Solution = solution;
            NodesCash = nodesCash;
        }

        public void AddFileWatcher()
        {
            string solutionFolder = Path.GetDirectoryName(Solution.FilePath);
            FileSystemWatcher = new FileSystemWatcher(solutionFolder, "*.cs");
            FileSystemWatcher.EnableRaisingEvents = true;

            Task.Run(async () =>
            {
                while (true)
                {
                    await locker.WaitAsync();

                    Solution newSolution = Solution;
                    foreach (var doc in filesDictionary)
                    {
                        var document = NodesCash.GetDocument(doc.Key);
                        var text = File.ReadAllText(doc.Key);
                        newSolution.withd
                        newSolution = newSolution.WithDocumentText(document.Document.Id, Microsoft.CodeAnalysis.Text.SourceText.From(text));
                    }

                    Solution.Workspace.TryApplyChanges(newSolution);

                    locker.Release();
                }
            });

            FileSystemWatcher.Changed += HandleFileChanging(FileOperationTypeEnum.Changed);

            FileSystemWatcher.Created += HandleFileChanging(FileOperationTypeEnum.Created);

            FileSystemWatcher.Renamed += (sender, e) =>
            {
                if (locker.CurrentCount == 0)
                    return;

                if (!e.FullPath.EndsWith(".cs"))
                    return;

                filesDictionary.TryAdd(e.FullPath, FileOperationTypeEnum.Renamed);
            };

            FileSystemWatcher.Deleted += HandleFileChanging(FileOperationTypeEnum.Deleted);
        }

        private FileSystemEventHandler HandleFileChanging(FileOperationTypeEnum operationType)
        {
            return (sender, e) =>
            {
                if (locker.CurrentCount == 0)
                    return;

                    if (!e.FullPath.EndsWith(".cs"))
                    return;

                filesDictionary.TryAdd(e.FullPath, changed);
            };
        }
    }
}
