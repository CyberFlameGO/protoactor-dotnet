// -----------------------------------------------------------------------
// <copyright file="DurableContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto.Cluster.Durable
{
    public class DurableContext
    {
        private readonly DurablePlugin _durable;
        private readonly ClusterIdentity _identity;
        private int _counter;

        public DurableContext(ClusterIdentity identity, DurablePlugin durable)
        {
            _identity = identity;
            _durable = durable;
        }

        public object Message { get; set; }

        public T MessageAs<T>() => (T) Message;

        public Task<T> WaitForExternalEvent<T>() => null;

        public Task CreateTimer() => null;

        public async Task<T> RequestAsync<T>(string identity, string kind, object message)
        {
            //send request to local orchestrator
            //orchestrator saves request to DB

            //await response from orchestrator
            var target = new ClusterIdentity
            {
                Identity = identity,
                Kind = kind
            };

            _counter++;

            var request = new DurableRequest(_identity, target, message, _counter);
            var response = await _durable.RequestAsync(request);
            var m = response.Message;
            return (T) m;
        }

        public async Task PersistFunctionAsync(IContext context)
        {
            _counter = 0;
            Message = context.Message!;

            //save activation
            await _durable.PersistFunctionStartAsync(_identity, Message);

            //ack back to sender
            context.Respond(new DurableFunctionStarted()); //this should be a real message like "FunctionStarted" or something
        }
    }
}