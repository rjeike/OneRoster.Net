using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Models;
using OneRosterSync.Net.Extensions;
using Newtonsoft.Json;

namespace OneRosterSync.Net.Controllers
{
    [ApiController, Route("api/mockapi/")]
    public class MockApiController : ControllerBase
    {
        private readonly ILogger Logger;

        public MockApiController(ILogger<MockApiController> logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Organization updates
        /// </summary>
        [HttpPost, Route("org")]
        public async Task<JsonResult> Org([FromBody] ApiPost<CsvOrg> org) { return await ProcessEntity<CsvOrg>(org); }

        /// <summary>
        /// Course updates
        /// </summary>
        [HttpPost, Route("course")]
        public async Task<JsonResult> Course([FromBody] ApiPost<CsvCourse> course) { return await ProcessEntity<CsvCourse>(course); }

        /// <summary>
        /// Academic Session updates
        /// </summary>
        [HttpPost, Route("academicsession")]
        public async Task<JsonResult> AcademicSession([FromBody] ApiPost<CsvAcademicSession> academicSession) { return await ProcessEntity<CsvAcademicSession>(academicSession); }

        /// <summary>
        /// Class updates
        /// </summary>
        [HttpPost, Route("class")]
        public async Task<JsonResult> Class([FromBody] ApiPost<CsvClass> _class) { return await ProcessEntity<CsvClass>(_class); }

        /// <summary>
        /// User updates
        /// </summary>
        [HttpPost, Route("user")]
        public async Task<JsonResult> UserEntity([FromBody] ApiPost<CsvUser> user) { return await ProcessEntity<CsvUser>(user); }

        /// <summary>
        /// Enrollment updates
        /// Note that the LMS Target IDs for class and user are included in the EnrollmentMap object
        /// </summary>
        [HttpPost, Route("enrollment")]
        public async Task<JsonResult> Enrollment([FromBody] ApiPost<CsvEnrollment> enrollment) { return await ProcessEntity<CsvEnrollment>(enrollment); }


        /// <summary>
        /// Generic Handler for the MockAPIs
        /// </summary>
        private async Task<JsonResult> ProcessEntity<T>(ApiPost<T> entity) where T : CsvBaseObject
        {
            Logger.Here().LogInformation($"Recieved data from Mock API for {entity.EntityType}: {JsonConvert.SerializeObject(entity)}");

            // simulate time to respond
            await Task.Delay(100);

            ApiResponse response = new ApiResponse
            {
                Success = true,
                TargetId = entity.TargetId ?? System.Guid.NewGuid().ToString(),
            };

            Logger.Here().LogInformation($"Finished Processing data  for {entity.EntityType}.  Response: {JsonConvert.SerializeObject(response)}");

            return new JsonResult(response);
        }
    }
}
