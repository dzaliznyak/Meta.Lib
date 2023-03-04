using Meta.Lib.Exceptions;
using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Meta.Lib.Modules.PubSubPipe
{
    internal class RemoteSystemMessage
    {
        public List<string> ErrorMessages { get; set; }
        public bool IsTimeoutException { get; set; }
        public bool IsNoSubscribersException { get; set; }

        public RemoteSystemMessage()
        {
        }

        public RemoteSystemMessage(Exception exception)
        {
            ErrorMessages = new List<string>();

            if (exception is TimeoutException)
            {
                IsTimeoutException = true;
                ErrorMessages.Add(exception.Message);
            }
            else if (exception is NoSubscribersException)
            {
                IsNoSubscribersException = true;
                ErrorMessages.Add(exception.Message);
            }
            else
            {
                AddMessages(exception);
            }
        }

        internal void ThrowIfError()
        {
            if (ErrorMessages == null)
                return;

            if (IsTimeoutException && ErrorMessages.Count == 1)
            {
                throw new TimeoutException();
            }
            else if (IsNoSubscribersException && ErrorMessages.Count == 1)
            {
                throw new NoSubscribersException(ErrorMessages.First());
            }
            else if (ErrorMessages.Any())
            {
                throw new PubSubPipeException(string.Join(Environment.NewLine, ErrorMessages));
            }
        }

        void AddMessages(Exception exception)
        {
            if (exception is AggregateException aggEx)
            {
                foreach (var innerEx in aggEx.InnerExceptions)
                {
                    AddMessages(innerEx);
                }
            }
            else
            {
                ErrorMessages.Add(exception.Message);

                if (exception.InnerException != null)
                {
                    AddMessages(exception.InnerException);
                }
            }
        }
    }

    internal class RemoteSubscribeRequest : RemoteSystemMessage
    {
        public string TypeName { get; set; }

        public RemoteSubscribeRequest()
        {
        }

        public RemoteSubscribeRequest(Type type)
        {
            TypeName = type.AssemblyQualifiedName;
        }
    }

    internal class RemoteSubscribeResponse : RemoteSystemMessage
    {
        public RemoteSubscribeResponse()
        {
        }

        public RemoteSubscribeResponse(Exception exception)
            : base(exception)
        {
        }
    }

    internal class RemoteUnsubscribeRequest : RemoteSystemMessage
    {
        public string TypeName { get; set; }

        public RemoteUnsubscribeRequest()
        {
        }

        public RemoteUnsubscribeRequest(Type type)
        {
            TypeName = type.AssemblyQualifiedName;
        }
    }

    internal class RemoteUnsubscribeResponse : RemoteSystemMessage
    {
        public RemoteUnsubscribeResponse()
        {
        }

        public RemoteUnsubscribeResponse(Exception exception)
            : base(exception)
        {
        }
    }

    internal class RemotePublishRequest : RemoteSystemMessage
    {
        public PubSubOptions Options { get; set; }
        public string TypeName { get; set; }
        public string MessageJson { get; set; }

        public RemotePublishRequest()
        {
        }

        public RemotePublishRequest(object message, PubSubOptions options)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Options = options;
            TypeName = message.GetType().AssemblyQualifiedName;

            MessageJson = JsonSerializer.Serialize(message, message.GetType());
        }

        public RemotePublishRequest(Exception exception)
            : base(exception)
        {
        }

        internal object GetMessage()
        {
            var message = JsonSerializer.Deserialize(MessageJson, Type.GetType(TypeName));
            return message;
        }
    }

    internal class RemotePublishResponse : RemoteSystemMessage
    {
        public RemotePublishResponse()
        {
        }

        public RemotePublishResponse(Exception exception)
            : base(exception)
        {
        }
    }



    internal class RemoteProcessRequest : RemoteSystemMessage
    {
        public PubSubOptions Options { get; set; }
        public string TypeName { get; set; }
        public string ResponseTypeName { get; set; }
        public string MessageJson { get; set; }
        public int Timeout { get; set; }

        public RemoteProcessRequest()
        {
        }

        public RemoteProcessRequest(object message, Type responseType, int timeout, PubSubOptions options)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            Timeout = timeout;
            Options = options;
            TypeName = message.GetType().AssemblyQualifiedName;
            ResponseTypeName = responseType.AssemblyQualifiedName;

            MessageJson = JsonSerializer.Serialize(message, message.GetType());
        }

        public RemoteProcessRequest(Exception exception)
            : base(exception)
        {
        }

        internal object GetMessage()
        {
            var message = JsonSerializer.Deserialize(MessageJson, Type.GetType(TypeName));
            return message;
        }
    }

    internal class RemoteProcessResponse : RemoteSystemMessage
    {
        public string ResultJson { get; set; }

        public RemoteProcessResponse()
        {
        }

        public RemoteProcessResponse(object result)
        {
            ResultJson = JsonSerializer.Serialize(result, result.GetType());
        }

        public RemoteProcessResponse(Exception exception)
            : base(exception)
        {
        }

        internal TResult GetResult<TResult>()
        {
            var result = JsonSerializer.Deserialize<TResult>(ResultJson);
            return result;
        }
    }

}
