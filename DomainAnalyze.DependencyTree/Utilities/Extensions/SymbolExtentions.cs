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
            var syntaxTree = symbol.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree;
            var project = solution.Projects.FirstOrDefault(item => syntaxTree.FilePath.Contains(Path.GetDirectoryName(item.FilePath)));

            return project;
        }

        public static async Task<Compilation> GetCompilationAsync(this ISymbol symbol, Solution solution)
        {
            return await symbol.GetProject(solution).GetCompilationAsync();
        }

        public static async Task<SemanticModel> GetSemanticModelAsync(this ISymbol symbol, Solution solution)
        {
            var compilation = await symbol.GetCompilationAsync(solution);

            return compilation.GetSemanticModel(symbol.DeclaringSyntaxReferences.FirstOrDefault().SyntaxTree);
        }
    }
}
