using TraineeManagement.api.DTO.MentorDto;
using TraineeManagement.api.DTO.Task;
using TraineeManagement.api.DTO.TraineeDto;
using TraineeManagement.api.Enum;

namespace TraineeManagement.api.DTO.TaskAssignmentDto
{
    public class DetailedTaskAssignmentResponse
    {
        public DetailedTaskAssignmentResponse(
            int id,
            int traineeId,
            int mentorId,
            int taskId,
            DateTime assignedDate,
            DateTime dueDate,
            TaskAssignmentStatusEnum status,
            string remarks,
            TraineeResponse trainee,
            MentorResponse mentor,
            TaskResponse task
        )
        {
            Id = id;
            TraineeId = traineeId;
            MentorId = mentorId;
            TaskId = taskId;
            AssignedDate = assignedDate;
            DueDate = dueDate;
            Status = status;
            Remarks = remarks;
            Trainee = trainee;
            Mentor = mentor;
            Task = task;
        }

        public DetailedTaskAssignmentResponse() { }

        public int Id { get; set; }
        public int TraineeId { get; set; }
        public int MentorId { get; set; }
        public int TaskId { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime DueDate { get; set; }
        public TaskAssignmentStatusEnum Status { get; set; }
        public string Remarks { get; set; } = string.Empty;

        public TraineeResponse Trainee { get; set; } = new();
        public MentorResponse Mentor { get; set; } = new();
        public TaskResponse Task { get; set; } = new();
    }
}