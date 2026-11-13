using DomainAnalyze.DependencyTree.Models.CashModels;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Services.DIRootsProviders
{
    public interface IDIRootsProvider
    {
        Solution Solution { get; init; }
        DependenciesTreeState DependenciesState { get; init; }
        List<NamedTypeCashModel> GetDIRoots();
    }
}
