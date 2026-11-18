using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Exceptions
{
    public class DocumentCompilationException : Exception
    {
        public List<string> Errors { get; set; }
        public string FilePath { get; set; }
    }
}
