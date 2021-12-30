using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AppKettle.Helpers
{
    public static class Product
    {
        #region Constructors

        static Product()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            Name = versionInfo.ProductName;
            Version = versionInfo.FileVersion;
            BuildTime = File.GetCreationTime(assembly.Location);

        }

        #endregion

        #region Static members

        public static string Name { get; }
        public static string Version { get; }
        public static DateTime BuildTime { get; }

        #endregion
    }
}
