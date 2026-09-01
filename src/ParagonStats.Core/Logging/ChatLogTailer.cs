using System.Text;

namespace ParagonStats.Core.Logging;

/// <summary>
/// Incremental reader for a chatlog the game may still be writing: emits only
/// complete lines (a partial trailing line waits, with decoder state, for its
/// newline), and a truncated file restarts from the top. Pull-based - the
/// caller owns all timing; no threads in Core.
/// </summary>
public sealed class ChatLogTailer : IDisposable
{
    private readonly FileStream _stream;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _partial = new();
    private long _position;

    public ChatLogTailer(string path)
    {
        // ReadWrite|Delete sharing: the running game client keeps today's
        // chatlog open for appending, and that must never block the tailer.
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    public IReadOnlyList<string> Poll()
    {
        if (_stream.Length < _position)
        {
            // Truncated (file recycled or manually cleared): start over.
            _position = 0;
            _decoder.Reset();
            _partial.Clear();
        }

        List<string> lines = [];
        _stream.Seek(_position, SeekOrigin.Begin);
        byte[] bytes = new byte[8192];
        char[] chars = new char[8192];
        int read;
        while ((read = _stream.Read(bytes, 0, bytes.Length)) > 0)
        {
            _position += read;
            int decoded = _decoder.GetChars(bytes, 0, read, chars, 0);
            for (int i = 0; i < decoded; i++)
            {
                if (chars[i] == '\n')
                {
                    lines.Add(_partial.ToString());
                    _partial.Clear();
                }
                else
                {
                    _partial.Append(chars[i]);
                }
            }
        }

        return lines;
    }

    public void Dispose() => _stream.Dispose();
}
