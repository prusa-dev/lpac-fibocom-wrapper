using RJCP.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace LpacFibocomWrapper.ApduDevice;
public partial class ApduAtDevice : BaseAtDevice
{
    private SerialPortStream? _portStream;

    public ApduAtDevice(string? atDevice)
    {
        if (atDevice is null)
        {
            return;
        }

        _portStream = new SerialPortStream(atDevice)
        {
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            ReadBufferSize = 8192,
            WriteBufferSize = 8192,
            ReadTimeout = 10000,
            WriteTimeout = 1000,
        };
    }

    [GeneratedRegex("\r\n(OK|ERROR|\\+CME ERROR|\\+CMS ERROR)")]
    protected static partial Regex RegExEndOfResponse();

    protected override Task<string[]> SendAtCommand(string atCommand)
    {
        if (_portStream is null)
        {
            throw new NullReferenceException();
        }

        var data = new StringBuilder();

        _portStream.DiscardInBuffer();
        _portStream.DiscardOutBuffer();

        _portStream.WriteLine(atCommand);
        while (true)
        {
            var intermediateData = _portStream.ReadExisting();
            data.Append(intermediateData);
            if (RegExEndOfResponse().IsMatch(intermediateData))
            {
                break;
            }
        }
        var lines = data.ToString()
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        return Task.FromResult(lines);
    }

    protected override void Dispose(bool disposing)
    {
        if (_portStream is not null)
        {
            _portStream.Dispose();
            _portStream = null;
        }
    }

    public override async Task<IEnumerable<ApduItem>> GetDriverApduList()
    {
        await using var portStream = new SerialPortStream();
        var ports = portStream.GetPortDescriptions();
        return ports
                .Select(p => new ApduItem(p.Port, p.Description))
                .ToList();
    }

    public override Task<bool> Connect()
    {
        if (_portStream is null)
        {
            throw new NullReferenceException();
        }

        var port = _portStream.PortName;

        if (!_portStream.GetPortNames()
            .Contains(port, StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception($"Serial Port {port} not found");
        }

        _portStream.Open();
        return Task.FromResult(true);
    }

    public override Task<bool> Disconnect()
    {
        _portStream?.Close();
        return Task.FromResult(true);
    }
}
