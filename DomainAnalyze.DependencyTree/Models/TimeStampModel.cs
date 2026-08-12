using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models
{
    public class TimeStampModel
    {
        public long SolutionBuildTime { get; internal set; }
        public long DependenciesRegistrationSearchTime {  get; internal set; }
        public long DependenciesSearchTime { get; internal set; }
        public long InvocationsModelBuildTime { get; internal set; }
        public long AnalyzeTime {  get; internal set; }
    }
}
