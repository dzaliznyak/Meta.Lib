﻿using Meta.Lib.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meta.Lib.Modules.PubSub
{
    internal class RequestResponseProcessor
    {
        readonly MetaPubSub _hub;

        public RequestResponseProcessor(MetaPubSub hub)
        {
            _hub = hub;
        }

        public async Task<TMessage> When<TMessage>(
            int millisecondsTimeout, 
            Predicate<TMessage> match = null,
            CancellationToken cancellationToken = default)
            where TMessage : class, IPubSubMessage
        {
            var tcs = new TaskCompletionSource<TMessage>();

            Task Handler(TMessage x)
            {
                tcs.TrySetResult(x);
                return Task.CompletedTask;
            }

            _hub.Subscribe(Handler, match);

            try
            {
                return await tcs.Task.TimeoutAfter(millisecondsTimeout, cancellationToken);
            }
            finally
            {
                await _hub.Unsubscribe((Func<TMessage, Task>)Handler);
            }
        }

        public async Task<TResponse> Process<TResponse>(
            IPubSubMessage message, 
            Predicate<TResponse> match = null,
            CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage
        {
            var tcs = new TaskCompletionSource<TResponse>();

            Task Handler(TResponse response)
            {
                tcs.TrySetResult(response);
                return Task.CompletedTask;
            }

            _hub.Subscribe(Handler, match);

            try
            {
                //todo - calculate remaining timeout
                await _hub.Publish(message);
                return await tcs.Task.TimeoutAfter(message.ResponseTimeout, cancellationToken);
            }
            finally
            {
                await _hub.Unsubscribe((Func<TResponse, Task>)Handler);
            }
        }

        public async Task<TResponse> ProcessOnServer<TResponse>(
            IPubSubMessage message,
            Predicate<TResponse> match = null,
            CancellationToken cancellationToken = default)
            where TResponse : class, IPubSubMessage
        {
            var tcs = new TaskCompletionSource<TResponse>();

            Task Handler(TResponse response)
            {
                tcs.TrySetResult(response);
                return Task.CompletedTask;
            }

            await _hub.SubscribeOnServer(Handler, match);

            try
            {
                //todo - calculate remaining timeout
                await _hub.PublishOnServer(message);
                return await tcs.Task.TimeoutAfter(message.ResponseTimeout, cancellationToken);
            }
            finally
            {
                await _hub.Unsubscribe((Func<TResponse, Task>)Handler);
            }
        }

    }
}
