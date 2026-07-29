using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TraineeManagement.api.Enum;
using TraineeManagement.api.models;

namespace TraineeManagement.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}")]
    public class HealhController : ControllerBase
    {

        private readonly HealthCheckService _healthCheckService;

        public HealhController(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(HealthModel), StatusCodes.Status200OK)]
        public ActionResult<HealthModel> GetHealthResult()
        {
            var statusPayload = new HealthModel
            {
                Status = "Healthy",
                ApplicationName = "TraineeManagement.api",
                Time = DateTime.UtcNow
            };

            return Ok(statusPayload);

        }

        [HttpGet("live")]
        public async Task<IActionResult> GetLive()
        {
            var report = await _healthCheckService.CheckHealthAsync(check => check.Tags.Contains("live"));

            if (report.Status == HealthStatus.Healthy)
            {
                return Ok(new { status = "Healthy" });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy" });
        }

        [HttpGet("ready")]
        public async Task<IActionResult> GetReady()
        {
            // This will execute MySQL, Redis, and RabbitMQ checks concurrently
            var report = await _healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"));

            var response = new
            {
                status = report.Status.ToString(),
                duration = report.TotalDuration,
                dependencies = report.Entries.Select(e => new
                {
                    service = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration
                })
            };

            if (report.Status == HealthStatus.Healthy)
            {
                return Ok(response);
            }

            // If even ONE dependency fails, the overall status becomes Unhealthy
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }
}

