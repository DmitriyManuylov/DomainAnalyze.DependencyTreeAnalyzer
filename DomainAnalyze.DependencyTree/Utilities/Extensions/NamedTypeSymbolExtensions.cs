using DomainAnalyze.DependencyTree.Utilities.EqualityComparers;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities.Extensions
{
    public static class NamedTypeSymbolExtensions
    {
        public static bool CustomEquals(this INamedTypeSymbol sourceNamedTypeSymbol, INamedTypeSymbol namedTypeSymbol)
        {
            return NamedTypeEqualityComparer.Instance.Equals(sourceNamedTypeSymbol, namedTypeSymbol);
        }

        public static string TypeFullName(this INamedTypeSymbol namedTypeSymbol)
        {
            return $"{namedTypeSymbol.ContainingNamespace.NamespaceFullName()}.{namedTypeSymbol.Name}";
        }

        public static string TypeNameWithNamespace(this INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol is null)
            {
                return string.Empty;
            }

            return $"{namedTypeSymbol.ContainingNamespace.NamespaceFullName()}.{namedTypeSymbol.Name}";
        }

        public static string TypeFullNameWithGeneric(this INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol is null)
            {
                return string.Empty;
            }

            return $"{namedTypeSymbol.ContainingNamespace.NamespaceFullName()}.{namedTypeSymbol.GenericTypeName()}";
        }

        public static string TypeSymbolFullName(this ITypeSymbol typeSymbol, bool castSystemTypesToAlias = false)
        {
            switch (typeSymbol.Kind)
            {
                case SymbolKind.NamedType:
                    return (typeSymbol as INamedTypeSymbol).GenericTypeName(castSystemTypesToAlias);
                case SymbolKind.ArrayType:
                    return $"{(typeSymbol as IArrayTypeSymbol).ElementType.TypeSymbolFullName(castSystemTypesToAlias)}[]";

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Полное имя обобщенного типа без пространства имен
        /// </summary>
        /// <param name="namedTypeSymbol"></param>
        /// <returns></returns>
        public static string GenericTypeName(this INamedTypeSymbol namedTypeSymbol, bool castSystemTypesToAlias = false)
        {
            var typeParams = namedTypeSymbol.TypeArguments;

            var name = castSystemTypesToAlias ? Mapper.MapSystemTypesToPseudoname(namedTypeSymbol.Name) : namedTypeSymbol.Name;

            if (typeParams.Count() > 0)
            {
                string typesNameList = string.Join(", ", typeParams.Select(item => item.TypeSymbolFullName(castSystemTypesToAlias)));
                return $"{name}<{typesNameList}>";
            }

            return name;
        }
    }
}
