using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.Task;
using TraineeManagement.api.Enum;
using TraineeManagement.api.Helper;
using TraineeManagement.api.Models;
using TraineeManagement.api.Repository.Task;

namespace TraineeManagement.api.Services
{
    public class TaskService : ITaskService
    {
        private AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TaskResponse> AddTask(CreateTaskRequest task)
        {

            if (!task.Title.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper Task Title");
            }

            if (!task.ExpectedTechStack.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper Tech Stack");
            }

            TaskModel taskModel = new TaskModel(
                    task.Title.ToLower(),
                    task.Description.ToLower(),
                    task.ExpectedTechStack.ToLower(),
                    task.DueDate,
                    task.Status
                );


            _context.Task.Add(taskModel);
            await _context.SaveChangesAsync();

            return TaskModel.ToDto(taskModel);
        }

        public async Task<bool> DeleteTaskById(int id)
        {
            TaskModel? task = await _context.Task.FindAsync(id);
            if (task == null) throw new NotFoundException("Task Not Found!");
            _context.Task.Remove(task);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TaskResponse> GetTaskById(int id)
        {
            TaskResponse? task = await _context.Task
                .Where(t => t.Id == id)
                .Select(t => new TaskResponse(t.Id, t.Title, t.Description, t.ExpectedTechStack, t.DueDate, t.Status, t.CreatedAt, t.UpdatedAt))
                .FirstOrDefaultAsync();

            if (task == null) throw new NotFoundException("Task Not Found");

            return task;
        }

        public async Task<IEnumerable<TaskResponse>> GetTaskList()
        {
            List<TaskResponse> taskList = await _context.Task
              .Select(p => new TaskResponse(p.Id, p.Title, p.Description, p.ExpectedTechStack, p.DueDate, p.Status, p.CreatedAt, p.UpdatedAt))
              .ToListAsync();

            return taskList;
        }

        public async Task<TaskResponse> UpdateTask(int id, UpdateTaskRequest taskRequest)
        {
            TaskModel? task = await _context.Task.FindAsync(id);
            if (task == null) throw new NotFoundException("Task Not Found");

            if (taskRequest.Title != null)
            {
                
                if (!taskRequest.Title.All(char.IsLetterOrDigit))
                {
                    throw new InvalidRequest("Task title should contain only Alpha-Numeric Characters");
                }

                task.Title = taskRequest.Title;
            }

            if (taskRequest.Description != null) task.Description = taskRequest.Description;

            if (taskRequest.ExpectedTechStack != null)
            {
                task.ExpectedTechStack = taskRequest.ExpectedTechStack;
            }

            if (taskRequest.Status != null) task.Status = (TaskStatusEnum)taskRequest.Status;

            if(taskRequest.DueDate != null) task.DueDate = (DateTime)taskRequest.DueDate;

            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return TaskModel.ToDto(task);
        }
    }
}
