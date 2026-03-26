using Microsoft.Extensions.Logging;

namespace TechnoartSDK.HTTP;

/// <summary>
/// HTTP logging handler safe for all response types, including SSE and NDJSON streaming.
/// Logs the request, response status, and full response body for every call.
/// A <see cref="TeeStream"/> captures bytes as the real consumer reads them, then logs
/// the collected body when the stream is disposed — so the response stream is never
/// pre-read or buffered before the caller receives it.
/// </summary>
public class StreamSafeLoggingHandler(ILogger<StreamSafeLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        logger.LogInformation("HTTP Request: {Method} {Uri}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, cancellationToken);

        logger.LogInformation("HTTP Response: {StatusCode}", (int)response.StatusCode);

        // Wrap the response body in a TeeStream so the consumer reads normally while bytes
        // are captured in parallel. The full body is logged when the stream is disposed.
        var originalContent = response.Content;
        var originalStream = await originalContent.ReadAsStreamAsync(cancellationToken);

        var teeStream = new TeeStream(originalStream,
            body => logger.LogInformation("Response Body: {Body}", body));

        var newContent = new StreamContent(teeStream);
        // Copy all content headers (Content-Type, Content-Length, etc.) to the replacement content
        foreach (var header in originalContent.Headers)
            newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);

        response.Content = newContent;
        return response;
    }

    /// <summary>
    /// Pass-through stream that tees every read into an internal buffer.
    /// When disposed, hands the full captured content to <paramref name="onDisposed"/>.
    /// </summary>
    private sealed class TeeStream(Stream inner, Action<string> onDisposed) : Stream
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
}
