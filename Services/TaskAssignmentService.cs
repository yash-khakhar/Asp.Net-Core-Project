using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.TaskAssignmentDto;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Models;
using TraineeManagement.api.Redis.CacheKeys;
using TraineeManagement.api.Redis.Repository;
using TraineeManagement.api.Repository.TaskAssignment;

namespace TraineeManagement.api.Services
{
    public class TaskAssignmentService : ITaskAssignmentService
    {
        private AppDbContext _context;
        private readonly IRedisCacheRepo _redisCacheRepo;

        public TaskAssignmentService(AppDbContext context, IRedisCacheRepo redisCacheRepo)
        {
            _context = context;
            _redisCacheRepo = redisCacheRepo;
        }
        public async Task<TaskAssignmentResponse> AddTaskAssignment(CreateTaskAssignmentRequest taskAssignment)
        {

            DateTime dueDate = await _context.Task
                .Where(t => t.Id == taskAssignment.TaskId)
                .Select(t => t.DueDate)
                .FirstOrDefaultAsync();

            if (dueDate == default)
            {
                throw new NotFoundException("Task not found!");
            }

            if (dueDate < taskAssignment.AssignedDate)
            {
                throw new InvalidRequest("Task Due Date cannot be before Assigned Date!");
            }

            TaskAssignmentModel taskAssignmentModel = new TaskAssignmentModel(
                     taskAssignment.TraineeId,
                     taskAssignment.MentorId,
                     taskAssignment.TaskId,
                     taskAssignment.AssignedDate,
                     dueDate,
                     taskAssignment.Status,
                     taskAssignment.Remarks == null ? "" : taskAssignment.Remarks
            );

            _context.TaskAssignment.Add(taskAssignmentModel);

            await _context.SaveChangesAsync();

            // removing all trainee key from cache
            await _redisCacheRepo.RemoveItem(TaskAssignmentCacheKey.AllTaskAssignment);
            await _redisCacheRepo.RemoveItem($"{TaskAssignmentCacheKey.SingleTaskAssignmnet}:trainee:{taskAssignment.TraineeId}");

            return TaskAssignmentModel.ToDto(taskAssignmentModel);
        }

        public async Task<TaskAssignmentResponse> GetTaskAssignmentById(int id)
        {

            string cacheKey = $"{TaskAssignmentCacheKey.SingleTaskAssignmnet}:{id}";

            TaskAssignmentResponse? cachedData = await _redisCacheRepo.GetItem<TaskAssignmentResponse>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }
            else
            {

                TaskAssignmentResponse? taskAssignment = await _context.TaskAssignment
                        .Where(t => t.Id == id)
                        .Select(t => new TaskAssignmentResponse(
                            t.Id,
                            t.TraineeId,
                            t.MentorId,
                            t.TaskId,
                            t.AssignedDate,
                            t.DueDate,
                            t.Status,
                            t.Remarks == null ? string.Empty : t.Remarks
                        ))
                        .FirstOrDefaultAsync();

                if (taskAssignment == null) throw new NotFoundException("Task Assignment Not Found");

                await _redisCacheRepo.SetItem<TaskAssignmentResponse>(cacheKey, taskAssignment);

                return taskAssignment;

            }


        }

        public async Task<IEnumerable<TaskAssignmentResponse>> GetTaskAssignmentList()
        {

            string cacheKey = TaskAssignmentCacheKey.AllTaskAssignment;

            IEnumerable<TaskAssignmentResponse>? cachedData = await _redisCacheRepo.GetItem<IEnumerable<TaskAssignmentResponse>>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }
            else
            {

                List<TaskAssignmentResponse> taskAssignmentList = await _context.TaskAssignment
                    .Include(t => t.Trainee)
                    .Include(t => t.Mentor)
                    .Include(t => t.Task)
                    .Select(t => new TaskAssignmentResponse(
                        t.Id,
                        t.TraineeId,
                        t.MentorId,
                        t.TaskId,
                        t.AssignedDate,
                        t.DueDate,
                        t.Status,
                        t.Remarks == null ? string.Empty : t.Remarks

                     ))
                    .ToListAsync();

                await _redisCacheRepo.SetItem<IEnumerable<TaskAssignmentResponse>>(cacheKey, taskAssignmentList);

                return taskAssignmentList;

            }


        }

        public async Task<TaskAssignmentResponse> UpdateTaskAssignment(int id, UpdateTaskAssignmentRequest taskAssignment)
        {
            TaskAssignmentModel? task = await _context.TaskAssignment.FindAsync(id);
            if (task == null) throw new NotFoundException("Task Assignment Not Found");

            if (taskAssignment.TraineeId != null) task.TraineeId = (int)taskAssignment.TraineeId;
            if (taskAssignment.MentorId != null) task.MentorId = (int)taskAssignment.MentorId;
            if (taskAssignment.TaskId != null) task.TaskId = (int)taskAssignment.TaskId;
            if (taskAssignment.AssignedDate != null) task.AssignedDate = (DateTime)taskAssignment.AssignedDate;
            if (taskAssignment.DueDate != null) task.DueDate = (DateTime)taskAssignment.DueDate;
            if (taskAssignment.Status != null) task.Status = (TaskAssignmentStatusEnum)taskAssignment.Status;
            if (taskAssignment.Remarks != null) task.Remarks = taskAssignment.Remarks;

            await _context.SaveChangesAsync();

            // removing all trainee key from cache
            await _redisCacheRepo.RemoveItem(TaskAssignmentCacheKey.AllTaskAssignment);
            await _redisCacheRepo.RemoveItem($"{TaskAssignmentCacheKey.SingleTaskAssignmnet}:{id}");
            await _redisCacheRepo.RemoveItem($"{TaskAssignmentCacheKey.SingleTaskAssignmnet}:trainee:{taskAssignment.TraineeId}");

            return TaskAssignmentModel.ToDto(task);
        }

        public async Task<List<DetailedTaskAssignmentResponse>> GetTaskAssignmentByTraineeId(int traineeId)
        {
            string cacheKey = $"{TaskAssignmentCacheKey.SingleTaskAssignmnet}:trainee:{traineeId}";

            List<DetailedTaskAssignmentResponse>? cachedData = await _redisCacheRepo.GetItem<List<DetailedTaskAssignmentResponse>>(cacheKey);

            if (cachedData != null)
            {
                return cachedData;
            }
            else
            {

                List<DetailedTaskAssignmentResponse> taskAssignments = await _context.TaskAssignment
                    .Where(t => t.TraineeId == traineeId)
                    .Include(t => t.Trainee)
                    .Include(t => t.Mentor)
                    .Include(t => t.Task)
                    .Select(t => new DetailedTaskAssignmentResponse(
                        t.Id,
                        t.TraineeId,
                        t.MentorId,
                        t.TaskId,
                        t.AssignedDate,
                        t.DueDate,
                        t.Status,
                        t.Remarks ?? string.Empty,
                        new DTO.TraineeDto.TraineeResponse(
                            t.Trainee!.Id,
                            t.Trainee.FirstName,
                            t.Trainee.LastName,
                            t.Trainee.Email,
                            t.Trainee.TechStack,
                            t.Trainee.Status,
                            t.Trainee.CreatedAt,
                            t.Trainee.UpdatedAt
                        ),
                        new DTO.MentorDto.MentorResponse(
                            t.Mentor!.Id,
                            t.Mentor.FirstName,
                            t.Mentor.LastName,
                            t.Mentor.Email,
                            t.Mentor.Expertise,
                            t.Mentor.Status,
                            t.Mentor.CreatedAt,
                            t.Mentor.UpdatedAt
                        ),
                        new DTO.Task.TaskResponse(
                            t.Task!.Id,
                            t.Task.Title,
                            t.Task.Description,
                            t.Task.ExpectedTechStack,
                            t.Task.DueDate,
                            t.Task.Status,
                            t.Task.CreatedAt,
                            t.Task.UpdatedAt
                        )
                    ))
                .ToListAsync();

                if (taskAssignments == null || taskAssignments.Count == 0) throw new NotFoundException("Task Assignment Not Found");

                await _redisCacheRepo.SetItem<List<DetailedTaskAssignmentResponse>>(cacheKey, taskAssignments);

                return taskAssignments;

            }
        }
    }
}
