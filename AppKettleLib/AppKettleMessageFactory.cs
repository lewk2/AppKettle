
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AppKettle
{
    public static class AppKettleMessageFactory
    {
        private static ushort _sequence = 0;

        public static byte[] GetKettleStatusByteMessage(string imei)
        {
            var kettleString = $"AA000D00000000000003B7{(_sequence++):X2}360000";

            var kettleStringWithSum = $"{kettleString}{AppKettleHexMessage.CalculateChecksum(kettleString):X2}".ToLowerInvariant();

            return GetByteMessage(kettleStringWithSum, imei);
        }

        public static byte[] GetKettleWakeByteMessage(string imei)
        {
            var kettleString = $"AA000D00000000000003B7{(_sequence++):X2}410000";

            var kettleStringWithSum = $"{kettleString}{AppKettleHexMessage.CalculateChecksum(kettleString):X2}".ToLowerInvariant();

            return GetByteMessage(kettleStringWithSum, imei);
        }


        public static byte[] GetKettleOnByteMessage(string imei, int targetTemp = 100, int keepWarmMins = 5)
        {
            var kettleString = $"AA001200000000000003B7{(_sequence++):X2}39000000{targetTemp:X2}{keepWarmMins:X2}0000";

            var kettleStringWithSum = $"{kettleString}{AppKettleHexMessage.CalculateChecksum(kettleString):X2}".ToLowerInvariant();

            return GetByteMessage(kettleStringWithSum, imei);
        }


        public static byte[] GetKettleOffByteMessage(string imei)
        {
            var kettleString = $"AA000D00000000000003B7{(_sequence++):X2}3A0000";

            var kettleStringWithSum = $"{kettleString}{AppKettleHexMessage.CalculateChecksum(kettleString):X2}".ToLowerInvariant();

            return GetByteMessage(kettleStringWithSum, imei);
        }

        public static IAppKettleHexMessage GetHexMessage(string message, bool encrypted = false)
        {
            var msgBytes = Convert.FromHexString(message);
            if (encrypted)
            {
                var encryptedBytes = Encoding.ASCII.GetBytes(message)[0..];
                var decryptedChars = DecryptStringFromBytes_Aes(encryptedBytes);
                msgBytes = Convert.FromHexString(new string(decryptedChars));
            }

            var msgMemory = new Memory<byte>(msgBytes);

            var msg = new AppKettleHexMessage()
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
                var statMsg = new AppKettleStatusHexMessage(msg);
                return statMsg;
            }

            if (msg.Command == KettleCmd.INIT)
            {
                var initMsg = new AppKettleInitHexMessage(msg);
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
    
        private static byte[] GetByteMessage(string data, string imei)
        {
            var akRequest = new AppKettleJsonRequestMessage
            {
                data2 = data,
                imei = imei
            };

            var requestJson = JsonSerializer.Serialize(akRequest);
            var jsonBytes = Encoding.UTF8.GetBytes(requestJson);
            var len = jsonBytes.Length;
            var byteMsg = Encoding.UTF8.GetBytes($"##{len:X4}{requestJson}&&");
            return byteMsg;
        }
    }


    public static class AppKettleByteMessage
    {

    }
}