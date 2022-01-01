using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AppKettle.Service.Managers
{
    public class KettleManager : IDisposable
    {
        private readonly ILogger<KettleManager> _logger;
        private readonly Task _initTask;
        private AppKettle _kettle;
        private readonly AppConfig _config;

        #region Constructors

        public KettleManager(ILogger<KettleManager> logger, IConfiguration config)
        {
            _logger = logger;

            _config = (AppConfig)config.Get(typeof(AppConfig));

            _initTask = InitKettle(_config.BroadcastIp,_config.KettleIp,_config.KettleImei);
            Task.Delay(1000);

            _logger.LogInformation("KettleManager created");
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _logger.LogTrace("KettleManager disposing");
            GC.SuppressFinalize(this);
            _logger.LogTrace("KettleManager disposed");
        }

        #endregion

        #region Members

        public void Startup()
        {
            _logger.LogInformation("Starting up Kettle Manager");
        }

        public AppKettle GetAppKettle()
        {
            return _kettle;
        }

        public bool IsKettleDiscovered()
        {
            if(_kettle==null) return false;

            return _kettle.Discovered;
        }

        public async Task<bool> KettleWake()
        {
            if (_kettle == null) return false;

            if (_kettle.State == KettleState.Ready) return false;

            await _kettle.Wake();

            return true;
        }

        public async Task<bool> KettleOff()
        {
            if (_kettle == null) return false;

            if (_kettle.State != KettleState.Heating && _kettle.State != KettleState.KeepWarm) return false;

            await _kettle.Off();

            return true;
        }

        public async Task<bool> KettleOn(int TargetTemp = 100, int KeepWarm = 5)
        {
            if (_kettle == null) return false;

            if(_kettle.State != KettleState.Ready) return false;

            await _kettle.On(TargetTemp, KeepWarm);

            return true;
        }

        public async Task<bool> KettleQuery()
        {
            if (_kettle == null) return false;

            await _kettle.Query();

            return true;
        }

        #endregion

        #region Private Members
        private async Task InitKettle(string broadcastAddress, string kettleIp = "", string kettleImei = "")
        {
            _kettle = new AppKettle(_logger);
            await _kettle.DiscoverKettle(broadcastAddress,kettleIp,kettleImei);

            await _kettle.Connect();
        }

        #endregion
    }
}
