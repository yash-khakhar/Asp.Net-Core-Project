using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraineeManagement.api.DTO.MentorDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Enum.Mentor;
using TraineeManagement.api.Repository.Mentor;

namespace TraineeManagement.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MentorController : ControllerBase
    {
        private readonly IMentorService _mentorService;
        private readonly ILogger<TraineeController> _logger;
        public MentorController(IMentorService mentorService, ILogger<TraineeController> logger)
        {
            _mentorService = mentorService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}")]
        public async Task<IActionResult> GetAllMentor(
            [FromQuery(Name = "pageNumber")] int pageNumber = 1,
            [FromQuery(Name = "pageSize")] int pageSize = 10,
            [FromQuery(Name = "search")] string? search = null,
            [FromQuery(Name = "status")] MentorStatusEnum? status = null
        )
        {
            MentorSearchResultDto mentorList = await _mentorService.GetMentorAsync(pageNumber, pageSize, search, status);
            return Ok(mentorList);
        }

        
        [HttpGet("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}")]
        public async Task<IActionResult> GetMentorById(int id)
        {
            MentorResponse mentor = await _mentorService.GetMentorById(id);
            return Ok(mentor);
        }

        
        [HttpPost]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}")]
        public async Task<IActionResult> AddMentor([FromBody] CreateMentorRequest mentorRequest)
        {
            MentorResponse mentor = await _mentorService.AddMentor(mentorRequest);

            _logger.LogInformation($"NEW MENTOR ADDED: Mentor Name: {mentorRequest.FirstName}");

            return StatusCode(StatusCodes.Status201Created, mentor);
        }

       
        [HttpPut("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}")]
        public async Task<IActionResult> UpdateMentor([FromRoute]int id, [FromBody] UpdateMentorRequest mentorRequest)
        {
            MentorResponse mentor = await _mentorService.UpdateMentor(id, mentorRequest);

            _logger.LogInformation($"MENTOR UPDATED: Mentor Name: {mentorRequest.FirstName}");

            return StatusCode(StatusCodes.Status200OK, mentor);

        }

        
        [HttpDelete("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}")]
        public async Task<IActionResult> DeleteMentor(int id)
        {
            await _mentorService.DeleteMentorById(id);

            _logger.LogInformation($"MENTOR DELETED: Mentor Id: {id}");

            return StatusCode(StatusCodes.Status204NoContent);
        }
    }
}
