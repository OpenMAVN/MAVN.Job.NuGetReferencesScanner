using System.Net;
using MAVN.Job.NuGetReferencesScanner.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.PlatformAbstractions;

namespace MAVN.Job.NuGetReferencesScanner.Controllers
{
    [Route("api/[controller]")]
    public class IsAliveController : Controller
    {
        /// <summary>
        /// Checks service is alive
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IsAliveResponse), (int)HttpStatusCode.OK)]
        public IActionResult Get()
        {
            var app = PlatformServices.Default.Application;
            return Ok(
                new IsAliveResponse
                {
                    Name = app.ApplicationName,
                    Version = app.ApplicationVersion,
                });
        }
    }
}
