using Consul;
using Ocelot.Logging;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Consul.Interfaces;

public sealed class DockerConsulServiceBuilder : DefaultConsulServiceBuilder
{
    public DockerConsulServiceBuilder(
        Func<ConsulRegistryConfiguration> configuration,
        IConsulClientFactory clientFactory,
        IOcelotLoggerFactory loggerFactory)
        : base(configuration, clientFactory, loggerFactory)
    {
    }

    protected override string GetDownstreamHost(ServiceEntry entry, Node node)
        => entry.Service.Address;
}