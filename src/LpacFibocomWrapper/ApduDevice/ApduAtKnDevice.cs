using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace LpacFibocomWrapper.ApduDevice;
public class ApduAtKnDevice : BaseAtDevice
{
    private readonly HttpClient _httpClient;
    private readonly string? _atDevice;
    private readonly string _login;
    private readonly string _password;

    public ApduAtKnDevice(string? atDevice, string address, string login, string password)
    {
        var cookieContainer = new CookieContainer();
        var httpClientHandler = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
        _httpClient = new HttpClient(httpClientHandler, true);
        _httpClient.BaseAddress = new Uri(address);
        _atDevice = atDevice;
        _login = login;
        _password = password;
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<HttpResponseMessage> KeeneticRequest(string requestUri, string? json = null)
    {
        if (json is null)
        {
            return await _httpClient.GetAsync(requestUri);
        }

        return await _httpClient.PostAsync(requestUri, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private async Task<bool> KeeneticAuth()
    {
        var response = await KeeneticRequest("auth");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var realm = response.Headers.GetValues("X-NDM-Realm").FirstOrDefault();
            var md5 = MD5.HashData(Encoding.UTF8.GetBytes($"{_login}:{realm}:{_password}"));
            var md5HexDigest = string.Concat(md5.Select(x => x.ToString("x2")));

            var challenge = response.Headers.GetValues("X-NDM-Challenge").FirstOrDefault();
            var sha = SHA256.HashData(Encoding.UTF8.GetBytes(challenge + md5HexDigest));
            var shaHexDigest = string.Concat(sha.Select(x => x.ToString("x2")));

            response = await KeeneticRequest("auth", $$"""{"login":"{{_login}}","password":"{{shaHexDigest}}"}""");
        }

        return response.StatusCode == HttpStatusCode.OK;
    }

    public override async Task<IEnumerable<ApduItem>> GetDriverApduList()
    {
        if (await KeeneticAuth())
        {
            var response = await KeeneticRequest("rci/show/interface");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
                return data
                    .Where(x => (string)x.Value!["type"]! == "UsbLte")
                    .Select(x => new ApduItem(x.Key, (string)x.Value!["description"]!))
                    .ToList();
            }
        }
        return [];
    }

    public override async Task<bool> Connect()
    {
        return await KeeneticAuth();
    }

    public override Task<bool> Disconnect()
    {
        return Task.FromResult(true);
    }

    protected override async Task<string[]> SendAtCommand(string atCommand)
    {
        if (await KeeneticAuth())
        {
            atCommand = atCommand.Replace("\"", "\\\"");
            var response = await KeeneticRequest($"rci/interface/{_atDevice}/tty/send", $$"""{"command":"{{atCommand}}"}""");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

                var ttyOut = data["tty-out"];
                if (ttyOut is not null)
                {
                    return ttyOut
                            .AsArray()
                            .Select(x => (string)x!)
                            .ToArray();
                }
            }
        }

        return [];
    }
}
