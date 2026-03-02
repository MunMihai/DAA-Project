using System;

public interface IMessageSender
{
    void Send(string text);
}

public class SmsSender : IMessageSender
{
    public void Send(string text) { }
}

public abstract class MessagingService
{
    public abstract IMessageSender CreateSender();

    public void Notify(string text)
    {
        var sender = CreateSender();
        sender.Send(text);
    }
}

public class SmsMessagingService : MessagingService
{
    public override IMessageSender CreateSender() => new SmsSender();
}