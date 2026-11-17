using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.CashModels
{
    public class CompilationCashModel
    {
        public int Generation { get; internal set; }
        public DateTime LastProjectFileUpdate { get; private set; } = DateTime.Now;
        public DateTime LastUpdate { get; private set; } = DateTime.Now;
        public HashSet<NamedTypeCashModel> NamedTypesList { get; set; } = [];
        public HashSet<DocumentCashModel> DocumentsList { get; set; } = [];

        [JsonIgnore]
        public Project Project { get; set; }

        [JsonIgnore]
        public Compilation Compilation { get; set; }

        public void UpGeneration()
        {
            Generation++;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (obj is not CompilationCashModel compilationCashModel)
                return false;

            return this.Project.Name.Equals(compilationCashModel.Project.Name);
        }

        public override int GetHashCode()
        {
            return Project.Name.GetHashCode();
        }

        /// <summary>
        /// Переустановка полей модели компиляции.
        /// </summary>
        /// <param name="compilation">Новая компиляция проекта.</param>
        /// <param name="project">Проект.</param>
        /// <param name="lastProjectFileUpdate">Последнее обновление файла проекта.</param>
        public void ResetCompilation(Compilation compilation, Project project, DateTime? lastProjectFileUpdate)
        {
            Compilation = compilation;
            Project = project;
            LastProjectFileUpdate = lastProjectFileUpdate ?? LastProjectFileUpdate;
            LastUpdate = DateTime.Now;

            Generation++;
        }
    }
}
