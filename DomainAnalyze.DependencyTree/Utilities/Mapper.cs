using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities
{
    public class Mapper
    {
        public static string MapSystemTypesToPseudoname(string typeName)
        {
            string result = typeName switch
            {
                "Boolean" => "bool",
                "Int32" => "int",
                "String" => "string",
                _ => typeName
            };

            return result;
        }
    }
}
