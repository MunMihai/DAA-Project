using System;

public interface IPayment
{
    void Pay(decimal amount);
}

public class CardPayment : IPayment
{
    public void Pay(decimal amount) { }
}

public abstract class Caine
{
    public abstract IPayment Build();

    public void Execute(decimal amount)
    {
        var p = Build();
        p.Pay(amount);
    }
}

public class CardService : Caine
{
    public override IPayment Build() => new CardPayment();
}