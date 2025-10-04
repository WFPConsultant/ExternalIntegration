using Microsoft.AspNetCore.Mvc;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;

namespace UVP.ExternalIntegration.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntegrationController : ControllerBase
    {
        private readonly IInvocationManagerService _invocationManager;
        private readonly ILogger<IntegrationController> _logger;
        private readonly IIntegrationOrchestrationService _orchestrationService;

        public IntegrationController(
            IInvocationManagerService invocationManager,
            ILogger<IntegrationController> logger,
            IIntegrationOrchestrationService orchestrationService)
        {
            _invocationManager = invocationManager;
            _logger = logger;
            _orchestrationService = orchestrationService;
        }

        [HttpPost("clearance/initial-call")] //execute-cycle
        public async Task<IActionResult> ExecuteFullClearanceCycle([FromBody] IntegrationRequestDto request)
        {
            try
            {
                _logger.LogInformation("Executing full clearance cycle for {IntegrationType}", request.IntegrationType);

                var result = await _orchestrationService.ExecuteFullClearanceCycleAsync(
                    request.DoaCandidateId,
                    request.CandidateId,
                    request.IntegrationType);

                return Ok(new
                {
                    Success = result,
                    Message = result ? "Clearance cycle initiated successfully" : "Failed to initiate clearance cycle"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing clearance cycle");
                return BadRequest(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        [HttpPost("clearance/check-progress")]
        public async Task<IActionResult> CheckAndProgressClearance([FromBody] IntegrationRequestDto request)
        {
            try
            {
                var result = await _orchestrationService.CheckAndProgressClearanceAsync(
                    request.DoaCandidateId,
                    request.CandidateId,
                    request.IntegrationType);

                return Ok(new
                {
                    Success = result,
                    Message = "Clearance progress checked"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking clearance progress");
                return BadRequest(new
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}
