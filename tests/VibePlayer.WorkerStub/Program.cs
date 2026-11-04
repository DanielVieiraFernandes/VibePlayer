using System;
using System.Threading;

// Comporta-se como o processo de transcrição nos casos difíceis de provocar no de verdade.
Console.In.ReadLine();
Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

switch (args[0])
{
    case "crash":
        Console.WriteLine("""{"Kind":"Stage","Stage":"Extracting"}""");
        Environment.Exit(3);
        break;

    case "hang":
        Console.WriteLine("""{"Kind":"Progress","Fraction":0.5}""");
        Thread.Sleep(Timeout.Infinite);
        break;

    case "slow-exit":
        Console.WriteLine("""{"Kind":"Done","Value":"pt"}""");
        Thread.Sleep(Timeout.Infinite);
        break;

    case "noise":
        Console.WriteLine("whisper_init: isto não é um evento");
        Console.WriteLine("""{"Kind":"Done","Value":"pt"}""");
        break;
}
