using MessageSample;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Information)
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddEnvironmentVariables();

builder.Host.UseSerilog();

if (builder.Configuration.GetValue<bool>("Controllers"))
{
    builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(setup =>
        setup.CustomSchemaIds(x => x.FullName));
}

var factory = new ConnectionFactory();
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("rabbitmq")))
    factory.Uri = new Uri(builder.Configuration.GetConnectionString("rabbitmq"));
else
    factory.HostName = "localhost";

builder.Services.AddSingleton<IConnection>(_ => factory.CreateConnection());

if (builder.Configuration.GetValue<bool>("Consumers"))
{
    var faultyCook = FaultyCookImplementation.Create(builder.Configuration.GetValue("FaultyCook", 0.0));
    builder.Services.AddSingleton(faultyCook);
    Log.Logger.Information("Starting Consumers with {FaultyCook} FaultyCook", faultyCook.FaultThreshold);
    MessageSample.CommandDriven.Topology.Configure(builder);
    MessageSample.EventDriven.Topology.Configure(builder);
    MessageSample.DocumentDriven.Topology.Configure(builder);
    MessageSample.CommandDrivenPipeline.Topology.Configure(builder);
    MessageSample.Saga.Topology.Configure(builder);
}
else
{
    Log.Logger.Information("Not starting the Consumers");
}

WaitUntilRabbitMqIsReady(factory);

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Controllers"))
{
// Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
}

Log.Information("Defining topologies....");

MessageSample.CommandDriven.Topology.DefineTopology(app);
MessageSample.EventDriven.Topology.DefineTopology(app);
MessageSample.DocumentDriven.Topology.DefineTopology(app);
MessageSample.CommandDrivenPipeline.Topology.DefineTopology(app);
MessageSample.Saga.Topology.DefineTopology(app);

Log.Information("Starting the application....");

app.Run();

void WaitUntilRabbitMqIsReady(ConnectionFactory factory)
{
    Log.Information("Trying to connect to RabbitMQ {Connection} {Host}", factory.Uri, factory.HostName);
    var retry = 0;
    var connected = false;
    do
        try
        {
            using var connection = factory.CreateConnection();
            connected = connection.IsOpen;
        }
        catch (Exception e)
        {
            Log.Information(e, "RabbitMQ is not ready yet - retry {Retry}", retry);
            Thread.Sleep(500);
            retry++;
        }
    while (!connected && retry <= 10);

    if (!connected)
    {
        Log.Error("Failed to connect to RabbitMQ via {Connection} {Host}", factory.Uri, factory.HostName);
        throw new Exception($"Failed to connect to RabbitMQ via {factory.Uri}");
    }

    Log.Information("RabbitMQ is Ready {Connection} {Host}", factory.Uri, factory.HostName);
}

public static class IModelExtensions
{
    public static Dictionary<string, object> PrepareDqlFor(this IModel channel, string prefix)
    {
        var exchangeName = prefix + ".dead-letter-exchange";
        var dlqQueueName = prefix + ".dlq";
        channel.ExchangeDeclare(exchangeName, "fanout", durable: true);

        channel.QueueDeclare(dlqQueueName, true, false, false, null);
        channel.QueueBind(dlqQueueName, exchangeName, "#");

        return new Dictionary<string, object>()
        {
            { "x-dead-letter-exchange", exchangeName }
        };
    }
}