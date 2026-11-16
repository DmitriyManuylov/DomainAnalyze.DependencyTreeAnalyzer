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
