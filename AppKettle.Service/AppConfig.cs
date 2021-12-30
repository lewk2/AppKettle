using System;

namespace AppKettle
{
    public class AppConfig
    {
        #region Constructors

        public AppConfig()
        {
            AllowedHosts = Array.Empty<string>();
            ApplicationUrls = Array.Empty<string>();
        }

        #endregion

        #region Properties

        public string[] AllowedHosts { get; set; }

        public string[] ApplicationUrls { get; set; }
        
        public bool LogAllHeaders { get; set; }

        public SwaggerSettings Swagger { get; set; }
        

        #endregion
    }

    public class SwaggerSettings
    {
        public const string SectionName = "Swagger";

        public bool Enabled { get; set; }

    }
}
