using CommandLine;

namespace Transistor.Database.Exporter
{
    public class Options
    {
        [Option('s', "Source Server", Required = true, HelpText = "The server to connect to for the database i.e. example.database.windows.net")]
        public string SourceServer { get; set; }

        [Option('t', "Target Server", Required = true, HelpText = "The server to restore the database on.")]
        public string TargetServer { get; set; }

        [Option('d', "database", Required = true, HelpText = "The database to copy locally")]
        public string Database { get; set; }

        [Option('a', "directoryId", Required = false, HelpText = "The directory id, required if you have multiple directories assosiated with your account.")]
        public string DirectoryId { get; set; }
    }
}