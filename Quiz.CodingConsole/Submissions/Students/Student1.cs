using System;

public interface INotification
{
    void Send(string msg);
}

public class EmailNotification : INotification
{
    public void Send(string msg) { }
}

public abstract class NotificationService
{
    public abstract INotification Create();

    public void Process(string msg)
    {
        var n = Create();
        n.Send(msg);
    }
}

public class EmailService : NotificationService
{
    public override INotification Create() => new EmailNotification();
}