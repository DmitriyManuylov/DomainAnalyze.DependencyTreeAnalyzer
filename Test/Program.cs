using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Models.CashModels;
using DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices;
using DomainAnalyze.DependencyTree.Services.DependencyTreeAnalyzer;
using DomainAnalyze.DependencyTree.Services.DIRootsProviders;
using Microsoft.CodeAnalysis;
using System.Configuration;

namespace Test
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var solutionPath = ConfigurationManager.AppSettings.Get("SolutionPath");

            var options = new DependencyAnalyzerOptions
            {
                UseFileWatcher = true,
            };

            var an = new An(solutionPath, options);

            await an.Analyze();

            while (true)
            {
                await Task.Delay(10000);
            }
        }
    }

    public class An : DependencyTreeAnalyzer
    {
        public An(string solutionPath, DependencyAnalyzerOptions analyzerOptions) : base(solutionPath, analyzerOptions)
        {
            SetDependencyRegistrationsSearcher<DefaultNinjectRegistrationsSearcher>();
            SetDIRootsProvider<ControllersDIRootsProvider>();
        }

        protected override async Task InnerAnalyze()
        {
            await Task.Yield();
        }

        protected override void OnDocumentChanged(DocumentCashModel documentCashModel, Document newDocument)
        {
            
        }

        protected override void OnProjectChanged(CompilationCashModel compilationCashModel, Project oldProject, Project newProject)
        {
            
        }

        protected override void OnSolutionChanged(Solution oldSolution, Solution newSolution)
        {
            
        }
    }
}
