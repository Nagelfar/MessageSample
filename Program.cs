using MessageSample;
using RabbitMQ.Client;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Host.UseSerilog();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup =>
    setup.CustomSchemaIds(x => x.FullName));
builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory() { HostName = "localhost" };
    return factory.CreateConnection();
});

if (builder.Configuration.GetValue<bool>("Consumers"))
{
    Log.Logger.Information("Starting Consumers");
    MessageSample.CommandDriven.Topology.Configure(builder);
    MessageSample.CommandDrivenPipeline.Topology.Configure(builder);
}
else
{
    Log.Logger.Information("Not starting the Consumers");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

MessageSample.CommandDriven.Topology.DefineTopology(app);
MessageSample.CommandDrivenPipeline.Topology.DefineTopology(app);

app.Run();