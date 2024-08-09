using System.Text.RegularExpressions;

namespace LpacFibocomWrapper.ApduDevice;
public abstract partial class BaseAtDevice : IApduDevice
{
    [GeneratedRegex("\\+CGLA:\\s?(?<length>\\d+),(?<data>[\\S]+)")]
    protected static partial Regex RegExCgla();

    [GeneratedRegex("\\+CCHO:\\s?(?<channelId>\\d+)")]
    protected static partial Regex RegExCcho();

    protected int LogicChannelId = -1;

    protected abstract Task<string[]> SendAtCommand(string atCommand);

    public void Dispose()
    {
        Dispose(true);
    }

    protected abstract void Dispose(bool disposing);


    public abstract Task<IEnumerable<ApduItem>> GetDriverApduList();

    public abstract Task<bool> Connect();

    public abstract Task<bool> Disconnect();

    public async Task<int> LogicChannelOpen(string param)
    {
        var lines = await SendAtCommand($"AT+CCHO=\"{param}\"");

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];

            var m = RegExCcho().Match(line);
            if (m.Success && int.TryParse(m.Groups["channelId"].Value, out LogicChannelId))
            {
                return LogicChannelId;
            }

            if (line == "OK" && lineIndex > 0 && int.TryParse(lines[lineIndex - 1], out LogicChannelId))
            {
                return LogicChannelId;
            }
        }

        LogicChannelId = -1;

        return LogicChannelId;
    }

    public async Task<bool> LogicChannelClose()
    {
        await SendAtCommand($"AT+CCHC={LogicChannelId}");
        return true;
    }

    public async Task<string?> Transmit(string param)
    {
        var lines = await SendAtCommand($"AT+CGLA={LogicChannelId},{param.Length},\"{param}\"");
        foreach (var line in lines)
        {
            var m = RegExCgla().Match(line);
            if (m.Success)
            {
                return m.Groups["data"].Value.Trim('"');
            }
        }

        return null;
    }
}
