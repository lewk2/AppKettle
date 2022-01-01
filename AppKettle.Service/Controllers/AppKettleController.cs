using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AppKettle.Service.Managers;
using AppKettle.Exceptions;

namespace AppKettle.Controllers
{
    [ApiController]
    [Route("kettle/v1/[controller]")]
    public class AppKettleController : ControllerBase
    {
        private readonly ILogger<AppKettleController> _logger;
        private readonly KettleManager _kettleManager;

        public AppKettleController(ILogger<AppKettleController> logger, KettleManager kettleManager)
        {
            _logger = logger;
            _kettleManager = kettleManager;
        }

        [HttpGet]
        [Route("")]
        public async Task<AppKettle> GetAppKettleStatus()
        {
            var ak = _kettleManager.GetAppKettle();

            var waitCount = 1;
            var stateTimeout = 30;
            while (DateTime.UtcNow.Subtract(_kettleManager.GetAppKettle().StatusTime) > TimeSpan.FromSeconds(5))
            {
                if (!ak.Connected)
                {
                    if(ak.Discovered == false && string.IsNullOrEmpty(ak.KettleIp)){
                        _logger.LogInformation($"Waiting for kettle to be discovered ({waitCount++})");
                        await Task.Delay(1000);
                    }
                    else
                    {
                        _logger.LogInformation($"Waiting for kettle to connect ({waitCount++})");
                        await ak.Connect();
                    }
                }
                else
                {
                    _logger.LogInformation($"Waiting for kettle to update status ({waitCount++})");
                    await ak.Query();
                }

                await Task.Delay(100);
                if (waitCount > stateTimeout)
                {
                    var connProbMsg = "Kettle is unresponsive";

                    var connProbDetails = new ProblemDetails
                    {
                        Title = "Kettle Unresponsive",
                        Detail = connProbMsg,
                        Type = "/error/unresponsive",
                        Status = 400
                    };

                    throw new HttpResponseException
                    {
                        Status = 400,
                        Value = connProbDetails
                    };
                }
                
            }

            return _kettleManager.GetAppKettle();// (HttpContext.RequestAborted);

        }

        [HttpGet]
        [Route("discovered")]
        public bool GetAppKettleDiscovery()
        {
            return _kettleManager.IsKettleDiscovered();// (HttpContext.RequestAborted);
        }

        [HttpGet]
        [Route("boil")]
        public async Task<AppKettle> SimpleBoil()
        {
            var ak = _kettleManager.GetAppKettle();
            if(!ak.Connected){
                var connProbMsg = "Kettle is not connected";
                
                var connProbDetails = new ProblemDetails
                {
                    Title = "Kettle Disconnected",
                    Detail = connProbMsg,
                    Type = "/error/disconnected",
                    Status = 400
                };

                throw new HttpResponseException
                {
                    Status = 400,
                    Value = connProbDetails
                };
            }

            if(ak.Volume < 200){
                var volProbMsg = "Kettle does not have enough water";
                
                var volProbDetails = new ProblemDetails
                {
                    Title = "Kettle Disconnected",
                    Detail = volProbMsg,
                    Type = "/error/needswater",
                    Status = 400
                };

                throw new HttpResponseException
                {
                    Status = 400,
                    Value = volProbDetails
                };
            }

            if(ak.State == KettleState.NotOnBase){
                var noKettleProbMsg = "Kettle is not on the base";
                
                var noKettleProbDetails = new ProblemDetails
                {
                    Title = "Kettle Missing",
                    Detail = noKettleProbMsg,
                    Type = "/error/nokettle",
                    Status = 400
                };

                throw new HttpResponseException
                {
                    Status = 400,
                    Value = noKettleProbDetails
                };
            }

            if(ak.State == KettleState.Standby){
                await _kettleManager.KettleWake();
                await Task.Delay(100);
                ak = _kettleManager.GetAppKettle();
            }

            var waitCount = 1;
            var stateTimeout = 30;
            while(ak.State != KettleState.Ready)
            {
                _logger.LogInformation($"Waiting for kettle to become ready to boil ({waitCount++})");

                await Task.Delay(1000);

                if (waitCount > stateTimeout)
                {
                    var notReadyProbMsg = "Kettle is not in 'Ready' state";

                    var notReadyProbDetails = new ProblemDetails
                    {
                        Title = "Kettle Not Ready",
                        Detail = notReadyProbMsg,
                        Type = "/error/notready",
                        Status = 400
                    };

                    throw new HttpResponseException
                    {
                        Status = 400,
                        Value = notReadyProbDetails
                    };
                }
            }

            await _kettleManager.KettleOn();

            return _kettleManager.GetAppKettle();// (HttpContext.RequestAborted);
        }

        [HttpPost]
        [Route("wake")]
        public async Task<bool> WakeKettle()
        {
            return await _kettleManager.KettleWake();// (HttpContext.RequestAborted);
        }

        [HttpPost]
        [Route("on")]
        public async Task<bool> KettleOn(int TargetTemp = 100, int KeepWarmMins = 5)
        {
            return await _kettleManager.KettleOn(TargetTemp,KeepWarmMins);// (HttpContext.RequestAborted);
        }

        [HttpPost]
        [Route("off")]
        public async Task<bool> KettleOff()
        {
            return await _kettleManager.KettleOff();// (HttpContext.RequestAborted);
        }
    }
}
