﻿namespace FubuTransportation.Runtime
{
    public class Receiver : IReceiver
    {
        private readonly IMessageInvoker _messageInvoker;

        public Receiver(IMessageInvoker messageInvoker)
        {
            _messageInvoker = messageInvoker;
        }

        public void Receive(IChannel channel, Envelope envelope)
        {
            _messageInvoker.Invoke(envelope);
        }
    }
}