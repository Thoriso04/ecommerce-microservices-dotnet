namespace SocEventService.Services;

public class GraphSeedService
{
    private readonly GraphService _graph;
    private readonly ILogger<GraphSeedService> _logger;

    public GraphSeedService(GraphService graph, ILogger<GraphSeedService> logger)
    {
        _graph = graph; _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Seeding knowledge graph");

        await _graph.RunWriteAsync(@"
            MERGE (gw:Service {name:'api-gateway'})
            MERGE (us:Service {name:'user-service'})
            MERGE (ps:Service {name:'product-service'})
            MERGE (os:Service {name:'order-service'})
            MERGE (gw)-[:DEPENDS_ON]->(us)
            MERGE (gw)-[:DEPENDS_ON]->(ps)
            MERGE (gw)-[:DEPENDS_ON]->(os)
            MERGE (os)-[:DEPENDS_ON]->(ps)
        ", new { });

        await _graph.RunWriteAsync(@"
            MATCH (us:Service {name:'user-service'})
            MATCH (ps:Service {name:'product-service'})
            MATCH (os:Service {name:'order-service'})
            MERGE (e1:Endpoint {path:'/api/users/login'})
            MERGE (e2:Endpoint {path:'/api/products'})
            MERGE (e3:Endpoint {path:'/api/orders'})
            MERGE (us)-[:EXPOSES]->(e1)
            MERGE (ps)-[:EXPOSES]->(e2)
            MERGE (os)-[:EXPOSES]->(e3)
        ", new { });

        await _graph.RunWriteAsync(@"
            MERGE (t1:Threat {name:'CredentialAttack', description:'Repeated login attempts to guess or stuff credentials (MITRE T1110)'})
            MERGE (t2:Threat {name:'UnauthorizedAccess', description:'Access attempts to protected resources without valid authorization'})
            MERGE (t3:Threat {name:'ServiceDependencyFailure', description:'Failure of a dependency causing cascading service disruption'})
            MERGE (t4:Threat {name:'DenialOfService', description:'Abnormally high request volume aimed at degrading service availability'})

            MERGE (c1:Control {name:'AccountLockout', description:'Lock account after repeated failed logins'})
            MERGE (c2:Control {name:'MFA', description:'Require multi-factor authentication'})
            MERGE (c3:Control {name:'PasswordResetProcedure', description:'Force password reset on suspected compromise'})
            MERGE (c4:Control {name:'RateLimiting', description:'Limit requests per client at the API Gateway'})
            MERGE (c5:Control {name:'CircuitBreaker', description:'Stop calling a failing dependency to prevent cascading failure'})

            MERGE (v1:Vulnerability {name:'WeakPasswordPolicy'})
            MERGE (v2:Vulnerability {name:'NoRateLimitingOnLogin'})

            MERGE (c1)-[:MITIGATES]->(t1)
            MERGE (c2)-[:MITIGATES]->(t1)
            MERGE (c3)-[:MITIGATES]->(t1)
            MERGE (c4)-[:MITIGATES]->(t4)
            MERGE (c5)-[:MITIGATES]->(t3)
            MERGE (t1)-[:EXPLOITS]->(v1)
            MERGE (t1)-[:EXPLOITS]->(v2)

            MERGE (d1:Document {name:'MITRE ATT&CK T1110 Brute Force', url:'https://attack.mitre.org/techniques/T1110/'})
            MERGE (d1)-[:DESCRIBES]->(c1)
            MERGE (d1)-[:SUPPORTS_RESPONSE_TO]->(t1)
        ", new { });

        await _graph.RunWriteAsync(@"
            MATCH (t1:Threat {name:'CredentialAttack'})
            MATCH (t2:Threat {name:'UnauthorizedAccess'})
            MATCH (t3:Threat {name:'ServiceDependencyFailure'})
            MATCH (t4:Threat {name:'DenialOfService'})
            MATCH (us:Service {name:'user-service'})
            MATCH (ps:Service {name:'product-service'})
            MATCH (os:Service {name:'order-service'})
            MERGE (t1)-[:TARGETS]->(us)
            MERGE (t2)-[:TARGETS]->(ps)
            MERGE (t2)-[:TARGETS]->(os)
            MERGE (t3)-[:TARGETS]->(os)
            MERGE (t4)-[:TARGETS]->(us)
        ", new { });

        await _graph.RunWriteAsync(@"
            MERGE (a1:Asset {name:'user-database', type:'Database'})
            MERGE (a2:Asset {name:'product-database', type:'Database'})
            MERGE (a3:Asset {name:'order-database', type:'Database'})
            MATCH (us:Service {name:'user-service'})
            MATCH (ps:Service {name:'product-service'})
            MATCH (os:Service {name:'order-service'})
            MERGE (us)-[:USES]->(a1)
            MERGE (ps)-[:USES]->(a2)
            MERGE (os)-[:USES]->(a3)
        ", new { });

        await _graph.RunWriteAsync(@"
            MERGE (r1:Role {name:'Customer'})
            MERGE (r2:Role {name:'Admin'})
        ", new { });

        _logger.LogInformation("Graph seeding complete");
    }
}