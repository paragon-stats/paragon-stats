using ParagonStats.Core.Logging;
using ParagonStats.Core.Parsing;
using ParagonStats.Core.Sessions;
using ParagonStats.Core.Stats;

namespace ParagonStats.Core.Tests;

/// <summary>
/// The zero-collection ruling, attacked the way the delta review attacked it:
/// every shape below was a verified bypass of the 45-char window or the
/// untrimmed parser gate, reaching event, capture, and count.
/// </summary>
public sealed class CollectionPolicyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ps-policy-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]

    // Leading-whitespace bypasses: one space defeated the old 45-char window,
    // and the parser's metadata checks were not trimmed at all.
    [InlineData("2026-09-01 12:00:00  Using global chat handle @Handle")]
    [InlineData("2026-09-01 12:00:00           Joined channel 'ChannelA'")]
    [InlineData("2026-09-01 12:00:00 \tLeft channel 'ChannelA'")]
    [InlineData("2026-09-01 12:00:00 \t[Tell] Someone: private words")]
    [InlineData("2026-09-01 12:00:00  [Tell] Someone: private words")]
    [InlineData("2026-09-01 12:00:00                            [Tell] Someone: private words")]

    // The plain shapes, still refused.
    [InlineData("2026-09-01 12:00:00 [Tell] Someone: private words")]
    [InlineData("2026-09-01 12:00:00 Using global chat handle @Handle")]

    // No timestamp: a continuation line, communication content by definition.
    [InlineData("| a continuation line of the message of the day")]

    // Undecidable to the end (all whitespace, or a truncated refused literal)
    // fails closed rather than collecting.
    [InlineData("2026-09-01 12:00:00                                        ")]
    [InlineData("2026-09-01 12:00:00 Joined channel")]
    public void Refused_shapes_never_reach_a_session(string raw)
    {
        Assert.True(CollectionPolicy.Refuses(raw));

        SessionTracker tracker = new();
        tracker.Accept("acct", "2026-09-01 11:00:00 Welcome to City of Heroes, Nova!");
        Assert.False(tracker.Accept("acct", raw)); // no event

        CharacterSession session = Assert.Single(tracker.Sessions);
        Assert.Equal(1, session.Messages.TotalCaptured); // the banner alone: no capture, no count
        Assert.DoesNotContain(session.Messages.Messages, m => m.Payload.Contains("Someone", StringComparison.Ordinal));
        Assert.DoesNotContain(session.Messages.Messages, m => m.Payload.Contains("@Handle", StringComparison.Ordinal));
        Assert.DoesNotContain(session.Messages.Messages, m => m.Payload.Contains("Channel", StringComparison.Ordinal));
    }

    [Theory]

    // Confusable and markup openers: TrimStart/StartsWith are ASCII-only, so
    // anything that is not a letter or digit opens a refused line.
    [InlineData("2026-09-01 12:00:00 ［Tell］ Someone: private words")]
    [InlineData("2026-09-01 12:00:00 <Tell> Someone: private words")]
    [InlineData("2026-09-01 12:00:00 (Tell) Someone: private words")]
    [InlineData("2026-09-01 12:00:00 <a href='cmd:gmotd'><b>Click Here</b></a> or type /gmotd")]
    public void Non_alphanumeric_openers_are_refused(string raw)
    {
        Assert.True(CollectionPolicy.Refuses(raw));
    }

    [Theory]
    [InlineData("2026-09-01 12:00:00 You gain 10 experience.")]
    [InlineData("2026-09-01 12:00:00 Welcome to City of Heroes, Nova!")]
    [InlineData("2026-09-01 12:00:00 Left the mission map.")] // shares a prefix start with "Left channel "
    [InlineData("2026-09-01 12:00:00 Joined a team.")]
    [InlineData("2026-09-01 12:00:00 Using Hasten now.")]
    [InlineData("2026-09-01 12:00:00 Ñova HITS you! Health power was autohit.")] // non-ASCII name, still data
    [InlineData("2026-09-01 12:00:00 42nd Street Thug has defeated Nova")]
    public void Data_lines_are_still_collected(string raw)
    {
        Assert.False(CollectionPolicy.Refuses(raw));
    }

    [Fact]
    public void A_refused_line_is_refused_before_its_sender_arrives()
    {
        // The reader decides at the bracket - character 21 - so an in-progress
        // chat line never buffers the channel, sender, or any message text.
        string path = Path.Join(_root, "chatlog 2026-09-01.txt");
        using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using ChatLogTailer tailer = new(path);

        writer.Write(System.Text.Encoding.UTF8.GetBytes("2026-09-01 12:00:00 [Tell] Bob->Alice: meet "));
        writer.Flush();
        Assert.Empty(tailer.Poll());

        writer.Write(System.Text.Encoding.UTF8.GetBytes("me at the base\n2026-09-01 12:00:05 You gain 10 experience.\n"));
        writer.Flush();
        Assert.Equal(["2026-09-01 12:00:05 You gain 10 experience."], tailer.Poll());
    }

    [Fact]
    public void The_message_log_refuses_communication_payloads_even_if_handed_one()
    {
        // Defense in depth: the only lifetime-retaining store gates too.
        MessageLog log = new();
        log.Add(new DateTime(2026, 9, 1, 12, 0, 0), EventCategory.Uncategorized, "[Tell] Someone: private words");
        log.Add(new DateTime(2026, 9, 1, 12, 0, 1), EventCategory.Uncategorized, " Using global chat handle @Handle");
        Assert.Empty(log.Messages);
        Assert.Equal(0, log.TotalCaptured);

        log.Add(new DateTime(2026, 9, 1, 12, 0, 2), EventCategory.Reward, "You gain 10 experience.");
        Assert.Single(log.Messages);
    }

    [Fact]
    public void A_line_without_an_end_is_dropped_rather_than_grown_forever()
    {
        // A data-shaped line with no newline must not grow the buffer without
        // bound; past the cap the remainder is discarded.
        string path = Path.Join(_root, "chatlog 2026-09-02.txt");
        File.WriteAllText(path, "2026-09-01 12:00:00 " + new string('x', 200_000) + "\n2026-09-01 12:00:05 You gain 10 experience.\n");

        using ChatLogTailer tailer = new(path);
        Assert.Equal(["2026-09-01 12:00:05 You gain 10 experience."], tailer.Poll());
    }

    [Fact]
    public void A_byte_order_mark_does_not_swallow_the_first_line()
    {
        // The StreamReader that used to consume the BOM is gone; without
        // explicit handling the mark shifted every offset and refused line 1.
        string path = Path.Join(_root, "chatlog 2026-09-03.txt");
        File.WriteAllText(path, "2026-09-01 12:00:00 Welcome to City of Heroes, Nova!\n2026-09-01 12:00:05 You gain 10 experience.\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        ReplayResult result = LogReplayer.Replay([path]);
        CharacterSession session = Assert.Single(result.Sessions);
        Assert.Equal("Nova", session.Character);
        Assert.Equal(10, session.Stats.Experience);
    }
}
