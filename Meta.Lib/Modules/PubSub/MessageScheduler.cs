using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class MessageScheduler
    {
        readonly Func<IPubSubMessage, Task> _output;

        public MessageScheduler(Func<IPubSubMessage, Task> output)
        {
            _output = output;
        }

        internal async void Schedule<TMessage>(TMessage message, int millisecondsDelay, CancellationToken cancellationToken)
            where TMessage : class, IPubSubMessage
        {
            try
            {
                await Task.Delay(millisecondsDelay, cancellationToken);
                await _output(message);
            }
            catch (Exception)
            {
                // this is async void method...
            }
        }
    }
}
