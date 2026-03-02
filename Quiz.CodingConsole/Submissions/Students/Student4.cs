using System;

public interface IProduct { }

public class ConcreteProduct : IProduct { }

public abstract class Creator
{
    public abstract IProduct Create();

    public void Use()
    {
        var p = Create();
    }
}