namespace ChartForge.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string SsoSubjectId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}