using RabbitMQ.Client;
using Timer = System.Timers.Timer;

namespace MessageSample.Saga;

public interface IStartSaga<T> : IHandleMessageEnvelope<T> where T : notnull
{
}

public interface IHandleSaga<T> : IHandleMessageEnvelope<T> where T : notnull
{
}

public interface IHandleTimeout<T> where T : notnull
{
    void Timeout(Envelope<T> message);
}

public class SagaHost
{
    private readonly IConnection _connection;

    public SagaHost(IConnection connection)
    {
        _connection = connection;
    }

    public SagaUtil<TState> Find<TState>(object target)
    {
        var model = _connection.CreateModel();
        var sagaQueue = typeof(TState).Name;
        // model.QueueDeclare(sagaQueue,true,true,false,)
        return new SagaUtil<TState>(target, _connection);
    }
}

public class SagaUtil<TState>
{
    private readonly object _target;
    private readonly IModel _model;

    private List<Timer> timers = new List<Timer>();

    public SagaUtil(object target, IConnection connection)
    {
        _target = target;
        _model = connection.CreateModel();
    }

    public void Send(string queue, IEnumerable<Envelope> envelopes)
    {
        _model.Send(queue, envelopes);
    }

    public void Timeout(Envelope message, TimeSpan when)
    {
        var timer = new Timer(when)
        {
            Enabled = true,
            AutoReset = false
        };
        timer.Elapsed += (sender, args) =>
        {
            timers.Remove(timer);
            timer.Stop();
        };
        timers.Add(timer);
        timer.Start();
    }
}

public class TableServiceState
{
}

public class DidFoodPreparationFinish
{
}

public class TableServiceSaga :
    IStartSaga<PrepareOrder>,
    IHandleTimeout<DidFoodPreparationFinish>
{
    private readonly SagaUtil<TableServiceState> _sagaUtil;

    public TableServiceSaga(SagaHost sagaHost)
    {
        _sagaUtil = sagaHost.Find<TableServiceState>(this);
    }

    public void Message(Envelope<PrepareOrder> message)
    {
        var foodCommands =
            message.Body.Food
                .Select(x => new CookFood { Food = x, Order = message.Body.Order })
                .Select(x => Envelope.Create(x, message.Metadata));
        _sagaUtil.Send(Topology.FoodPreparationQueue, foodCommands);
        _sagaUtil.Timeout(
            message.CorrelateWith(
                new DidFoodPreparationFinish
                {
                }),
            TimeSpan.FromSeconds(10.0)
        );
    }

    public void Timeout(Envelope<DidFoodPreparationFinish> message)
    {
        throw new NotImplementedException();
    }
}