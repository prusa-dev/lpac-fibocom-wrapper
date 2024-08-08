namespace LpacFibocomWrapper.ApduDevice;

public record ApduItem(string Env, string Name);

public interface IApduDevice : IDisposable
{
    Task<IEnumerable<ApduItem>> GetDriverApduList();
    Task<bool> Connect();
    Task<bool> Disconnect();

    Task<int> LogicChannelOpen(string param);
    Task<bool> LogicChannelClose();

    Task<string?> Transmit(string param);
}
