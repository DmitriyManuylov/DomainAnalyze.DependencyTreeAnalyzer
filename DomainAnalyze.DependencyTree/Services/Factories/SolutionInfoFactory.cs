using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Services.Factories
{
    internal class SolutionInfoFactory
    {
        public static async Task<SolutionInfo> ExtractSolutionInfoAsync(Solution sourceSolution, CancellationToken cancellationToken)
        {
            var projectIds = sourceSolution.Projects
                .ToDictionary(
                p => SolutionInfoFactory.NormalizePath(p.FilePath!),
                p => ProjectId.CreateNewId(debugName: p.Name),
                StringComparer.OrdinalIgnoreCase);

            // Затем преобразуем все проекты, включая ссылки между ними.
            var projectInfos = new List<ProjectInfo>();

            foreach (var project in sourceSolution.Projects)
            {
                ProjectInfo info = await ExtractProjectInfoAsync(project, cancellationToken);

                projectInfos.Add(info);
            }

            var solutionInfo = SolutionInfo.Create(
                sourceSolution.Id,
                VersionStamp.Create(),
                sourceSolution.FilePath,
                projectInfos,
                sourceSolution.AnalyzerReferences);

            return solutionInfo;
        }

        public static async Task<ProjectInfo> ExtractProjectInfoAsync(Project project, CancellationToken cancellationToken = default)
        {
            var docsInfo = new List<DocumentInfo>();

            foreach (var doc in project.Documents)
            {

                var text = await doc.GetTextAsync(cancellationToken);

                var loader = TextLoader.From(
                    TextAndVersion.Create(
                        text,
                        VersionStamp.Create(),
                        doc.FilePath));

                var docInfo = DocumentInfo.Create(
                    doc.Id,
                    doc.Name,
                    doc.Folders,
                    doc.SourceCodeKind,
                    loader,
                    doc.FilePath);

                docsInfo.Add(docInfo);
            }

            foreach (var doc in project.AdditionalDocuments)
            {

                var text = await doc.GetTextAsync(cancellationToken);

                var loader = TextLoader.From(
                    TextAndVersion.Create(
                        text,
                        VersionStamp.Create(),
                        doc.FilePath));

                var docInfo = DocumentInfo.Create(
                    doc.Id,
                    doc.Name,
                    doc.Folders,
                    SourceCodeKind.Regular,
                    loader,
                    doc.FilePath);

                docsInfo.Add(docInfo);
            }

            var additDocs = new List<DocumentInfo>();

            var info = ProjectInfo.Create(
                id: project.Id,
                version: VersionStamp.Create(),
                name: project.Name,
                assemblyName: project.AssemblyName ?? project.Name,
                language: project.Language,
                filePath: project.FilePath,
                outputFilePath: project.OutputFilePath,
                compilationOptions: project.CompilationOptions,
                parseOptions: project.ParseOptions,
                documents: docsInfo,
                projectReferences: project.ProjectReferences,
                metadataReferences: project.MetadataReferences,
                analyzerReferences: project.AnalyzerReferences,
                additionalDocuments: additDocs,
                isSubmission: project.IsSubmission);
            return info;
        }

        public static ProjectId GetStableProjectId(Solution solution, string projectPath)
        {
            var normalizedPath = NormalizePath(projectPath);

            var existing = solution.Projects.FirstOrDefault(p =>
                p.FilePath != null &&
                NormalizePath(p.FilePath) == normalizedPath);

            return existing?.Id ?? ProjectId.CreateNewId();
        }

        public static DocumentId GetStableDocumentId(
            Project existingProject,
            ProjectId targetProjectId,
            string documentPath)
        {
            var normalizedPath = NormalizePath(documentPath);

            var existing = existingProject.Documents.FirstOrDefault(d =>
                d.FilePath != null &&
                NormalizePath(d.FilePath) == normalizedPath);

            return existing?.Id ?? DocumentId.CreateNewId(targetProjectId);
        }

        public static async Task<DocumentInfo> CreateDocumentInfoAsync(
            TextDocument document,
            DocumentId documentId,
            SourceCodeKind sourceCodeKind,
            CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken);

            var loader = TextLoader.From(
                TextAndVersion.Create(
                    text,
                    VersionStamp.Create(),
                    document.FilePath));

            return DocumentInfo.Create(
                documentId,
                document.Name,
                document.Folders,
                sourceCodeKind,
                loader,
                document.FilePath);
        }

        public static async Task<ProjectInfo> CreateProjectInfoAsync(
            Project source,
            Project existingProject,
            ProjectId stableProjectId,
            IReadOnlyDictionary<string, ProjectId> projectIds,
            CancellationToken cancellationToken = default)
        {
            // Обычные исходные документы.
            var documents = new List<DocumentInfo>();

            foreach (var document in source.Documents)
            {
                var id = GetStableDocumentId(
                    existingProject,
                    stableProjectId,
                    document.FilePath!);

                documents.Add(await CreateDocumentInfoAsync(
                    document,
                    id,
                    document.SourceCodeKind,
                    cancellationToken));
            }

            // AdditionalDocuments.
            var additionalDocuments = new List<DocumentInfo>();

            foreach (var document in source.AdditionalDocuments)
            {
                var id = GetStableDocumentId(
                    existingProject,
                    stableProjectId,
                    document.FilePath!);

                additionalDocuments.Add(await CreateDocumentInfoAsync(
                    document,
                    id,
                    SourceCodeKind.Regular,
                    cancellationToken));
            }

            // AnalyzerConfigDocuments.
            var analyzerConfigDocuments = new List<DocumentInfo>();

            foreach (var document in source.AnalyzerConfigDocuments)
            {
                var id = GetStableDocumentId(
                    existingProject,
                    stableProjectId,
                    document.FilePath!);

                analyzerConfigDocuments.Add(await CreateDocumentInfoAsync(
                    document,
                    id,
                    SourceCodeKind.Regular,
                    cancellationToken));
            }

            // Переназначаем ProjectId ссылок на проекты.
            var projectReferences = source.ProjectReferences
                .Select(reference =>
                {
                    var referencedProject = source.Solution
                        .GetProject(reference.ProjectId)!;

                    var path = NormalizePath(referencedProject.FilePath!);

                    return new ProjectReference(
                        projectIds[path],
                        reference.Aliases,
                        reference.EmbedInteropTypes);
                })
                .ToList();

            return ProjectInfo.Create(
                id: stableProjectId,
                version: VersionStamp.Create(),
                name: source.Name,
                assemblyName: source.AssemblyName ?? source.Name,
                language: source.Language,
                filePath: source.FilePath,
                outputFilePath: source.OutputFilePath,
                compilationOptions: source.CompilationOptions,
                parseOptions: source.ParseOptions,
                documents: documents,
                projectReferences: projectReferences,
                metadataReferences: source.MetadataReferences,
                analyzerReferences: source.AnalyzerReferences,
                additionalDocuments: additionalDocuments,
                isSubmission: source.IsSubmission);
        }

        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar)
                .ToUpperInvariant();
        }
    }
}
