using Neo4j.Driver;

namespace SocEventService.Services;

public class GraphService : IAsyncDisposable
{
    private readonly IDriver _driver;

    public GraphService(IConfiguration config)
    {
        var uri = config["Neo4j:Uri"] ?? "bolt://neo4j:7687";
        var user = config["Neo4j:User"] ?? "neo4j";
        var pass = config["Neo4j:Password"] ?? "password123";
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, pass));
    }

    public async Task RunWriteAsync(string cypher, object parameters)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx => { await tx.RunAsync(cypher, parameters); });
    }

    public async Task<List<IRecord>> RunReadAsync(string cypher, object? parameters = null)
    {
        await using var session = _driver.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters ?? new { });
            return await cursor.ToListAsync();
        });
    }

    public async ValueTask DisposeAsync() => await _driver.DisposeAsync();
}