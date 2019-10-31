using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Dictionary<IPEndPoint, int> test = new Dictionary<IPEndPoint, int>() ;

        IPEndPoint a = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 77);
        IPEndPoint b = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 77);

        test.Add(a, 19);
        test.Add(b, 20);

        Console.WriteLine(test.Count);
    }
}