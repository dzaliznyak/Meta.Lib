using Meta.Lib.Messages;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSubPipe;
using System;
using System.Threading.Tasks;

namespace TestClient;

internal class Client : IAsyncDisposable
{
    MetaPubSub _hub;
    PubSubPipeClient _pipe;
    static int pingId;

    public Client()
    {
    }

    public async ValueTask DisposeAsync()
    {
        await _pipe.UnsubscribeOnServer<PingResponse>();
        _hub.Unsubscribe<PingResponse>(OnPingResponse);
        _pipe.DisconnectFromServer();
        _pipe.Dispose();
    }

    public async Task Init()
    {
        try
        {
            _hub = new MetaPubSub();

            // connecting the remote server
            _pipe = new PubSubPipeClient("Meta", _hub);
            await _pipe.ConnectToServer();
            await _pipe.SubscribeOnServer<PingResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    static Task OnPingResponse(PingResponse response)
    {
        Console.WriteLine($"Client >>>: ping response {response.Id} {DateTime.Now:HH:mm:ss.fff}");
        return Task.CompletedTask;
    }

    public async Task Ping()
    {
        try
        {
            await _pipe.PublishOnServer(new PingCommand() { Id = pingId++.ToString() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PublishOnServer error: {ex.Message}");
        }
    }

    internal async Task Disconnect()
    {
        await _pipe.UnsubscribeOnServer<PingResponse>();
        _pipe.DisconnectFromServer();
    }
}
