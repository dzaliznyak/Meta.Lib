using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class PipeTransmit
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        public TaskCompletionSource<bool> Tcs { get; }
            = new TaskCompletionSource<bool>();

        public string Packet { get; }
        public int Timeout { get; }

        public PipeTransmit(IPubSubMessage message, int millisecondsTimeout = 5_000)
        {
            Timeout = millisecondsTimeout;
            var serializedMessage = JsonConvert.SerializeObject(message);
            Packet = $"m\t{Id}\t{message.GetType().AssemblyQualifiedName}\t{serializedMessage}";
        }

    }
}
