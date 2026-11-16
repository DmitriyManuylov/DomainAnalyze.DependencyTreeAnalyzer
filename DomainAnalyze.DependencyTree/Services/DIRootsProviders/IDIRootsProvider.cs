using DomainAnalyze.DependencyTree.Models.CashModels;
using DomainAnalyze.DependencyTree.Services.DependencyTreeAnalyzer;
using Microsoft.CodeAnalysis;

namespace DomainAnalyze.DependencyTree.Services.DIRootsProviders
{
    public interface IDIRootsProvider
    {
        Solution Solution { get; init; }
        DependenciesTreeState DependenciesState { get; init; }
        List<NamedTypeCashModel> GetDIRoots();
    }
}
