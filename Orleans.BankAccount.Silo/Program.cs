using Orleans.BankAccount.Grains.Services;
using Orleans.BankAccount.Interfaces.Infrastructure;
using Orleans.Configuration;
using Orleans.Dashboard;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("ORLEANS_DB_CONNECTION")
                       ?? "Host=localhost;Port=5432;Database=orleans;Username=postgres;Password=postgres";

var clusterId = Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "dev-cluster";
var serviceId = Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ?? "BankAccountService";
var pulsarServiceUrl = Environment.GetEnvironmentVariable("PULSAR_SERVICE_URL") ?? "pulsar://localhost:6650";
var pulsarTopic = Environment.GetEnvironmentVariable("PULSAR_TOPIC") ?? "bank-events";
var advertisedIp = Environment.GetEnvironmentVariable("ADVERTISED_IP");
var siloPort = int.Parse(Environment.GetEnvironmentVariable("SILO_PORT") ?? "11111");
var gatewayPort = int.Parse(Environment.GetEnvironmentVariable("GATEWAY_PORT") ?? "30000");

Console.WriteLine($"Starting Orleans Silo...");
Console.WriteLine($"Cluster ID: {clusterId}");
Console.WriteLine($"Service ID: {serviceId}");
Console.WriteLine($"PostgreSQL: {connectionString.Split(';')[0]}");
Console.WriteLine($"Pulsar: {pulsarServiceUrl}");
Console.WriteLine($"Advertised IP: {advertisedIp ?? "auto-detect"}");
Console.WriteLine($"Silo Port: {siloPort}, Gateway Port: {gatewayPort}");

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseAdoNetClustering(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = connectionString;
    });

    siloBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = clusterId;
        options.ServiceId = serviceId;
    });

    siloBuilder.ConfigureEndpoints(
        siloPort: siloPort,
        gatewayPort: gatewayPort,
        advertisedIP: string.IsNullOrEmpty(advertisedIp) ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Parse(advertisedIp),
        listenOnAnyHostAddress: true
    );
    
    siloBuilder.UseTransactions();

    // Configure PostgreSQL for default grain storage
    siloBuilder.AddAdoNetGrainStorageAsDefault(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = connectionString;
    });

    // Configure PostgreSQL for event log storage
    siloBuilder.AddAdoNetGrainStorage("LogStorage", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = connectionString;
    });
    
    siloBuilder.AddLogStorageBasedLogConsistencyProvider();

    siloBuilder.AddDashboard();

    siloBuilder.ConfigureServices(services =>
    {
        services.AddSingleton<IEventPublisher>(_ => new PulsarEventPublisher(pulsarServiceUrl, pulsarTopic));
        
        Console.WriteLine($"Registered IEventPublisher: PulsarEventPublisher (topic: {pulsarTopic})");
    });
});

var app = builder.Build();

app.MapOrleansDashboard(routePrefix: "/dashboard");

Console.WriteLine("Orleans Silo starting...");

await app.RunAsync();