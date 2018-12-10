using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Controllers
{
    [Route("api/mockapi/")]
    [ApiController]
    public class MockApiController : ControllerBase
    {
        private readonly ILogger Logger;

        public MockApiController(ILogger<MockApiController> logger)
        {
            Logger = logger;
        }

        // GET: api/MockApi/ping
        [HttpGet, Route("ping")]
        public IEnumerable<string> Ping()
        {
            return new string[] { "value1", "value2" };
        }

        // POST: api/MockApi/update
        [HttpPost, Route("update")]
        public JsonResult Update([FromBody] string data)
        {
            Logger.LogInformation($"Recieved data from Mock API {data}");
            return new JsonResult(new { myvar = "booyah" });
        }
    }
}
