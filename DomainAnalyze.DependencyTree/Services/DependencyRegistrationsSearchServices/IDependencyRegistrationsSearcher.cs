using DomainAnalyze.DependencyTree.Models;
using DomainAnalyze.DependencyTree.Services.DependencyTreeAnalyzer;
using Microsoft.CodeAnalysis;

namespace DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices
{
    /// <summary>
    /// Стратегия поиска регистраций зависимостей (Ninject)
    /// </summary>
    public interface IDependencyRegistrationsSearcher
    {
        Solution Solution { get; init; }
        DependenciesTreeState DependenciesState { get; init; }
        /// <summary>
        /// Проверка соответствия типа условиям применимости
        /// </summary>
        /// <returns></returns>
        bool CheckType(INamedTypeSymbol type);

        /// <summary>
        /// Поиск регистраций зависимостей в определении типа
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        Task<List<DependencyRegistrationModel>> SearchRegistrations(INamedTypeSymbol type);
    }
}
