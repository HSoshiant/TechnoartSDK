namespace TechnoartSDK.HTTP;

/// <summary>
/// Pass-through <see cref="Stream"/> that tees every read into an internal buffer.
/// The consumer receives bytes normally (zero extra latency); when the stream is
/// disposed the full accumulated content is handed to <paramref name="onDisposed"/>
/// as a UTF-8 string. Used by <see cref="StreamSafeLoggingHandler"/> to log SSE
/// and NDJSON response bodies without pre-reading or buffering them up front.
/// </summary>
public sealed class TeeStream(Stream inner, Action<string> onDisposed) : Stream
{
    private readonly MemoryStream _buffer = new();

    public override bool CanRead  => inner.CanRead;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        if (n > 0) _buffer.Write(buffer, offset, n);
        return n;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var n = await inner.ReadAsync(buffer, offset, count, ct);
        if (n > 0) _buffer.Write(buffer, offset, n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await inner.ReadAsync(buffer, ct);
        if (n > 0) _buffer.Write(buffer.Span[..n]);
        return n;
    }

    public override void Flush() => inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value)                 => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            onDisposed(System.Text.Encoding.UTF8.GetString(_buffer.ToArray()));
            inner.Dispose();
            _buffer.Dispose();
        }
        base.Dispose(disposing);
    }
}
