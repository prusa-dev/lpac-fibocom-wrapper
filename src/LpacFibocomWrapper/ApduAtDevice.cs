using RJCP.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace LpacFibocomWrapper;
public sealed partial class ApduAtDevice : IDisposable
{
    public static ApduAtDevice Create(string port)
    {
        var portStream = new SerialPortStream(port)
        {
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            ReadBufferSize = 8192,
            WriteBufferSize = 8192,
        };

        return new ApduAtDevice(portStream);
    }

    private SerialPortStream? _portStream;
    private int _logicChannelId = -1;

    private ApduAtDevice(SerialPortStream portStream)
    {
        _portStream = portStream;
    }

    public void Dispose()
    {
        if (_portStream is not null)
        {
            _portStream.Dispose();
            _portStream = null;
        }
    }

    [GeneratedRegex("\r\n(OK|ERROR|\\+CME ERROR|\\+CMS ERROR)")]
    private static partial Regex _endOfResponse();

    private string SendAtCommand(string atCommand)
    {
        if (_portStream is null) throw new ArgumentNullException();
        var data = new StringBuilder();

        _portStream.DiscardInBuffer();
        _portStream.DiscardOutBuffer();

        _portStream.WriteLine(atCommand);
        while (true)
        {
            var intermediateData = _portStream.ReadExisting();
            data.Append(intermediateData);
            if (_endOfResponse().IsMatch(intermediateData))
            {
                break;
            }
        }

        return data.ToString();
    }

    public void Connect()
    {
        _portStream!.Open();
    }

    public void Disconnect()
    {
        _portStream!.Close();
    }

    public int OpenLogicChannel(string param)
    {
        var response = SendAtCommand($"AT+CCHO=\"{param}\"");
        var data = response
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        if (data[^1] == "OK" && int.TryParse(data[^2], out _logicChannelId))
        {
            return _logicChannelId;
        }

        return -1;
    }

    public int CloseLogicChannel()
    {
        var response = SendAtCommand($"AT+CCHC={_logicChannelId}");
        return 0;
    }

    [GeneratedRegex("\\+CGLA:\\s?(?<length>\\d+),(?<data>[\\S]+)")]
    private static partial Regex _cglaRe();

    public bool TryTransmit(string param, out string data)
    {
        data = string.Empty;

        var response = SendAtCommand($"AT+CGLA={_logicChannelId},{param.Length},\"{param}\"");
        var m = _cglaRe().Match(response);
        if (m.Success)
        {
            data = m.Groups["data"].Value.Trim('"');
            return true;
        }

        return false;
    }
}
