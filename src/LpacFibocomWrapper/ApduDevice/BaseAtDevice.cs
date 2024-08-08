using System.Text.RegularExpressions;

namespace LpacFibocomWrapper.ApduDevice;
public abstract partial class BaseAtDevice : IApduDevice
{
    [GeneratedRegex("\\+CGLA:\\s?(?<length>\\d+),(?<data>[\\S]+)")]
    protected static partial Regex RegExCgla();

    protected int LogicChannelId = -1;

    protected abstract Task<string[]> SendAtCommand(string atCommand);

    public abstract void Dispose();

    public abstract Task<IEnumerable<ApduItem>> GetDriverApduList();

    public abstract Task<bool> Connect();

    public abstract Task<bool> Disconnect();

    public async Task<int> LogicChannelOpen(string param)
    {
        var lines = await SendAtCommand($"AT+CCHO=\"{param}\"");
        if (lines.Length > 0 && lines[^1] == "OK" && int.TryParse(lines[^2], out LogicChannelId))
        {
            return LogicChannelId;
        }

        return -1;
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
