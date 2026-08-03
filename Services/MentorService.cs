using Microsoft.EntityFrameworkCore;
using TraineeManagement.api.CustomException;
using TraineeManagement.api.Data;
using TraineeManagement.api.DTO.MentorDto;
using TraineeManagement.api.Enum.Mentor;
using TraineeManagement.api.Helper;
using TraineeManagement.api.Models;
using TraineeManagement.api.Repository.Mentor;

namespace TraineeManagement.api.Services
{
    public class MentorService : IMentorService
    {
        private AppDbContext _context;

        public MentorService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MentorResponse> AddMentor(CreateMentorRequest mentor)
        {

            if(!mentor.FirstName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper FirstName");
            }

            if (!mentor.LastName.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper LastName");
            }

            if (!mentor.Email.IsValidEmail())
            {
                throw new InvalidRequest("Enter Proper Email Address");
            }

            if (!mentor.Expertise.IsOnlyLetters())
            {
                throw new InvalidRequest("Enter Proper Expertise");
            }

            MentorModel mentorModel = new MentorModel(
                    mentor.FirstName.ToLower(),
                    mentor.LastName.ToLower(),
                    mentor.Email.ToLower(),
                    mentor.Expertise.ToLower(),
                    mentor.Status
                );
            

            _context.Mentor.Add(mentorModel);
            await _context.SaveChangesAsync();

            return MentorModel.ToDto(mentorModel);

        }

        public async Task<bool> DeleteMentorById(int id)
        {
            MentorModel? mentor = await _context.Mentor.FindAsync(id);
            if (mentor == null) throw new NotFoundException("Mentor Not Found!");
            _context.Mentor.Remove(mentor);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<MentorResponse> GetMentorById(int id)
        {
            MentorResponse? mentor = await _context.Mentor
                .Where(m => m.Id == id)
                .Select(m => new MentorResponse(m.Id, m.FirstName, m.LastName, m.Email, m.Expertise, m.Status, m.CreatedAt, m.UpdatedAt))
                .FirstOrDefaultAsync();

            if (mentor == null) throw new NotFoundException("Mentor Not Found");

            return mentor;
        }

        public async Task<IEnumerable<MentorResponse>> GetMentorList()
        {
            List<MentorResponse> mentorList = await _context.Mentor
             .Select(p => new MentorResponse(p.Id, p.FirstName, p.LastName, p.Email, p.Expertise, p.Status, p.CreatedAt, p.UpdatedAt))
             .ToListAsync();

            return mentorList;
        }

        public async Task<MentorResponse> UpdateMentor(int id, UpdateMentorRequest updateMentorRequest)
        {
            MentorModel? mentor = await _context.Mentor.FindAsync(id);
            if (mentor == null) throw new NotFoundException("Mentor Not Found");

            if (updateMentorRequest.FirstName != null)
            {

                if (!updateMentorRequest.FirstName.IsOnlyLetters())
                {
                    throw new InvalidRequest("Enter Proper FirstName");
                }

                mentor.FirstName = updateMentorRequest.FirstName;
            }

            if (updateMentorRequest.LastName != null)
            {

                if (!updateMentorRequest.LastName.IsOnlyLetters())
                {
                    throw new InvalidRequest("Enter Proper LastName");
                }

                mentor.LastName = updateMentorRequest.LastName;
            }

            if (updateMentorRequest.Email != null)
            {

                if (!updateMentorRequest.Email.IsValidEmail())
                {
                    throw new InvalidRequest("Enter Proper Email Address");
                }

                mentor.Email = updateMentorRequest.Email;
            }

            mentor.Status = updateMentorRequest.Status;

            if (updateMentorRequest.Expertise != null)
            {

                mentor.Expertise = updateMentorRequest.Expertise;
            }

            mentor.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MentorModel.ToDto(mentor);
        }

        public async Task<MentorSearchResultDto> GetMentorAsync(int pageNumber, int pageSize, string? search, MentorStatusEnum? status)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _context.Mentor.AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                query = query.Where(t =>
                    t.FirstName.ToLower().Contains(lowerSearch) ||
                    t.LastName.ToLower().Contains(lowerSearch) ||
                    t.Email.ToLower().Contains(lowerSearch) ||
                    t.Expertise.ToLower().Contains(lowerSearch)
                );
            }

            int totalRecords = await query.CountAsync();
            int skip = (pageNumber - 1) * pageSize;

            var mentorList = await query
                .Skip(skip)
                .Take(pageSize)
                .Select(p => new MentorResponse(
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.Email,
                    p.Expertise,
                    p.Status,
                    p.CreatedAt,
                    p.UpdatedAt
                ))
                .ToListAsync();

            var result = new MentorSearchResultDto(pageNumber, pageSize, totalRecords, mentorList);

            return result;
        }

    }
}
