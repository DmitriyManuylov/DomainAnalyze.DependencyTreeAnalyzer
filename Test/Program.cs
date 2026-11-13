using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Services;
using DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices;
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
        }

        protected override async Task InnerAnalyze()
        {
            await Task.Yield();
        }
    }
}
