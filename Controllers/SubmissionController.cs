using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.DTO.SubmissionDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Repository.Submission;

namespace TraineeManagement.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubmissionController : ControllerBase
    {
        private readonly ISubmissionService _submissionService;
        private readonly ILogger<SubmissionController> _logger;

        public SubmissionController(ISubmissionService submissionService, ILogger<SubmissionController> logger)
        {
            _submissionService = submissionService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}")]
        public async Task<IActionResult> GetAllSubmissions()
        {
            IEnumerable<SubmissionResponse> submissionList = await _submissionService.GetSubmissionList();
            return Ok(submissionList);
        }


        [HttpGet("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> GetSubmissionById(int id)
        {

            if (id <= 0) throw new InvalidRequest("Invalid Data Input");

            SubmissionResponse submission = await _submissionService.GetSubmissionById(id);
            return Ok(submission);
        }

        [HttpGet("task-assignment/{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> GetSubmissionByTaskAssignmentId(int id)
        {

            if (id <= 0) throw new InvalidRequest("Invalid Data Input");

            List<SubmissionResponse> submission = await _submissionService.GetSubmissionByTaskAssignmentId(id);
            return Ok(submission);
        }


        [HttpPost]
        [Authorize(Roles = $"{nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> AddSubmission([FromForm] CreateSubmissionRequest submissionRequest, List<IFormFile> files)
        {
            if (submissionRequest == null) throw new InvalidRequest("Invalid Data Input");

            var CorrelationId = Activity.Current?.RootId
                              ?? HttpContext.TraceIdentifier;

            SubmissionResponse submission = await _submissionService.AddSubmission(submissionRequest, files, CorrelationId);
            _logger.LogInformation($"NEW Submission ADDED");
            return StatusCode(StatusCodes.Status201Created, submission);
            
        }


        [HttpGet("submission-files/{submissionFileId}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> DownloadFile(int submissionFileId)
        {
            var (fileStream, contentType, fileName) = await _submissionService.DownloadFileAsync(submissionFileId, HttpContext.RequestAborted);

            return File(fileStream, contentType, fileName);

        }

        [HttpDelete("submission-files/{submissionFileId}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> DeleteSubmission(int submissionFileId)
        {
            await _submissionService.DeleteSubmissionAsync(submissionFileId);

            return StatusCode(StatusCodes.Status204NoContent);

        }

    }
}
