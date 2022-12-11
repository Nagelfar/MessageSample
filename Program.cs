using MessageSample;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft",LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics",LogEventLevel.Information)
    .WriteTo.Console(outputTemplate:"{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UseSerilog();

if (builder.Configuration.GetValue<bool>("Controllers"))
{
    builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(setup =>
        setup.CustomSchemaIds(x => x.FullName));
}

builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    return factory.CreateConnection();
});

if (builder.Configuration.GetValue<bool>("Consumers"))
{
    Log.Logger.Information("Starting Consumers");
    MessageSample.CommandDriven.Topology.Configure(builder);
    MessageSample.EventDriven.Topology.Configure(builder);
    MessageSample.DocumentDriven.Topology.Configure(builder);
    MessageSample.CommandDrivenPipeline.Topology.Configure(builder);
}
else
{
    Log.Logger.Information("Not starting the Consumers");
}

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

MessageSample.CommandDriven.Topology.DefineTopology(app);
MessageSample.EventDriven.Topology.DefineTopology(app);
MessageSample.DocumentDriven.Topology.DefineTopology(app);
MessageSample.CommandDrivenPipeline.Topology.DefineTopology(app);

app.Run();