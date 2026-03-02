using System;
namespace Student3;
public interface INotification { void Send(string msg); }

public class EmailNotification : INotification
{
    public void Send(string msg) { }
}

public class NotificationService
{
    public void Process(string msg)
    {
        var n = new EmailNotification(); // no factory
        n.Send(msg);
    }
}