using System.Text;

using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Logging;

/// <summary>
/// Incremental reader for a chatlog the game may still be writing: emits only
/// complete lines, a truncated file restarts from the top, and a deleted-
/// then-recreated file is detected by the path length falling below the read
/// position and reopened.
/// Pull-based - the caller owns all timing; no threads in Core.
/// The zero-collection ruling is enforced HERE, before materialization:
/// a line <see cref="CollectionPolicy"/> refuses is discarded from the char
/// buffer without ever becoming a string, at most
/// <see cref="CollectionPolicy.ClassifyLength"/> chars of it are held (then
/// zeroed), and the transient read buffers are scrubbed every poll - a
/// memory dump of this process does not carry player communications.
/// </summary>
public sealed class ChatLogTailer : IDisposable
{
    private readonly string _path;

    // GetMaxCharCount covers a pending multi-byte sequence carried by the
    // decoder across reads; sizing chars to the byte count alone overflows
    // when a split character completes at the start of a full chunk.
    private readonly byte[] _bytes = new byte[8192];
    private readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(8192)];
    private readonly StringBuilder _partial = new();
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private FileStream _stream;
    private long _position;
    private bool _discarding;
    private bool _classified;

    public ChatLogTailer(string path)
    {
        _path = path;
        _stream = Open(path);
    }

    public IReadOnlyList<string> Poll()
    {
        // The file AT THE PATH shrinking below our position means truncation
        // or delete-and-recreate (the old handle would keep reading the dead
        // file, its length frozen). Length-vs-position is deterministic on
        // every platform - creation time is not (Linux ctime moves on write).
        // Recreate-to-equal-or-longer within one poll is the accepted blind
        // spot, as with in-place truncate-then-regrow.
        if (new FileInfo(_path).Length < _position)
        {
            _stream.Dispose();
            _stream = Open(_path);
            Restart();
        }

        List<string> lines = [];
        _stream.Seek(_position, SeekOrigin.Begin);
        int read;
        while ((read = _stream.Read(_bytes, 0, _bytes.Length)) > 0)
        {
            _position += read;
            int decoded = _decoder.GetChars(_bytes, 0, read, _chars, 0);
            for (int i = 0; i < decoded; i++)
            {
                Accept(_chars[i], lines);
            }
        }

        // Scrub the transient buffers: refused content lives at most one poll.
        Array.Clear(_bytes);
        Array.Clear(_chars);
        return lines;
    }

    /// <summary>
    /// Batch end-of-file: a final line without a trailing newline is still a
    /// complete line on disk. Never used in live mode, where a newline-less
    /// tail is an in-progress write.
    /// </summary>
    public string? Drain()
    {
        if (_discarding || _partial.Length == 0 || RefusePartial())
        {
            Erase();
            return null;
        }

        string line = _partial.ToString();
        _partial.Clear();
        return line;
    }

    public void Dispose() => _stream.Dispose();

    private static FileStream Open(string path) =>

        // ReadWrite|Delete sharing: the running game client keeps today's
        // chatlog open for appending, and that must never block the tailer.
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private void Accept(char c, List<string> lines)
    {
        if (c == '\n')
        {
            if (!_discarding && _partial.Length > 0 && !RefusePartial())
            {
                lines.Add(_partial.ToString());
                _partial.Clear();
            }
            else
            {
                Erase();
            }

            _discarding = false;
            _classified = false;
            return;
        }

        if (_discarding)
        {
            return; // the rest of a refused line is never even buffered
        }

        _partial.Append(c);
        if (!_classified && _partial.Length >= CollectionPolicy.ClassifyLength)
        {
            _classified = true;
            if (RefusePartial())
            {
                Erase();
                _discarding = true;
            }
        }
    }

    private bool RefusePartial()
    {
        Span<char> prefix = stackalloc char[CollectionPolicy.ClassifyLength];
        int length = Math.Min(_partial.Length, prefix.Length);
        _partial.CopyTo(0, prefix, length);
        return CollectionPolicy.Refuses(prefix[..length]);
    }

    /// <summary>Overwrite before clearing so refused chars do not linger in the builder's chunks.</summary>
    private void Erase()
    {
        for (int i = 0; i < _partial.Length; i++)
        {
            _partial[i] = '\0';
        }

        _partial.Clear();
    }

    private void Restart()
    {
        _position = 0;
        _decoder = Encoding.UTF8.GetDecoder();
        Erase();
        _discarding = false;
        _classified = false;
    }
}
