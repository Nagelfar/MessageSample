using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageSample.CommandDriven;

public class Delivery : IDisposable
{
    private readonly ILogger<Delivery> _logger;
    private readonly IModel _model;
    private readonly EventingBasicConsumer _consumer;

    public Delivery(IConnection connection, ILogger<Delivery> logger)
    {
        _logger = logger;
        _model = connection.CreateModel();
        _consumer = new EventingBasicConsumer(_model);
        _consumer.Received += (model, ea) => { OnMessage(ea); };
    }

    private void OnMessage(BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        _logger.LogInformation("Delivering Items for {@Message}", message);
        _model.BasicAck(ea.DeliveryTag, false);
    }

    public void Start()
    {
        _model.BasicConsume(queue: Topology.DeliveryQueue,
            autoAck: false,
            consumer: _consumer);
    }

    public void Dispose()
    {
        _model.Abort();
        _model.Dispose();
    }
}