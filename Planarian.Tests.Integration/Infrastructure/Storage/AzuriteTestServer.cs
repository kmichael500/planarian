using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Planarian.Tests;

/// <summary>
/// Real Azurite fixture for integration paths that still use the Azure Blob SDK directly,
/// such as chunked account File imports and archive block-from-URI publication. Business-level
/// durable object storage remains covered separately by the in-memory IObjectStorage fake.
/// </summary>
public sealed class AzuriteTestServer : IAsyncLifetime
{
    private const int BlobPort = 10000;
    private const string AccountName = "devstoreaccount1";
    private const string AccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private IContainer? _container;

    public string ConnectionString
    {
        get
        {
            var container = _container
                ?? throw new InvalidOperationException("Azurite integration fixture has not been initialized.");
            var host = container.Hostname;
            var port = container.GetMappedPublicPort(BlobPort);
            return $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};" +
                   $"BlobEndpoint=http://{host}:{port}/{AccountName};";
        }
    }

    public async Task InitializeAsync()
    {
        PostgresTestServer.ConfigureColimaForTestcontainers();
        _container = new ContainerBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:3.37.0")
            .WithPortBinding(BlobPort, assignRandomHostPort: true)
            .WithCommand("azurite", "--blobHost", "0.0.0.0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(BlobPort))
            .Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}
