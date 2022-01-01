
using System;

namespace AppKettle
{
    public class AppKettleInitHexMessage : AppKettleHexMessage
    {
        public AppKettleInitHexMessage(AppKettleHexMessage baseMessage)
        {
            HeaderByte = baseMessage.HeaderByte;
            Length = baseMessage.Length;
            Sequence = baseMessage.Sequence;
            Command = baseMessage.Command;
            Checksum = baseMessage.Checksum;
            Data = baseMessage.Data;

            if (Command != KettleCmd.INIT)
            {
                throw new InvalidCastException("Provided base message is not a INIT command message");
            }
        }
    }
}