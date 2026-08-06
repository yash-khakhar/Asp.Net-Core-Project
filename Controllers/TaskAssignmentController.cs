using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.DTO.TaskAssignmentDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Repository.TaskAssignment;
namespace TraineeManagement.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskAssignmentController : ControllerBase
    {

        private readonly ITaskAssignmentService _taskAssignmentService;
        private readonly ILogger<TaskAssignmentController> _logger;

        public TaskAssignmentController(ITaskAssignmentService taskAssignmentService, ILogger<TaskAssignmentController> logger)
        {
            _taskAssignmentService = taskAssignmentService;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> GetAllTaskAssignments()
        {
            IEnumerable<TaskAssignmentResponse> taskList = await _taskAssignmentService.GetTaskAssignmentList();
            return Ok(taskList);
        }


        [HttpGet("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> GetTaskAssignmentById(int id)
        {

            if (id <= 0) throw new InvalidRequest("Invalid Data Input"); 

            DetailedTaskAssignmentResponse task = await _taskAssignmentService.GetTaskAssignmentById(id);
            return Ok(task);
        }

        [HttpGet("trainee/{traineeId}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}, {nameof(UserRolesEnum.TRAINEE)}")]
        public async Task<IActionResult> GetTaskAssignmentByTraineeId(int traineeId)
        {

            if (traineeId <= 0) throw new InvalidRequest("Invalid Data Input"); 

            List<DetailedTaskAssignmentResponse> taskList = await _taskAssignmentService.GetTaskAssignmentByTraineeId(traineeId);
            return Ok(taskList);
        }

       
        [HttpPost]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}")]
        public async Task<IActionResult> AddTaskAssignment([FromBody] CreateTaskAssignmentRequest taskRequest)
        {
            if (taskRequest == null) throw new InvalidRequest("Invalid Data Input");

            TaskAssignmentResponse task = await _taskAssignmentService.AddTaskAssignment(taskRequest);
            _logger.LogInformation($"NEW TASK Assignment ADDED");
            return StatusCode(StatusCodes.Status201Created, task);
        }

        
        [HttpPut("{id}")]
        [Authorize(Roles = $"{nameof(UserRolesEnum.ADMIN)}, {nameof(UserRolesEnum.MENTOR)}")]
        public async Task<IActionResult> UpdateTaskAssignment(int id, [FromBody] UpdateTaskAssignmentRequest taskRequest)
        {
            if (taskRequest == null) throw new InvalidRequest("Invalid Data Input");

            TaskAssignmentResponse task = await _taskAssignmentService.UpdateTaskAssignment(id, taskRequest);
            _logger.LogInformation($"TASK Assignment UPDATED");
            return StatusCode(StatusCodes.Status200OK, task);
        }

    }
}
