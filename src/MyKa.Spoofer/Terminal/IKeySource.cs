namespace MyKa.Spoofer.Terminal;

/// <summary>Keyboard input that can be polled, so animated screens keep drawing while they wait for a key.</summary>
public interface IKeySource
{
    bool Available { get; }

    ConsoleKeyInfo Read();
}

/// <summary>The real console keyboard. Only valid when input is not redirected.</summary>
public sealed class ConsoleKeys : IKeySource
{
    public bool Available => Console.KeyAvailable;

    public ConsoleKeyInfo Read() => Console.ReadKey(intercept: true);
}
