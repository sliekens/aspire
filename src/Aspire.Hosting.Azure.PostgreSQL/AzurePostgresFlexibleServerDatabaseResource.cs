// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// A resource that represents an Azure PostgreSQL database. This is a child resource of an <see cref="AzurePostgresFlexibleServerResource"/>.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="databaseName">The database name.</param>
/// <param name="postgresParentResource">The Azure PostgreSQL parent resource associated with this database.</param>
public class AzurePostgresFlexibleServerDatabaseResource(string name, string databaseName, AzurePostgresFlexibleServerResource postgresParentResource)
    : Resource(name), IResourceWithParent<AzurePostgresFlexibleServerResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent Azure PostgresSQL resource.
    /// </summary>
    public AzurePostgresFlexibleServerResource Parent { get; } = postgresParentResource ?? throw new ArgumentNullException(nameof(postgresParentResource));

    /// <summary>
    /// Gets the connection string expression for the Postgres database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => Parent.GetDatabaseConnectionString(Name, databaseName);

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName { get; } = ThrowIfNullOrEmpty(databaseName);

    /// <summary>
    /// Gets the inner PostgresDatabaseResource resource.
    /// 
    /// This is set when RunAsContainer is called on the AzurePostgresFlexibleServerResource resource to create a local PostgreSQL container.
    /// </summary>
    internal PostgresDatabaseResource? InnerResource { get; private set; }

    /// <inheritdoc />
    public override ResourceAnnotationCollection Annotations => InnerResource?.Annotations ?? base.Annotations;

    private static string ThrowIfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(argument, paramName);
        return argument;
    }

    internal void SetInnerResource(PostgresDatabaseResource innerResource)
    {
        // Copy the annotations to the inner resource before making it the inner resource
        foreach (var annotation in Annotations)
        {
            innerResource.Annotations.Add(annotation);
        }

        InnerResource = innerResource;
    }

    /// <summary>
    /// Gets the connection URI expression for the PostgreSQL database.
    /// </summary>
    /// <remarks>
    /// Format: <c>postgresql://{user}:{password}@{host}:{port}/{database}</c> when password authentication is enabled.
    /// Format: <c>postgresql://{host}:{port}/{database}</c> when using Entra ID authentication.
    /// </remarks>
    public ReferenceExpression UriExpression => Parent.BuildUri(DatabaseName);

    /// <summary>
    /// Gets the JDBC connection string for the Azure PostgreSQL database.
    /// </summary>
    /// <remarks>
    /// <para>Format: <c>jdbc:postgresql://{host}:{port}/{database}?sslmode=require&amp;authenticationPluginClassName=com.azure.identity.extensions.jdbc.postgresql.AzurePostgresqlAuthenticationPlugin</c> when using Entra ID authentication.</para>
    /// <para>Format: <c>jdbc:postgresql://{host}:{port}/{database}</c> when password authentication is enabled. User and password credentials are provided as separate <c>Username</c> and <c>Password</c> properties.</para>
    /// <para>When running as a container, the JDBC connection string does not include the <c>sslmode</c> and <c>authenticationPluginClassName</c> parameters.</para>
    /// </remarks>
    public ReferenceExpression JdbcConnectionString => Parent.BuildJdbcConnectionString(DatabaseName);

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        if (InnerResource is not null)
        {
            foreach (var property in InnerResource.GetConnectionProperties())
            {
                yield return property;
            }
            yield return new("Azure", ReferenceExpression.Create($"false"));
        }
        else
        {
            yield return new("Host", ReferenceExpression.Create($"{Parent.Host}"));
            yield return new("Port", ReferenceExpression.Create($"{Parent.Port}"));
            if (Parent.UserName is not null)
            {
                yield return new("Username", ReferenceExpression.Create($"{Parent.UserName}"));
            }
            if (Parent.Password is not null)
            {
                yield return new("Password", ReferenceExpression.Create($"{Parent.Password}"));
            }
            yield return new("Database", ReferenceExpression.Create($"{DatabaseName}"));
            yield return new("Uri", UriExpression);
            yield return new("JdbcConnectionString", JdbcConnectionString);
            yield return new("Azure", ReferenceExpression.Create($"true"));
        }
    }
}
