using DomainAnalyze.DependencyTree.Models;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Services.DependencyRegistrationsSearchServices
{
    /// <summary>
    /// Стратегия поиска регистраций зависимостей (Ninject)
    /// </summary>
    public interface IDependencyRegistrationsSearcher
    {
        Solution Solution { get; init; }
        HashSet<INamedTypeSymbol> SolutionTypes { get; init; }
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
