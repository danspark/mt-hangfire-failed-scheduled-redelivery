using DelayedRedeliveryRepro;
using Hangfire;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<Publisher>();

builder.Services.AddMassTransit(mt =>
{
    mt.AddHangfire(config => config.UseRedisStorage("localhost:6379"));
    
    mt.AddHangfireConsumers();

    mt.AddConsumer<AlwaysFailConsumer>();

    mt.UsingRabbitMq((context, rmq) =>
    {
        rmq.Host("localhost");

        rmq.ConfigureEndpoints(context);
        
        rmq.UseScheduledRedelivery(retry =>
        {
            retry.Handle<Exception>();
            retry.Intervals(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        });
    });
});

var app = builder.Build();

app.Run();

public class Publisher : BackgroundService
{
    private readonly IBus _bus;

    public Publisher(IBus bus)
    {
        _bus = bus;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _bus.Publish(new Message(Guid.NewGuid()), stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public class AlwaysFailConsumer : IConsumer<Message>
{
    public Task Consume(ConsumeContext<Message> context)
    {
        throw new Exception("This will not be rescheduled");
    }
}