namespace OpenGW.Proxy
{
    public enum CheckerResult
    {
        Uncertain = 0,
        NotMe,
        IsMe_Done,
        IsMe_InProgress,
        IsMe_Failed,
    }
}