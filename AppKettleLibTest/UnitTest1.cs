using NUnit.Framework;
using AppKettle;
using System;
using System.Runtime.InteropServices;

namespace AppKettleLibTest
{
       
    /*
        TEST_MSG_STRINGS = (
        "aa000d010000000000000096a40000b7000200004164011e00008c",  # initial status, ignored for now
        "aa001803000000000000009b360000c800030000505004b30000f1",  # status
        "aa00180300000000000000a7360000c800030000505004b30000e5",  # another status
        "aa000d010000000000000017a4000036000200002364035e0000e6",  # status: doesn't match length
        "aa0018030000000000000052360000c8000200002364035e0000aa55",  # status: aa55 example
        "aa00180300000000000000aa55360000c8000200002f60014f000006", #another aa55 example
        "00001803000000000003b78b360000c8000400002b64039100007a",  # another status starting 00...
        "AA001200000000000003B70c390000006402000088",  # kettle on
        "AA000D00000000000003B7283A0000d6",  # kettle off
        "aa000e0000000000000000093a0000c8e6",  # ack kettle off
        "AA000D00000000000003B76d36000095",  # sync msg
        "aa000e00000000000003b715390000c821",  # ack on
        "aa000e0000000000000000093a0000c8e6",  # ack off
    * 
     */
    public class Tests
    {
        public const string InitialStatus = "aa000d010000000000000096a40000b7000200004164011e00008c";  // initial status, ignored for now
        public const string MyInitStatus1 = "aa000d010000000000000071a40000dc00020000326402bc000020"; // initial status from my kettle
        public const string Status1 =       "aa001803000000000000009b360000c800030000505004b30000f1";  // example status
        public const string Status2 = "aa00180300000000000000a7360000c800030000505004b30000e5";  // example status
        public const string KettleOn = "AA001200000000000003B70c390000006402000088"; // kettle on
        public const string KettOnLk = "AA001200000000000003B79E39000000640F0000E9"; // kettle on
        public const string KettleOff = "AA000D00000000000003B7283A0000d6"; // kettle off
        public const string CompleteStatus = "##0083{\"wifi_cmd\":\"62\",\"imei\":\"GD0-12900-77ad\",\"data3\":\"aa0018030000000000000004360000c8000200003764051b000025\",\"suc\":\"00\",\"seq\":\"54545\"}&&";
        
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(CompleteStatus)]
        public void TestFullMessageDecoding(string msgString)
        {
            if(!msgString.Contains("&&")) Assert.Fail();
            var msgs = msgString.Split("&&");
            if(msgs==null) Assert.Fail();
            foreach (var msg in msgs)
            {
                //some message sanity checking
                if (string.IsNullOrWhiteSpace(msg)) continue;
                
                if (msg.Length < 12 ){
                    _logger.LogWarning($"Unusually short message: {msg}");
                    Assert.Fail();
                }

                if (!msg.Substring(6).StartsWith("{")) Assert.Fail();

                var jsonMsg = JsonSerializer.Deserialize<AppKettleJsonResponseMessage>(msg.Substring(6));
                var akMsg = AppKettleMessageFactory.GetHexMessage(jsonMsg.data3, false);
                var statMsg = akMsg as AppKettleStatusHexMessage;

                if (statMsg != null)
                {
                    CurrentTemp = statMsg.CurrentTemp;
                    Volume = statMsg.WaterVolumeMl;
                    State = statMsg.State;
                    StatusTime = DateTime.UtcNow;
                    _logger.LogInformation($"Status: {State} - {CurrentTemp}C, {Volume}ml");
                }
            }

            Assert.Pass();
        }

        [TestCase(KettleOn, KettleCmd.K_ON)]
        [TestCase(KettleOff, KettleCmd.K_OFF)]
        [TestCase(Status1, KettleCmd.STAT)]
        [TestCase(Status2, KettleCmd.STAT)]
        [TestCase(MyInitStatus1, KettleCmd.INIT)]
        public void TestMessageDecoding(string message, KettleCmd expectedCommand,bool encrypted = false)
        {
            var akMsg = AppKettleMessageFactory.GetHexMessage(message, encrypted);

            if(akMsg.Command != expectedCommand)
            {
                Assert.Fail();
            }

            if(akMsg is AppKettleStatusHexMessage)
            {
                var akStatusMsg = akMsg as AppKettleStatusHexMessage;
                Console.WriteLine($"Kettle {akStatusMsg.State}, {akStatusMsg.CurrentTemp}C with {akStatusMsg.WaterVolumeMl}ml");
            }

            Assert.Pass();
        }

        [TestCase(KettleOn)]
        [TestCase(KettleOff)]
        [TestCase(KettOnLk)]
        [TestCase(Status1)]
        [TestCase(Status2)]
        public void ValidateChecksum(string message)
        {
            var calculatedSum = AppKettleHexMessage.CalculateChecksum(message,true);
            if(calculatedSum != Convert.FromHexString(message)[^1])
            {
                Assert.Fail("Checksum validation failure");
            }

            Assert.Pass();
        }
    }
}