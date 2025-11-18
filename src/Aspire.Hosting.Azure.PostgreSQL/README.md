# Aspire.Hosting.Azure.PostgreSQL library

Provides extension methods and resource definitions for an Aspire AppHost to configure Azure Database for PostgreSQL.

## Getting started

### Prerequisites

- Azure subscription - [create one for free](https://azure.microsoft.com/free/)

### Install the package

In your AppHost project, install the Aspire Azure PostgreSQL Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package Aspire.Hosting.Azure.PostgreSQL
```

## Configure Azure Provisioning for local development

Adding Azure resources to the Aspire application model will automatically enable development-time provisioning
for Azure resources so that you don't need to configure them manually. Provisioning requires a number of settings
to be available via .NET configuration. Set these values in user secrets in order to allow resources to be configured
automatically.

```json
{
    "Azure": {
      "SubscriptionId": "<your subscription id>",
      "ResourceGroupPrefix": "<prefix for the resource group>",
      "Location": "<azure location>"
    }
}
```

> NOTE: Developers must have Owner access to the target subscription so that role assignments
> can be configured for the provisioned resources.

## Usage example

Then, in the _AppHost.cs_ file of `AppHost`, register a Postgres database and consume the connection using the following methods:

```csharp
var postgresdb = builder.AddAzurePostgresFlexibleServer("pg")
                        .AddDatabase("postgresdb");

var myService = builder.AddProject<Projects.MyService>()
                       .WithReference(postgresdb);
```

The `WithReference` method configures a connection in the `MyService` project named `postgresdb`. By default, `AddAzurePostgresFlexibleServer` configures [Microsoft Entra ID](https://learn.microsoft.com/azure/postgresql/flexible-server/concepts-azure-ad-authentication) authentication. This requires changes to applications that need to connect to these resources. In the _Program.cs_ file of `MyService`, the database connection can be consumed using the client library [Aspire.Azure.Npgsql](https://www.nuget.org/packages/Aspire.Azure.Npgsql) or [Aspire.Azure.Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Aspire.Azure.Npgsql.EntityFrameworkCore.PostgreSQL):

```csharp
builder.AddAzureNpgsqlDataSource("postgresdb");
```

## Connection Properties

When you reference an Azure PostgreSQL Flexible Server resource using `WithReference`, the following connection properties are made available to the consuming project:

### Azure PostgreSQL Flexible Server

The Azure PostgreSQL Flexible Server resource exposes the following connection properties:

| Property Name | Description |
|---------------|-------------|
| `Host` | The hostname or fully qualified domain name (FQDN) of the Azure PostgreSQL server |
| `Port` | The port number the PostgreSQL server is listening on (5432 for Azure PostgreSQL) |
| `Username` | The username for authentication (only when password authentication is enabled) |
| `Password` | The password for authentication (only when password authentication is enabled) |
| `Uri` | The connection URI in postgresql:// format. When password authentication is enabled, the format is `postgresql://{Username}:{Password}@{Host}:{Port}`. When using Entra ID authentication, the format is `postgresql://{Host}:{Port}` |
| `JdbcConnectionString` | JDBC-format connection string. When using Entra ID authentication, the format is `jdbc:postgresql://{Host}:{Port}?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin`. When password authentication is enabled, the format is `jdbc:postgresql://{Host}:{Port}`. User and password credentials are provided as separate `Username` and `Password` properties when using password authentication. When running as a container, the JDBC connection string does not include the `sslmode` and `authenticationPluginClassName` parameters. |
| `Azure` | A value indicating whether the resource is hosted on Azure (`true`) or running as a container (`false`) |

### Azure PostgreSQL database

The Azure PostgreSQL database resource inherits all properties from its parent `AzurePostgresFlexibleServerResource` and adds:

| Property Name | Description |
|---------------|-------------|
| `Uri` | The connection URI in postgresql:// format. When password authentication is enabled, the format is `postgresql://{Username}:{Password}@{Host}:{Port}/{DatabaseName}`. When using Entra ID authentication, the format is `postgresql://{Host}:{Port}/{DatabaseName}` |
| `JdbcConnectionString` | JDBC connection string with database name. When using Entra ID authentication, the format is `jdbc:postgresql://{Host}:{Port}/{DatabaseName}?sslmode=require&authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin`. When password authentication is enabled, the format is `jdbc:postgresql://{Host}:{Port}/{DatabaseName}`. User and password credentials are provided as separate `Username` and `Password` properties when using password authentication. When running as a container, the JDBC connection string does not include the `sslmode` and `authenticationPluginClassName` parameters. |
| `Database` | The name of the database |
| `Azure` | A value indicating whether the resource is hosted on Azure (`true`) or running as a container (`false`) |

Aspire exposes each property as an environment variable named `[RESOURCE]_[PROPERTY]`. For instance, the `Uri` property of a resource called `postgresdb` becomes `POSTGRESDB_URI`.

## Additional documentation

* https://www.npgsql.org/doc/basic-usage.html
* https://github.com/dotnet/aspire/tree/main/src/Components/README.md

## Feedback & contributing

https://github.com/dotnet/aspire

_*Postgres, PostgreSQL and the Slonik Logo are trademarks or registered trademarks of the PostgreSQL Community Association of Canada, and used with their permission._
