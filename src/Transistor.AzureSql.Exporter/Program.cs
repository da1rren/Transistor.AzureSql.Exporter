using CommandLine;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Transistor.Database.Exporter;

namespace Transistor.Database.Tool
{
    public static class Program
    {
        public static CancellationTokenSource CancellationSource = new CancellationTokenSource();

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("Cancelation Requested");
                CancellationSource.Cancel();
            };

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
            var totalTables = TotalTables(sourceBuilder.ToString(), tokenProvider.GetValidAccessToken(), options.ExcludeTemporalTables);
            var progress = new ProgressBar(totalTables, "Beginning Extraction", new ProgressBarOptions { ProgressBarOnBottom = true, DisplayTimeInRealTime = true, });

            using (var file = File.Open(tempFile, FileMode.Create))
            {
                var children = new Dictionary<string, ChildProgressBar>();

                sourceService.ProgressChanged += (data, @event) =>
                {
                    var dataTypeName = data.GetType().FullName;
                    if (data != null &&
                        (
                            dataTypeName == "Microsoft.Data.Tools.Schema.Sql.Dac.OperationLogger" ||
                            dataTypeName == "Microsoft.SqlServer.Dac.Operation" ||
                            dataTypeName == "Microsoft.Data.Tools.Schema.Sql.Dac.Data.ExportBacpacStep"
                        )
                    )
                    {
                        progress.Message = @event.Message;
                        return;
                    }

                    if (dataTypeName == "Microsoft.Data.Tools.Schema.Sql.Dac.ObjectModel.Table")
                    {
                        var schema = data.GetType().GetProperty("SchemaName").GetValue(data);
                        var table = data.GetType().GetProperty("Name").GetValue(data);
                        var key = $"{schema}.{table}";

                        switch (@event.Status)
                        {
                            case DacOperationStatus.Pending:
                                break;

                            case DacOperationStatus.Running:
                                children.Add(key, progress.Spawn(1, @event.Message, new ProgressBarOptions { CollapseWhenFinished = true, ProgressBarOnBottom = true, DisplayTimeInRealTime = true }));
                                break;

                            case DacOperationStatus.Completed:
                            case DacOperationStatus.Cancelled:
                            case DacOperationStatus.Faulted:
                                children[key].Tick(@event.Message);
                                children[key].Dispose();
                                progress.Tick(@event.Message);
                                break;
                        }
                    };
                };

                if (options.ExcludeTemporalTables)
                {
                    var tables = ListNonTemporalTables(sourceBuilder.ToString(), tokenProvider.GetValidAccessToken());
                    sourceService.ExportBacpac(file, options.Database, tables: tables, cancellationToken: CancellationSource.Token);
                }
                else
                {
                    sourceService.ExportBacpac(file, options.Database, cancellationToken: CancellationSource.Token);
                }

                progress.Dispose();
            }

            return tempFile;
        }

        private static int TotalTables(string connectionString, string accessToken, bool excludeTemporal)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.AccessToken = accessToken;
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "select count(*) from sys.tables";

                if (excludeTemporal)
                {
                    command.CommandText += " where temporal_type != 1";
                }

                return (int)command.ExecuteScalar();
            }
        }

        private static IEnumerable<Tuple<string, string>> ListNonTemporalTables(string connectionString, string accessToken)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.AccessToken = accessToken;
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "select SCHEMA_NAME([schema_id]), [name] from sys.tables where temporal_type != 1";
                var reader = command.ExecuteReader();
                var tables = new List<Tuple<string, string>>();

                while (reader.Read())
                {
                    tables.Add(new Tuple<string, string>(reader.GetString(0), reader.GetString(1)));
                }

                return tables;
            }
        }

        private static void Import(Options options, string bacpacPath)
        {
            DropExistingDatabase(options.TargetServer, options.Database);
            var targetBuilder = new SqlConnectionStringBuilder
            {
                DataSource = options.TargetServer,
                InitialCatalog = options.Database,
                IntegratedSecurity = true
            };

            var targetService = new DacServices(targetBuilder.ToString());
            var bacpac = BacPackage.Load(bacpacPath);

            targetService.ImportBacpac(bacpac, options.Database, cancellationToken: CancellationSource.Token);
        }

        private static void DropExistingDatabase(string targetServer, string targetDatabase)
        {
            Console.WriteLine("Dropping existing database");
            var masterBuilder = new SqlConnectionStringBuilder
            {
                DataSource = targetServer,
                InitialCatalog = "master",
                IntegratedSecurity = true
            };

            try
            {
                var masterConnection = new SqlConnection(masterBuilder.ToString());
                masterConnection.Open();
                var command = masterConnection.CreateCommand();
                command.CommandText = @$"
                    ALTER DATABASE [{targetDatabase}] SET OFFLINE WITH ROLLBACK IMMEDIATE
                    ALTER DATABASE [{targetDatabase}] SET ONLINE WITH ROLLBACK IMMEDIATE
                    DROP DATABASE [{targetDatabase}]";

                command.ExecuteNonQuery();
            }
            catch
            {
                Console.WriteLine("Could not drop existing database");
            }
        }
    }
}