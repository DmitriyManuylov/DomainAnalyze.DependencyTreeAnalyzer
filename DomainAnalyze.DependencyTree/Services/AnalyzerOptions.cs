namespace DomainAnalyze.DependencyTree.Services
{
    public class AnalyzerOptions
    {
        /// <summary>
        /// Список типов, регистрируемых как зависимость, исключаемые из анализа
        /// </summary>
        public List<string> ExcludedDependencyTypesList { get; set; }

        /// <summary>
        /// Список типов, для которых не выполняется анализ внедряемых в него зависимостей
        /// </summary>
        public List<string> ExcludedDependentTypesList { get; set; }
    }
}
