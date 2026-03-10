namespace ChartForge.Core.Entities;
public class Conversation
{
   
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }

    // --- Navigation Properties ---
    public int? UserId { get; set; }
    public User User { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<ChartState> ChartStates { get; set; } = new List<ChartState>();
    public ICollection<DataState> DataStates { get; set; } = new List<DataState>();
}