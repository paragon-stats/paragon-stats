using System.Text;

using ParagonStats.Core.Parsing;

namespace ParagonStats.Core.Logging;

/// <summary>
/// Incremental reader for a chatlog the game may still be writing: emits only
/// complete lines, restarts from the top when the file is truncated, and
/// reopens when the path stops being the file the open handle points at.
/// Pull-based - the caller owns all timing; no threads in Core.
/// The zero-collection ruling is enforced HERE, before materialization: the
/// verdict is re-asked on every character, so a refused line stops being
/// buffered at the first character that proves it refused (a chat line at its
/// leading bracket - before any sender name arrives) and is never a string.
/// Refused characters are overwritten, and every buffer this class owns - raw
/// bytes, decoded chars, the classification window, the partial line - is
/// scrubbed at the end of each poll (even a failing one) and on Dispose.
/// A memory dump of this process does not carry player communications; what
/// it can carry is stated in <see cref="CollectionPolicy"/>.
/// </summary>
public sealed class ChatLogTailer : IDisposable
{
    /// <summary>Consecutive empty polls with a longer file at the path before the handle is presumed dead.</summary>
    private const int StaleReopenThreshold = 2;

    /// <summary>No real chatlog line approaches this; a longer one is dropped rather than grown into an OOM.</summary>
    private const int MaxLineLength = 64 * 1024;

    private const int ReadSize = 8192;

    /// <summary>
    /// Peak lines materialized per poll, checked once per read chunk (so the
    /// true bound is this plus at most one chunk's worth). A hand-crafted
    /// multi-gigabyte log cannot be turned into one enormous list; the caller
    /// simply polls again, and live watch already does.
    /// </summary>
    private const int MaxLinesPerPoll = 50_000;

    private readonly string _path;

    // GetMaxCharCount covers a pending multi-byte sequence carried by the
    // decoder across reads; sizing chars to the byte count alone overflows
    // when a split character completes at the start of a full chunk.
    private readonly byte[] _bytes = new byte[ReadSize];
    private readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(ReadSize)];

    // A field, not a stackalloc: the classification window is scrubbed with
    // the other buffers instead of being left as residue on the thread stack.
    private readonly char[] _window = new char[CollectionPolicy.MaxClassifyLength];
    private readonly StringBuilder _partial = new();
    private Decoder _decoder = Encoding.UTF8.GetDecoder();
    private FileStream _stream;
    private long _position;
    private CollectionVerdict _verdict = CollectionVerdict.Undecided;
    private bool _lastPollWasEmpty;
    private int _stalePolls;

    public ChatLogTailer(string path)
    {
        _path = path;
        _stream = Open(path);
    }

    /// <summary>
    /// True when the last poll stopped at its line cap with the file still
    /// ahead of the read position. The caller needs this to keep one
    /// account's files in order: a newer file must not be read past an older
    /// one that is still catching up.
    /// </summary>
    public bool HasMore { get; private set; }

    public IReadOnlyList<string> Poll()
    {
        List<string> lines = [];
        try
        {
            ReopenIfDetached();
            _stream.Seek(_position, SeekOrigin.Begin);
            int read;
            bool any = false;
            while ((read = _stream.Read(_bytes, 0, _bytes.Length)) > 0)
            {
                any = true;
                _position += read;
                int decoded = _decoder.GetChars(_bytes, 0, read, _chars, 0);
                for (int i = 0; i < decoded; i++)
                {
                    Accept(_chars[i], lines);
                }

                if (lines.Count >= MaxLinesPerPoll)
                {
                    HasMore = true;
                    break; // resume from _position on the next poll
                }
            }

            HasMore = HasMore && lines.Count >= MaxLinesPerPoll;
            _lastPollWasEmpty = !any;
            if (any)
            {
                _stalePolls = 0;
            }

            return lines;
        }
        finally
        {
            // Even a failing poll leaves no raw log content behind.
            Array.Clear(_bytes);
            Array.Clear(_chars);
            Array.Clear(_window);
        }
    }

    /// <summary>
    /// Batch end-of-file: a final line without a trailing newline is still a
    /// complete line on disk. Never used in live mode, where a newline-less
    /// tail is an in-progress write.
    /// </summary>
    public string? Drain()
    {
        try
        {
            if (_partial.Length == 0 || Classify(complete: true) != CollectionVerdict.Collect)
            {
                Erase();
                return null;
            }

            string line = _partial.ToString();
            _partial.Clear();
            return line;
        }
        finally
        {
            Array.Clear(_window);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        Erase();
        Array.Clear(_bytes);
        Array.Clear(_chars);
        Array.Clear(_window);
    }

    private static FileStream Open(string path) =>

        // ReadWrite|Delete sharing: the running game client keeps today's
        // chatlog open for appending, and that must never block the tailer.
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>
    /// The path may no longer be the file this handle holds: truncation
    /// shrinks it below the read position, while delete-and-recreate leaves
    /// the handle on a dead file whose length is frozen - visible as a longer
    /// file at the path that the handle keeps reporting EOF for. Requiring
    /// the second signal twice keeps an append landing between the two length
    /// queries from ever being mistaken for a new file (which would re-read
    /// and double-count).
    /// </summary>
    private void ReopenIfDetached()
    {
        long streamLength = _stream.Length;
        long pathLength = new FileInfo(_path).Length;

        bool shrank = pathLength < _position || pathLength < streamLength;
        bool detached = false;
        if (!shrank && _lastPollWasEmpty && pathLength > _position)
        {
            detached = ++_stalePolls >= StaleReopenThreshold;
        }

        if (!shrank && !detached)
        {
            return;
        }

        // Open first: if this throws, the existing handle stays usable rather
        // than leaving a disposed stream behind for the next poll to hit.
        FileStream fresh = Open(_path);
        _stream.Dispose();
        _stream = fresh;
        Restart();
    }

    private void Accept(char next, List<string> lines)
    {
        if (next == '\n')
        {
            if (_partial.Length > 0 && Classify(complete: true) == CollectionVerdict.Collect)
            {
                lines.Add(_partial.ToString());
                _partial.Clear();
            }
            else
            {
                Erase();
            }

            _verdict = CollectionVerdict.Undecided;
            return;
        }

        if (_verdict == CollectionVerdict.Refuse)
        {
            return; // the rest of a refused line is never even buffered
        }

        // A byte-order mark leads the file, not a line: drop it so it cannot
        // shift the timestamp offsets and refuse an entire real line.
        if (_partial.Length == 0 && next == '\uFEFF')
        {
            return;
        }

        _partial.Append(next);
        if (_partial.Length > MaxLineLength)
        {
            Erase();
            _verdict = CollectionVerdict.Refuse; // absurd line: drop the remainder
            return;
        }

        if (_verdict == CollectionVerdict.Undecided)
        {
            _verdict = Classify(complete: false);
            if (_verdict == CollectionVerdict.Refuse)
            {
                Erase();
            }
        }
    }

    private CollectionVerdict Classify(bool complete)
    {
        int length = Math.Min(_partial.Length, _window.Length);
        _partial.CopyTo(0, _window, 0, length);
        return CollectionPolicy.Classify(_window.AsSpan(0, length), complete || _partial.Length >= _window.Length);
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
        _verdict = CollectionVerdict.Undecided;
        _lastPollWasEmpty = false;
        _stalePolls = 0;
    }
}
