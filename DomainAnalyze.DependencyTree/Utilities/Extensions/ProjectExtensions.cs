using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class ProjectExtensions
    {
        public static async Task<List<INamedTypeSymbol>> GetProjectTypes(this Project project)
        {
            var result = new List<INamedTypeSymbol>();

            var compilation = await project.GetCompilationAsync();

            foreach (var doc in project.Documents)
            {
                var syntaxTree = await doc.GetSyntaxTreeAsync();

                var root = await syntaxTree!.GetRootAsync();

                var semanticModel = compilation!.GetSemanticModel(syntaxTree);

                var types = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>();

                var symbols = types.Select(item => semanticModel.GetDeclaredSymbol(item) as INamedTypeSymbol);

                result.AddRange(symbols);
            }

            return result;
        }
    }
}
