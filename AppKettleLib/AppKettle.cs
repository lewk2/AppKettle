namespace AppKettle
{
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.IO;
    using System;
    using System.Text.Json;
    using System.Threading;

    public class AppKettle
    {
        private TcpClient _tcpClient;
        private NetworkStream _socketStream;
        private StreamReader _socketReader;
        private StreamWriter _socketWriter;
        private Task _readingTask;
        private Task _keepAliveTask;
        private ushort _sequence = 0;
        private IPAddress _kettleIpAddress { get; set; }
        private PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        public bool Connected {
            get
            {
                return _tcpClient.Connected;
            }
        }

        public bool Discovered { get; private set; }

        public string KettleIp { get { return _kettleIpAddress.ToString(); } }

        public string Imei { get; private set; }

        public int CurrentTemp { get; private set; }

        public int Volume { get; private set; }

        public DateTime StatusTime {get; private set;}

        public KettleState State { get; private set; }

        public static async Task<AppKettle> DiscoverKettle(string broadcastAddress, string ipAddress = "", string imei = "")
        {
            var port = 15103;

            if (!string.IsNullOrEmpty(ipAddress))
            {
                var ak = new AppKettle
                {
                    _kettleIpAddress = IPAddress.Parse(ipAddress),
                    Imei = imei,
                    Discovered = true
                };

                return ak;
            }


            var kettleTask = ListenForKettle(port);

            while(!kettleTask.IsCompleted) {
                await SendUdpDatagram(broadcastAddress, port, "Probe#2020-05-05-10-47-15-2");
                await Task.Delay(1000);
            }

            return await kettleTask;
        }

        public async Task Connect()
        {
            if (!Discovered)
            {
                throw new InvalidOperationException("Cannot connect to an undiscovered kettle");
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_kettleIpAddress, 6002);
            _socketStream = _tcpClient.GetStream();
            _socketReader = new StreamReader(_socketStream);
            _socketWriter = new StreamWriter(_socketStream);

            _readingTask = ReadMessages();
            await GetStatus();
            _keepAliveTask = KeepAlive();
        
        }

        public void Disconnect()
        {
            _socketReader.Close();
            _socketWriter.Close();
            _socketStream.Close();
            _tcpClient.Close();
            _readingTask.Wait();
            _readingTask.Dispose();
        }

        public async Task Wake()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command an kettle while not connected");
            }

            var kettleWakeString = $"AA000D00000000000003B7{(_sequence++):X2}410000";

            var kettleWakeStringWithSum = $"{kettleWakeString}{AppKettleMessage.CalculateChecksum(kettleWakeString):X2}".ToLowerInvariant();

            var akRequest = new AppKettleJsonRequestMessage
            {
                data2 = kettleWakeStringWithSum,
                imei = Imei
            };

            var requestJson = JsonSerializer.Serialize(akRequest);
            var jsonBytes = Encoding.UTF8.GetBytes(requestJson);
            var len = jsonBytes.Length;
            var byteSend = Encoding.UTF8.GetBytes($"##00{len:X2}{requestJson}&&");

            await _socketStream.WriteAsync(byteSend);
            await _socketStream.FlushAsync();
        }

        public async Task Off()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command an kettle while not connected");
            }

            var kettleWakeString = $"AA000D00000000000003B7{(_sequence++):X2}3A0000";

            var kettleWakeStringWithSum = $"{kettleWakeString}{AppKettleMessage.CalculateChecksum(kettleWakeString):X2}".ToLowerInvariant();

            var akRequest = new AppKettleJsonRequestMessage
            {
                data2 = kettleWakeStringWithSum,
                imei = Imei
            };

            var requestJson = JsonSerializer.Serialize(akRequest);
            var jsonBytes = Encoding.UTF8.GetBytes(requestJson);
            var len = jsonBytes.Length;
            var byteSend = Encoding.UTF8.GetBytes($"##00{len:X2}{requestJson}&&");
            await _socketStream.WriteAsync(byteSend);
            await _socketStream.FlushAsync();
        }

        public async Task On(int targetTemp = 100, int keepWarmMins = 5)
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command an kettle while not connected");
            }

            var kettleOnString = $"AA001200000000000003B7{(_sequence++):X2}39000000{targetTemp:X2}{keepWarmMins:X2}0000";

            var kettleOnStringWithSum = $"{kettleOnString}{AppKettleMessage.CalculateChecksum(kettleOnString):X2}".ToLowerInvariant();

            var akRequest = new AppKettleJsonRequestMessage
            {
                data2 = kettleOnStringWithSum,
                imei = Imei
            };

            var requestJson = JsonSerializer.Serialize(akRequest);
            var jsonBytes = Encoding.UTF8.GetBytes(requestJson);
            var len = jsonBytes.Length;
            var byteSend = Encoding.UTF8.GetBytes($"##00{len:X2}{requestJson}&&");

            await _socketStream.WriteAsync(byteSend);
            await _socketStream.FlushAsync();
        }

        private static async Task SendUdpDatagram(string address, int Port, string Message)
        {
            var ipAddress = IPAddress.Parse(address);
            var IpEndPoint = new IPEndPoint(ipAddress, Port);
            var Socket = new UdpClient();
            var EncodedText = Encoding.UTF8.GetBytes(Message);
            await Socket.SendAsync(EncodedText, EncodedText.Length, IpEndPoint);
            Socket.Close();
        }

        private static async Task<AppKettle> ListenForKettle(int networkPort)
        {
            var localEp = new IPEndPoint(IPAddress.Any, networkPort);
            var udpClient = new UdpClient();
            udpClient.Client.EnableBroadcast = true;
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(localEp);

            while (true)
            {
                var udpResponse = await udpClient.ReceiveAsync();

                var receiveString = Encoding.ASCII.GetString(udpResponse.Buffer);

                if (!receiveString.StartsWith("Probe"))
                {
                    var elements = receiveString.Split('#');
                    if (elements.Length > 4 && elements[1] == "123456")
                    {
                        var ak = new AppKettle
                        {
                            _kettleIpAddress = udpResponse.RemoteEndPoint.Address,
                            Imei = elements[0],
                            Discovered = true
                        };

                        return ak;
                    }

                }
            }
        }

        private async Task ReadMessages()
        {
            var buffer = new byte[_tcpClient.ReceiveBufferSize];
            while (_tcpClient.Connected)
            {
                while (_socketStream.DataAvailable)
                {
                    try
                    {
                        var readBytes = await _socketStream.ReadAsync(buffer, 0, _tcpClient.ReceiveBufferSize);
                        string msgString = Encoding.UTF8.GetString(buffer[0..readBytes]);

                        var msgs = msgString.Split("&&");
                        foreach (var msg in msgs)
                        {
                            if (string.IsNullOrWhiteSpace(msg)) continue;
                            if (!msg.Substring(6).StartsWith("{")) continue;
                            var jsonMsg = JsonSerializer.Deserialize<AppKettleJsonResponseMessage>(msg.Substring(6));
                            var akMsg = AppKettleMessageFactory.GetMessage(jsonMsg.data3, false);
                            var statMsg = akMsg as AppKettleStatusMessage;
                            
                            if (statMsg != null)
                            {
                                CurrentTemp = statMsg.CurrentTemp;
                                Volume = statMsg.WaterVolumeMl;
                                State = statMsg.State;
                                StatusTime = DateTime.UtcNow;
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Closed socket {ex.Message}");
                        return;
                    }
                }

                await Task.Delay(100);
            }
        }

        private async Task KeepAlive()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot keep kettle alive while not connected");
            }

            while (await _timer.WaitForNextTickAsync())
            {
                if (Connected)
                {
                    var keepAliveString = "##000bKeepConnect&&";
                    var keepAliveBytes = Encoding.UTF8.GetBytes(keepAliveString);
                    await _socketStream.WriteAsync(keepAliveBytes);
                    await _socketStream.FlushAsync();
                }
            }
        }

        private async Task GetStatus()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command an kettle while not connected");
            }

            var kettleStatusString = $"AA000D00000000000003B7{(_sequence++):X2}360000";

            var kettleStatusStringWithSum = $"{kettleStatusString}{AppKettleMessage.CalculateChecksum(kettleStatusString):X2}".ToLowerInvariant();

            var akRequest = new AppKettleJsonRequestMessage
            {
                data2 = kettleStatusStringWithSum,
                imei = Imei
            };

            var requestJson = JsonSerializer.Serialize(akRequest);
            var jsonBytes = Encoding.UTF8.GetBytes(requestJson);
            var len = jsonBytes.Length;
            var byteSend = Encoding.UTF8.GetBytes($"##00{len:X2}{requestJson}&&");

            await _socketStream.WriteAsync(byteSend);
            await _socketStream.FlushAsync();
        }

    }
}