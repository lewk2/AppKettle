using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AppKettle.Service.Managers;

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
        public AppKettle GetAppKettleStatus()
        {
            return _kettleManager.GetAppKettle();// (HttpContext.RequestAborted);
        }


        [HttpGet]
        [Route("discovered")]
        public bool GetAppKettleDiscovery()
        {
            return _kettleManager.IsKettleDiscovered();// (HttpContext.RequestAborted);
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
