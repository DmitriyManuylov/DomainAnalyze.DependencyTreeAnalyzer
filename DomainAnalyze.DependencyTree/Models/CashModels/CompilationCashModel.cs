using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models.CashModels
{
    internal class CompilationCashModel
    {
        public int Generation { get; private set; }
        public HashSet<NamedTypeCashModel> NamedTypesList { get; set; } = [];
        public HashSet<DocumentCashModel> DocumentsList { get; set; } = [];

        [JsonIgnore]
        public Project Project { get; set; }

        [JsonIgnore]
        public Compilation Compilation { get; set; }

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
    }
}
