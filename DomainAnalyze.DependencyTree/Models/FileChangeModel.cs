using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Models
{
    internal class FileChangeModel
    {
        public string FilePath { get; set; }
        public string OldPath { get; set; }
        public string Project {  get; set; }
        public string OldProject { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is null ||  !(obj is FileChangeModel fileChangeModel))
                return false;

            return FilePath == fileChangeModel.FilePath;
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode();
        }
    }
}
