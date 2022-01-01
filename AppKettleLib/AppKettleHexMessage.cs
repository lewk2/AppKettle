
using System;

namespace AppKettle
{

    public class AppKettleHexMessage : IAppKettleHexMessage
    {
        public byte HeaderByte { get; set; }

        public ushort Length { get; set; }

        public byte Sequence { get; set; }

        public KettleCmd Command { get; set; }

        public byte[] Data { get; set; }

        public byte Checksum { get; set; }

        public static byte CalculateChecksum(string message, bool includesSum = false)
        {
            byte[] msgBytes;

            if (includesSum)
            {
                msgBytes = Convert.FromHexString(message)[1..^1];
            }
            else
            {
                msgBytes = Convert.FromHexString(message)[1..];
            }

            var sum = 0x0;
            for (var i = 0; i < msgBytes.Length; i++)
            {
                sum += msgBytes[i];
            }

            var checksum = (byte)(0xFF - (sum % 256));

            return checksum;
        }

    }

    public struct AppKettleStatus
    {

        public AppKettleStatus(byte[] data)
        {

        }

    
    }

    public enum KettleCmd
    {
        STAT = 0x36,
        K_ON = 0x39,
        K_OFF = 0x3A,
        WAKE = 0x41,
        TIM1 = 0x43,
        TIM2 = 0x44,
        INIT = 0xa4
    }

    public enum KettleStatusResult
    {
        OK = 0xc8
    }

    public enum KettleState
    {
        NotOnBase = 0,
        Unknown,
        Standby,
        Ready,
        Heating,
        KeepWarm
    }
}