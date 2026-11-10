using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainAnalyze.DependencyTree.Utilities
{
    internal static class FileSystemUtils
    {
        /// <summary>
        /// Ищет в текущей или родительских папках для файла по пути <paramref name="path"/> файл проекта .csproj
        /// </summary>
        /// <param name="path">Путь к файлу</param>
        /// <returns></returns>
        public static string SearchProjectFile(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException();

            var directoryInfo = new DirectoryInfo(directory);

            FileInfo projectFile = null;

            while (true)
            {
                if (directoryInfo is null)
                    throw new DirectoryNotFoundException();

                projectFile = directoryInfo.EnumerateFiles().FirstOrDefault(file => file.Extension == ".csproj");
                if (projectFile is not null)
                    return projectFile.FullName;

                directoryInfo = directoryInfo.Parent;
            }
        }
    }
}
