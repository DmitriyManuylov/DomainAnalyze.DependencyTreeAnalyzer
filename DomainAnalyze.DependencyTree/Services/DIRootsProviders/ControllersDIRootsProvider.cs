using DomainAnalyze.DependencyTree.Models.CashModels;
using Microsoft.CodeAnalysis;

namespace DomainAnalyze.DependencyTree.Services.DIRootsProviders
{
    public class ControllersDIRootsProvider : IDIRootsProvider
    {
        public Solution Solution { get; init; }
        public DependenciesTreeState DependenciesState { get; init; }

        public List<NamedTypeCashModel> GetDIRoots()
        {
            var controllers = DependenciesState.NamedTypeEnumerable.Where(item =>
                item.Name.Contains("Controller") && item.InnerSymbol.BaseType.Name.Contains("Controller"));

            return controllers.ToList();
        }
    }
}
