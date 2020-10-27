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

        public PipeTransmit(IPubSubMessage message, PipeMessageType pipeMessageType)
        {
            Timeout = message.ResponseTimeout;
            var serializedMessage = JsonConvert.SerializeObject(message);
            Packet = $"{(char)pipeMessageType}\t{Id}\t{message.GetType().AssemblyQualifiedName}\t{serializedMessage}";
        }

        public PipeTransmit(string message, PipeMessageType pipeMessageType, int millisecondsTimeout)
        {
            Timeout = millisecondsTimeout;
            Packet = $"{(char)pipeMessageType}\t{Id}\t{message}";
        }
    }
}
