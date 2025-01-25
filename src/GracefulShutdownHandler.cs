namespace bschttpd;
public class GracefulShutdownHandler
{
    private readonly RotatingW3CLoggingMiddleware _middleware;

    public GracefulShutdownHandler(RotatingW3CLoggingMiddleware middleware)
    {
        _middleware = middleware;
    }

    public void OnShutdown()
    {
        _middleware.Dispose();
    }
}