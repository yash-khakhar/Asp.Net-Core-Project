using TraineeManagement.api.DTO.MentorDto;
using TraineeManagement.api.Enum.Mentor;

namespace TraineeManagement.api.Repository.Mentor
{
    public interface IMentorService
    {
        public Task<IEnumerable<MentorResponse>> GetMentorList();
        public Task<MentorResponse> GetMentorById(int id);
        public Task<MentorResponse> AddMentor(CreateMentorRequest mentor);
        public Task<MentorResponse> UpdateMentor(int id, UpdateMentorRequest updateMentorRequest);
        public Task<bool> DeleteMentorById(int id);
        public Task<MentorSearchResultDto> GetMentorAsync(int pageNumber, int pageSize, string? search, MentorStatusEnum? status);
    }
}
