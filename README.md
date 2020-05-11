# Azure Database Export Tool

Allows a developer to export a database from Azure Sql to their local instance without having to try figure out the sqlbacpac utility.  Supports MFA via a credential prompt.

## Usage:
```
  dotnet import-azdb -s ExampleServer.database.windows.net -t (LocalDb)\MSSQLLocalDB -d ExampleDatabase

  -s, --Source Server    Required. The server to connect to for the database i.e. example.database.windows.net

  -t, --Target Server    Required. The server to restore the database on.  I.E. Local

  -d, --database         Required. The database to copy locally

  -a, --directoryId      The directory id, required if you have multiple directories associated with your account.
```

## Installation
```
dotnet tool install -g Transistor.AzureSql.Exporter
```