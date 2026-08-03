namespace TraineeManagement.api.DTO.MentorDto
{
    public class MentorSearchResultDto
    {

        public MentorSearchResultDto(int pageNumber, int pageSize, int totalRecords, List<MentorResponse> data)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalRecords = totalRecords;
            Data = data;
        }

        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public List<MentorResponse> Data { get; set; }
    }
}
