using CliWrap;
using CliWrap.EventStream;
using LpacFibocomWrapper.ApduDevice;
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

    private static void LoadEnvFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var parts = line.Split(
                '=',
                2,
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                continue;

            Environment.SetEnvironmentVariable(parts[0], parts[1]);
        }
    }

    private static IApduDevice? GetApduDevice()
    {
        var atDevice = Environment.GetEnvironmentVariable("DRIVER_IFID") ?? Environment.GetEnvironmentVariable("AT_DEVICE");

        LoadEnvFile("lpac-kn.env");

        var atKnAddress = Environment.GetEnvironmentVariable("AT_KN_ADDRESS");
        if (!string.IsNullOrWhiteSpace(atKnAddress))
        {
            var atKnLogin = Environment.GetEnvironmentVariable("AT_KN_LOGIN") ?? throw new Exception("AT_KN_LOGIN is empty");
            var atKnPassword = Environment.GetEnvironmentVariable("AT_KN_PASSWORD") ?? throw new Exception("AT_KN_PASSWORD is empty");
            return new ApduAtKnDevice(atDevice!, atKnAddress, atKnLogin, atKnPassword);
        }

        return new ApduAtDevice(atDevice!);
    }

    private static async Task<string> HandleDriverApduList(IApduDevice apduDevice)
    {
        var apduItems = await apduDevice.GetDriverApduList();

        var data = string.Join(",",
            apduItems
                .Select(a => $$"""{"env":"{{a.Env}}","name":"{{a.Name}}"}""")
                .ToArray()
        );

        return $$$"""{"type":"lpa","payload":{"data":[{{{data}}}]}}""";
    }

    private static async Task<string> HandleTypeApdu(IApduDevice apduDevice, string func, string? param)
    {
        const string okResponse = """{"ecode":0}""";
        const string errorResponse = """{"ecode":-1}""";

        if (func == "connect")
        {
            if (await apduDevice.Connect())
            {
                return okResponse;
            }
            return errorResponse;
        }

        if (func == "disconnect")
        {
            if (await apduDevice.Disconnect())
            {
                return okResponse;
            }
            return errorResponse;
        }

        if (func == "logic_channel_open")
        {
            if (param is null)
                return errorResponse;

            var channelId = await apduDevice.LogicChannelOpen(param);
            return $$"""{"ecode":{{channelId}}}""";
        }

        if (func == "logic_channel_close")
        {
            if (await apduDevice.LogicChannelClose())
            {
                return okResponse;
            }
            return errorResponse;
        }

        if (func == "transmit")
        {
            if (param is null)
                return errorResponse;

            var data = await apduDevice.Transmit(param);
            if (data is not null)
            {
                return $$"""{"ecode":0,"data":"{{data}}"}""";
            }
            return errorResponse;
        }

        return errorResponse;
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            using var apduDevice = GetApduDevice();

            if (new[] { "driver", "apdu", "list" }.All(a => args.Contains(a, StringComparer.OrdinalIgnoreCase)))
            {
                var response = await HandleDriverApduList(apduDevice!);
                Console.WriteLine(response);
                return 0;
            }

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
                                var payload = await HandleTypeApdu(apduDevice!, func, param);
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

        return -1;
    }
}
