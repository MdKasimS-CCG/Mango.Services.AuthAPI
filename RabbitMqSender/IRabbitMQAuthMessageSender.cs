namespace Mango.Services.AuthAPI.RabbitMqSender
{
    // TODO: Instead create a generic IMessageBus interface. So, that MQ servcie can be interchanged depending upon environment.
    public interface IRabbitMQAuthMessageSender
    {
        Task SendMessage(object message, string queueName);
    }
}
