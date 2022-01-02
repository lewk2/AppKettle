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
    using Microsoft.Extensions.Logging;

    public class AppKettle
    {
        private TcpClient _tcpClient;
        private NetworkStream _socketStream;
        private StreamReader _socketReader;
        private StreamWriter _socketWriter;
        private ILogger _logger;
        private Task _readingTask;
        private Task _keepAliveTask;
        private IPAddress _kettleIpAddress { get; set; }
        private PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        public AppKettle(ILogger logger)
        {
            _logger = logger;
            _logger.LogInformation("AppKettle Created");
        }

        public bool Connected {
            get
            {
                if (_tcpClient == null) return false;
                return _tcpClient.Connected;
            }
        }

        public bool Discovered { get; private set; }

        public string KettleIp { get { return _kettleIpAddress?.ToString(); } }

        public string Imei { get; private set; }

        public int CurrentTemp { get; private set; }

        public int Volume { get; private set; }

        public DateTime StatusTime {get; private set;}

        public KettleState State { get; private set; }

        public async Task DiscoverKettle(string broadcastAddress, string ipAddress = "", string imei = "")
        {
            var port = 15103;

            if (!string.IsNullOrEmpty(ipAddress))
            {
                _logger.LogInformation($"Kettle has configured IP address ({ipAddress}) with IMEI set to {imei} - bypassing discovery.");
                _kettleIpAddress = IPAddress.Parse(ipAddress);
                Imei = imei;
                Discovered = false;
                return;
            }

            var kettleTask = ListenForKettle(port);

            _logger.LogInformation($"Kettle has configured discovery broadcast address ({broadcastAddress}) - using this for announcement request.");

            while (!kettleTask.IsCompleted) {
                await SendUdpDatagram(broadcastAddress, port, "Probe#2020-05-05-10-47-15-2");
                await Task.Delay(1000);
            }

            return;
        }

        public async Task Connect()
        {
            if (!Discovered && string.IsNullOrEmpty(KettleIp))
            {
                throw new InvalidOperationException("Cannot connect to an undiscovered or unconfigured kettle");
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_kettleIpAddress, 6002);
            _socketStream = _tcpClient.GetStream();
            _socketReader = new StreamReader(_socketStream);
            _socketWriter = new StreamWriter(_socketStream);

            _readingTask = ReadMessages();
            await Query();
            _keepAliveTask = KeepAlive();

            _logger.LogInformation($"AppKettle connected on IP {_kettleIpAddress}");

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
                throw new InvalidOperationException("Cannot command a kettle while not connected");
            }

            _logger.LogInformation("Reqesting kettle wakes");
            await _socketStream.WriteAsync(AppKettleMessageFactory.GetKettleWakeByteMessage(Imei));
            await _socketStream.FlushAsync();
            await Query();
        }

        public async Task Off()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command a kettle while not connected");
            }

            _logger.LogInformation("Reqesting kettle turn off");
            await _socketStream.WriteAsync(AppKettleMessageFactory.GetKettleOffByteMessage(Imei));
            await _socketStream.FlushAsync();
            await Query();
        }

        public async Task On(int TargetTemp = 100, int KeepWarmMins = 5)
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command a kettle while not connected");
            }

            _logger.LogInformation("Reqesting kettle turn on");
            await _socketStream.WriteAsync(AppKettleMessageFactory.GetKettleOnByteMessage(Imei, TargetTemp, KeepWarmMins));
            await _socketStream.FlushAsync();
            await Query();
        }

        public async Task Query()
        {
            if (!Connected)
            {
                throw new InvalidOperationException("Cannot command a kettle while not connected");
            }

            _logger.LogInformation("Reqesting kettle update status");
            await _socketStream.WriteAsync(AppKettleMessageFactory.GetKettleStatusByteMessage(Imei));
            await _socketStream.FlushAsync();
        }

        private async Task SendUdpDatagram(string address, int Port, string Message)
        {
            var ipAddress = IPAddress.Parse(address);
            var IpEndPoint = new IPEndPoint(ipAddress, Port);
            var Socket = new UdpClient();
            var EncodedText = Encoding.UTF8.GetBytes(Message);
            _logger.LogInformation("Sending announcement UDP broadcast message - kettle should reply");
            await Socket.SendAsync(EncodedText, EncodedText.Length, IpEndPoint);
            Socket.Close();
        }

        private async Task ListenForKettle(int networkPort)
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
                        _kettleIpAddress = udpResponse.RemoteEndPoint.Address;
                        Imei = elements[0];
                        Discovered = true;
                       
                        return;
                    }
                }
            }
        }

        private async Task ReadMessages()
        {
            var buffer = new byte[_tcpClient.ReceiveBufferSize];
            _logger.LogInformation($"ReceiveBufferSize: {buffer.Length}");

            var readBytes = 0;
            while (true)
            {
                while (_tcpClient?.Connected == true)
                {
                    while (_socketStream.DataAvailable)
                    {
                        // try
                        // {
                            buffer = new byte[_tcpClient.ReceiveBufferSize];
                            readBytes = await _socketStream.ReadAsync(buffer, 0, _tcpClient.ReceiveBufferSize);
                            
                            if(readBytes < 1)
                            {
                                _logger.LogWarning("Zero bytes read during ReadAsync call... resetting connection");
                                break;
                            }
                            
                            string msgString = Encoding.UTF8.GetString(buffer[0..readBytes]);
                            if(!msgString.Contains("&&")) continue;
                            var msgs = msgString.Split("&&");
                            if(msgs==null) continue;
                            foreach (var msg in msgs)
                            {
                                //some message sanity checking
                                if (string.IsNullOrWhiteSpace(msg)) continue;
                                
                                if (msg.Length < 12 ){
                                    _logger.LogWarning($"Unusually short message: {msg}");
                                    continue;
                                }

                                if (!msg.Substring(6).StartsWith("{")) continue;

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
                        // }
                        // catch (Exception ex)
                        // {
                        //     _logger.LogError($"Kettle connection socket exception: {ex.Message} (ReadBytes: {readBytes})");
                        //     string msgString = Encoding.UTF8.GetString(buffer[0..readBytes]);
                        //     _logger.LogWarning($"MSG: {msgString}");
                        //     break;
                        // }
                    }

                    await Task.Delay(100);
                }

                _logger.LogWarning("Disconnected from kettle... will retry connection in 5 seconds...");
                await Task.Delay(5000);

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_kettleIpAddress, 6002);
                _socketStream = _tcpClient.GetStream();
                _socketReader = new StreamReader(_socketStream);
                _socketWriter = new StreamWriter(_socketStream);
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
                    try
                    {
                        var keepAliveString = "##000bKeepConnect&&";
                        var keepAliveBytes = Encoding.UTF8.GetBytes(keepAliveString);
                        await _socketStream.WriteAsync(keepAliveBytes);
                        await _socketStream.FlushAsync();
                    }
                    catch(Exception ex)
                    {
                        _logger.LogWarning($"Cannot send keep alive - exception while sending: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning("Cannot send keep alive - connection is closed");
                }
            }
        }

    }
}