using CommandLine;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using System;
using System.IO;
using Transistor.Database.Exporter;

namespace Transistor.Database.Tool
{
    public static class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .MapResult(RetrieveToken, _ => -1);
        }

        private static int RetrieveToken(Options options)
        {
            Console.WriteLine("Logging into Azure");
            var tokenProvider = new SimpleTokenProvider(options.DirectoryId);

            Console.WriteLine("Token retrieved starting database extraction...");
            var bacpacPath = Extract(options, tokenProvider);

            Console.WriteLine("Extracted bacpac downloaded to temp file: " + bacpacPath);
            Import(options, bacpacPath);
            Console.WriteLine("Import complete");
            return 0;
        }

        private static string Extract(Options options, IUniversalAuthProvider tokenProvider)
        {
            var sourceBuilder = new SqlConnectionStringBuilder
            {
                DataSource = options.SourceServer,
                InitialCatalog = options.Database,
                ConnectTimeout = 30,
                PersistSecurityInfo = false,
                TrustServerCertificate = false,
                Encrypt = true
            };

            var tempFile = Path.GetTempFileName();

            var sourceService = new DacServices(sourceBuilder.ToString(), tokenProvider);

            using (var file = File.Open(tempFile, FileMode.Create))
            {
                sourceService.ExportBacpac(file, options.Database);
            }

            return tempFile;
        }

        private static void Import(Options options, string bacpacPath)
        {
            var targetBuilder = new SqlConnectionStringBuilder
            {
                DataSource = options.TargetServer,
                InitialCatalog = options.Database,
                IntegratedSecurity = true
            };

            var targetService = new DacServices(targetBuilder.ToString());
            var bacpac = BacPackage.Load(bacpacPath);

            targetService.ImportBacpac(bacpac, options.Database);
        }
    }
}