using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class SymbolExtentions
    {
        public static Project GetProject(this ISymbol symbol, Solution solution)
        {
            var syntaxTree = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            if (syntaxTree is null)
                throw new Exception();

            var project = solution.Projects.Where(item => syntaxTree.FilePath.Contains(Path.GetDirectoryName(item.FilePath))).OrderByDescending(item => Path.GetDirectoryName(item.FilePath).Length).FirstOrDefault();

            return project;
        }

        public static async Task<Compilation> GetCompilationAsync(this ISymbol symbol, Solution solution)
        {
            var syntaxTree = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            if (syntaxTree is null)
                throw new Exception();

            var project = solution.Projects.Where(item => syntaxTree.FilePath.Contains(Path.GetDirectoryName(item.FilePath))).OrderByDescending(item => Path.GetDirectoryName(item.FilePath).Length).FirstOrDefault();
            var compilation = await project.GetCompilationAsync();

            return compilation;
        }

        public static async Task<SemanticModel> GetSemanticModelAsync(this ISymbol symbol, Solution solution)
        {
            var syntaxTree = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            if (syntaxTree is null)
                throw new Exception();

            var project = solution.Projects.Where(item => syntaxTree.FilePath.Contains(Path.GetDirectoryName(item.FilePath))).OrderByDescending(item => Path.GetDirectoryName(item.FilePath).Length).FirstOrDefault();

            var compilation = await project.GetCompilationAsync();

            return compilation.GetSemanticModel(syntaxTree);
        }
    }
}
