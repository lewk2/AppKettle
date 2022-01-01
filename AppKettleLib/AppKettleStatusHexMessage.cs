
using System;

namespace AppKettle
{
    public class AppKettleStatusHexMessage : AppKettleHexMessage
    {
        public AppKettleStatusHexMessage(AppKettleHexMessage baseMessage)
        {
            HeaderByte = baseMessage.HeaderByte;
            Length = baseMessage.Length;
            Sequence = baseMessage.Sequence;
            Command = baseMessage.Command;
            Checksum = baseMessage.Checksum;
            Data = baseMessage.Data;

            if(Command != KettleCmd.STAT)
            {
                throw new InvalidCastException("Provided base message is not a status command message");
            }

            /*
                0x0F    : 0xc8 for sucess
                0x10    : 0x00 - padding?
                0x11    : status : 0="Not on base", 2="Standby", 3="Ready", 4="Heating", 5="Keep Warm"
                0x12-13 : Number of seconds to "keep warm" - it counts down from 60*mins, where
                          mins is set in the ON message
                0x14    : Current temperature, in Celsius (Hex, so 0x26 = 40C)
                0x15    : Target Temperature, as set in the ON message (Hex, so 0x64 = 100C)
                0x16-17 : Volume, (Hex, so 043b = 1203ml)
                0x18-19 : 0x00 - padding? Unused?
             */

            var msgMemory = new Memory<byte>(Data);
            StatusResult = (KettleStatusResult)Data[12];
            State = (KettleState)Data[14];
            KeepWarmSeconds = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(msgMemory[15..17].Span);
            CurrentTemp = Data[17];
            TargetTemp = Data[18];
            WaterVolumeMl = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(msgMemory[19..21].Span);

        }

        public KettleStatusResult StatusResult;

        public KettleState State;

        public int KeepWarmSeconds;

        public int CurrentTemp;

        public int TargetTemp;

        public int WaterVolumeMl;
    }
}