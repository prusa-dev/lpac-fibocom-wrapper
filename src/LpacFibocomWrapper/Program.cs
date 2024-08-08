using CliWrap;
using CliWrap.EventStream;
using RJCP.IO.Ports;
using System.Text;
using System.Text.Json.Nodes;

namespace LpacFibocomWrapper;

public static class Program
{
    #region InputPipe    
    private static readonly SemaphoreSlim _inputPipeSemaphore = new(0, 1);
    private static readonly StringBuilder _inputBuffer = new();
    private static PipeSource CreateInputPipe()
    {
        return PipeSource.Create(async (destination, cancellationToken) =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _inputPipeSemaphore.WaitAsync(cancellationToken);
                var data = Encoding.UTF8.GetBytes(_inputBuffer.ToString());
                await destination.WriteAsync(data, cancellationToken);
            }
        });
    }
    private static void InputWriteLine(string value)
    {
        _inputBuffer.Clear();
        _inputBuffer.AppendLine(value);
        _inputPipeSemaphore.Release();
    }
    #endregion

    private static async Task<string> HandleDriverApduList()
    {
        await using var portStream = new SerialPortStream();
        var ports = portStream.GetPortDescriptions();

        var data = string.Join(",",
            ports
                .Select(p => $$"""{"env":"{{p.Port}}","name":"{{p.Description}}"}""")
                .ToArray()
        );

        return $$$"""{"type":"lpa","payload":{"data":[{{{data}}}]}}""";
    }


    private static string? _atDevice;
    private static ApduAtDevice? _apduAtDevice;

    private static string HandleTypeApdu(string func, string? param)
    {
        const string okResponse = """{"ecode":0}""";
        const string errorResponse = """{"ecode":-1}""";

        if (func == "connect")
        {
            if (_atDevice is null)
            {
                return errorResponse;
            }

            _apduAtDevice = ApduAtDevice.Create(_atDevice);
            _apduAtDevice.Connect();
            return okResponse;
        }

        if (func == "disconnect")
        {
            _apduAtDevice?.Disconnect();
            return okResponse;
        }

        if (func == "logic_channel_open")
        {
            if (param is null)
                return errorResponse;

            var channelId = _apduAtDevice!.OpenLogicChannel(param);
            return $$"""{"ecode":{{channelId}}}""";
        }

        if (func == "logic_channel_close")
        {
            _apduAtDevice!.CloseLogicChannel();
            return okResponse;
        }

        if (func == "transmit")
        {
            if (param is null)
                return errorResponse;

            if (_apduAtDevice!.TryTransmit(param, out var data))
            {
                return $$"""{"ecode":0,"data":"{{data}}"}""";
            }
        }

        return errorResponse;
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (new[] { "driver", "apdu", "list" }.All(a => args.Contains(a, StringComparer.OrdinalIgnoreCase)))
            {
                var response = await HandleDriverApduList();
                Console.WriteLine(response);
                return 0;
            }

            _atDevice = Environment.GetEnvironmentVariable("DRIVER_IFID") ?? Environment.GetEnvironmentVariable("AT_DEVICE");

            var procInputPipe = CreateInputPipe();

            var cmd = Cli.Wrap("lpac.orig.exe")
                .WithValidation(CommandResultValidation.None)
                .WithArguments(args)
                .WithEnvironmentVariables(env =>
                {
                    env.Set("LPAC_APDU", "stdio");
                })
                .WithStandardInputPipe(procInputPipe);

            await foreach (var cmdEvent in cmd.ListenAsync())
            {
                switch (cmdEvent)
                {
                    case StandardOutputCommandEvent stdOut:
                        if (string.IsNullOrWhiteSpace(stdOut.Text))
                            continue;

                        Console.WriteLine(stdOut.Text);
                        try
                        {
                            var request = JsonNode.Parse(stdOut.Text)!;
                            var requestType = request["type"]!.GetValue<string>();

                            if (requestType == "apdu")
                            {
                                var requestPayload = request["payload"]!;
                                var func = requestPayload["func"]!.GetValue<string>();
                                var param = requestPayload["param"]?.GetValue<string>();
                                var payload = HandleTypeApdu(func, param);
                                var response = $$"""{"type":"apdu","payload":{{payload}}}""";
                                Console.WriteLine(response);
                                InputWriteLine(response);
                            }
                        }
                        catch { }
                        break;
                    case StandardErrorCommandEvent stdErr:
                        Console.Error.WriteLine(stdErr.Text);
                        break;
                    case ExitedCommandEvent exited:
                        return exited.ExitCode;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        finally
        {
            _apduAtDevice?.Dispose();
        }

        return -1;
    }
}
