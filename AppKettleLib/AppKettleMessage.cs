
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AppKettle
{
    public static class AppKettleMessageFactory
    {
        public static IAppKettleMessage GetMessage(string message, bool encrypted = false)
        {
            var msgBytes = Convert.FromHexString(message);
            if (encrypted)
            {
                var encryptedBytes = Encoding.ASCII.GetBytes(message)[0..];
                var decryptedChars = DecryptStringFromBytes_Aes(encryptedBytes);
                msgBytes = Convert.FromHexString(new string(decryptedChars));
            }

            var msgMemory = new Memory<byte>(msgBytes);

            var msg = new AppKettleMessage()
            {
                HeaderByte = msgBytes[0],
                Length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(msgMemory[1..3].Span),
                Sequence = msgBytes[11],
                Command = (KettleCmd)msgBytes[12],
                Checksum = msgBytes[^1],
            };

            msg.Data = msgMemory[3..(3 + msg.Length)].ToArray();

            if (msg.Command == KettleCmd.STAT)
            {
                var statMsg = new AppKettleStatusMessage(msg);
                return statMsg;
            }

            if (msg.Command == KettleCmd.INIT)
            {
                var initMsg = new AppKettleInitMessage(msg);
                return initMsg;
            }

            return msg;
        }

        public static char[] DecryptStringFromBytes_Aes(byte[] cipherText)
        {
            // AES secrets:
            var SECRET_KEY = Encoding.ASCII.GetBytes("ay3$&dw*ndAD!9)<");
            var SECRET_IV = Encoding.ASCII.GetBytes("7e3*WwI(@Dczxcue");

            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");


            var decryptedBuffer = new char[cipherText.Length];

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = SECRET_KEY;
                aesAlg.IV = SECRET_IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            srDecrypt.ReadBlock(decryptedBuffer, 0,decryptedBuffer.Length);
                        }
                    }
                }
            }

            return decryptedBuffer;
        }
    }

    public class AppKettleStatusMessage : AppKettleMessage
    {
        public AppKettleStatusMessage(AppKettleMessage baseMessage)
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

    public class AppKettleInitMessage : AppKettleMessage
    {
        public AppKettleInitMessage(AppKettleMessage baseMessage)
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

    public class AppKettleMessage : IAppKettleMessage
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

    public interface IAppKettleMessage
    {
        public byte HeaderByte { get; set; }

        public ushort Length { get; set; }

        public byte Sequence { get; set; }

        public KettleCmd Command { get; set; }

        public byte[] Data { get; set; }

        public byte Checksum { get; set; }
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


    public class AppKettleJsonResponseMessage
    {
        public string wifi_cmd { get; set; }
        public string imei { get; set; }
        public string data3 { get; set; }
        public string suc { get; set; }
        public string seq { get; set; }
    }


    public class AppKettleJsonRequestMessage
    {
        public string app_cmd { get; set; } = "62";
        public string imei { get; set; }
        public string SubDev { get; set; } = string.Empty;
        public string data2 { get; set; }
    }
}