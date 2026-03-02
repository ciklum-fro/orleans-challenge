using Orleans.BankAccount.Interfaces;
using Orleans.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configuration from environment variables (Docker-friendly)
var connectionString = Environment.GetEnvironmentVariable("ORLEANS_DB_CONNECTION")
                       ?? "Host=localhost;Port=5432;Database=orleans;Username=postgres;Password=postgres";

var clusterId = Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID") ?? "dev-cluster";
var serviceId = Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID") ?? "BankAccountService";

Console.WriteLine("Orleans Client Configuration:");
Console.WriteLine($"  Cluster ID: {clusterId}");
Console.WriteLine($"  Service ID: {serviceId}");
Console.WriteLine($"  PostgreSQL: {connectionString.Split(';')[0]}");

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Configure Orleans Client to connect to the Silo cluster
// Note: The client must use the same clustering configuration as the Silo
builder.UseOrleansClient(clientBuilder =>
{
    clientBuilder.UseAdoNetClustering(options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = connectionString;
    });
    
    clientBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = clusterId;
        options.ServiceId = serviceId;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();

app.MapPost("/api/accounts/{accountId}", async (Guid accountId, IClusterClient client) =>
{
    var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
    var balance = await account.GetBalance();
    
    return Results.Ok(new 
    { 
        AccountId = accountId, 
        Balance = balance,
        Message = "Account ready" 
    });
})
.WithName("CreateAccount")
.WithDescription("Create a new bank account with the specified ID. If the account already exists, it will return the current balance.");

app.MapPost("/api/accounts/{accountId}/deposit", async (Guid accountId, DepositRequest request, IClusterClient client) =>
{
    var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
    
    await account.Deposit(request.Amount);
    var newBalance = await account.GetBalance();
    
    return Results.Ok(new 
    { 
        AccountId = accountId, 
        Amount = request.Amount,
        NewBalance = newBalance,
        Message = $"Deposited {request.Amount:C}" 
    });
})
.WithName("Deposit")
.WithDescription("Deposit a specified amount into the bank account.");

app.MapPost("/api/accounts/{accountId}/withdraw", async (Guid accountId, WithdrawRequest request, IClusterClient client) =>
{
    try
    {
        var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
        
        await account.Withdraw(request.Amount);
        var newBalance = await account.GetBalance();
        
        return Results.Ok(new 
        { 
            AccountId = accountId, 
            Amount = request.Amount,
            NewBalance = newBalance,
            Message = $"Withdrew {request.Amount:C}" 
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
})
.WithName("Withdraw")
.WithDescription("Withdraw a specified amount from the bank account. Will fail if insufficient funds.");

app.MapGet("/api/accounts/{accountId}/balance", async (Guid accountId, IClusterClient client) =>
{
    var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
    var balance = await account.GetBalance();
    
    return Results.Ok(new { AccountId = accountId, Balance = balance });
})
.WithName("GetBalance")
.WithDescription("Get the current balance of the bank account.");

app.MapGet("/api/accounts/{accountId}/events", async (Guid accountId, IClusterClient client) =>
{
    var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
    var eventLog = await account.GetEventLog();
    
    return Results.Ok(eventLog);
})
.WithName("GetEventLog")
.WithDescription("Get the raw event log for the account. Useful for debugging and auditing.");

app.MapGet("/api/accounts/{accountId}/history", async (Guid accountId, IClusterClient client) =>
{
    var account = client.GetGrain<IEventSourcedBankAccountGrain>(accountId);
    var history = await account.ReconstructTransactionHistory();
    
    return Results.Ok(history);
})
.WithName("GetTransactionHistory")
.WithDescription("Get the full transaction history by reconstructing the account state from events.");

app.MapGet("/health", 
        () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow })
    ).WithName("HealthCheck");

Console.WriteLine("Orleans Client API starting...");

app.Run();

// Request DTOs for API endpoints
public record DepositRequest(decimal Amount);
public record WithdrawRequest(decimal Amount);
