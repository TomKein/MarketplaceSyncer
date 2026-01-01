namespace WorkerService1.BusinessRu.Http;

public sealed class PhpKSort : IComparer<string>
{
    private const int OFFSET = 1000000;

    private static int Value(char c)
    {
        if ('0' <= c && c <= '9')
            return 3000000 + c;

        if ('A' <= c && c <= 'Z')
            return 2000000 + c;

        if ('a' <= c && c <= 'z')
            return 1000000 + c;

        return c switch
        {
            '_' => 1000000 + c + 1000,
            '[' or ']' => 1000000 + c + 2000,
            _ => throw new ArgumentException(
                $"{c} does not have an associated value")
        };
    }

    public int Compare(string? x, string? y)
    {
        if (x == y)
            return 0;

        if (x == null)
            return -1;

        if (y == null)
            return 1;

        for (var i = 0; i < x.Length || i < y.Length; i++)
        {
            if (i >= x.Length)
                return -1;

            if (i >= y.Length)
                return 1;

            var valueX = Value(x[i]);
            var valueY = Value(y[i]);

            if (valueX < valueY)
                return -1;

            if (valueX > valueY)
                return 1;
        }

        return 0;
    }
}
