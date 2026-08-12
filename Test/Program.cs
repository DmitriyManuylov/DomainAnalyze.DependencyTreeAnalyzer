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
            var an = new An(solutionPath);

            an.SetDependencyRegistrationsSearcher<DefaultNinjectRegistrationsSearcher>();
            await an.Analyze();
        }
    }

    public class An : DependencyTreeAnalyzer
    {
        public An(string solutionPath) : base(solutionPath)
        {
        }

        protected override Task InnerAnalyze()
        {
            throw new NotImplementedException();
        }
    }
}
