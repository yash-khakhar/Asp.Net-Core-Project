using TraineeManagement.api.DTO.TaskAssignmentDto;

namespace TraineeManagement.api.Repository.TaskAssignment
{
    public interface ITaskAssignmentService
    {
        public Task<IEnumerable<TaskAssignmentResponse>> GetTaskAssignmentList();
        public Task<DetailedTaskAssignmentResponse> GetTaskAssignmentById(int id);
        public Task<List<DetailedTaskAssignmentResponse>> GetTaskAssignmentByTraineeId(int traineeId);
        public Task<TaskAssignmentResponse> AddTaskAssignment(CreateTaskAssignmentRequest taskAssignment);
        public Task<TaskAssignmentResponse> UpdateTaskAssignment(int id, UpdateTaskAssignmentRequest taskAssignment);
    }
}
