using ChartForge.Core.Entities;
using ChartForge.Core.Enums;

namespace ChartForge.Web.Services
{
    public class ChatStateService
    {
        // Mock Data
        public User CurrentUser { get; private set; } = new User
        {
            Id = 1,
            DisplayName = "Dev User",
            Email = "dev@chartforge.com",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginAtUtc = DateTime.UtcNow
        };
        public List<Conversation> Conversations { get; private set; } = new();
        public void LoadMockData()
        {
            Conversations = new List<Conversation>
            {
                new Conversation { Id = 1, Title = "Monthly Churn Chart", UserId = 1, UpdatedAtUtc = DateTime.UtcNow, Messages = new List<Message>
    {
        new Message
        {
            Id = 1,
            Content = "asdasd",
            Role = MessageRole.User,
            SentAtUtc = DateTime.UtcNow,
            SequenceNumber = 1
        },
        new Message
        {
            Id = 2,
            Content = "AI response here",
            Role = MessageRole.Assistant,
            SentAtUtc = DateTime.UtcNow,
            SequenceNumber = 2
        }
    }},
                new Conversation { Id = 2, Title = "Sales Pipeline Q3", UserId = 1, UpdatedAtUtc = DateTime.UtcNow.Date.AddDays(-1) },
                new Conversation { Id = 3, Title = "Revenue Breakdown", UserId = 1, UpdatedAtUtc = DateTime.UtcNow.Date.AddDays(-8) },
            };
            Notify();
        }

        // End of Mock Data
        public ChatStateService()
        {
            // remove once in production
            if (!Conversations.Any())
                LoadMockData();

        }
        public Conversation ActiveConversation { get; private set; }
        public List<Message> Messages { get; private set; } = new();
        public List<ChartState> ChartStates { get; private set; } = new();
        public ChartState? ActiveChartVersion { get; private set; }
        public bool IsStreaming { get; private set; }
        //public string StreamingBuffer { get; private set; } = string.Empty;

        public event Action? OnChange;

        private void Notify() => OnChange?.Invoke();

        public int TotalVersions => ChartStates.Count;
        public int ActiveVersionNumber => ActiveChartVersion.VersionNumber;

        public bool CanGoPrev =>
            ActiveChartVersion is not null && ActiveVersionNumber > 1;

        public bool CanGoNext =>
            ActiveChartVersion is not null && ActiveVersionNumber < TotalVersions;

        // mutations

        public void LoadConversation(Conversation conversation)
        {
            ActiveConversation = conversation;
            Messages = conversation.Messages.ToList();
            ChartStates = conversation.ChartStates.ToList();

            ActiveChartVersion = ChartStates.MaxBy(v => v.VersionNumber);

            IsStreaming = false;
            Notify();
        }

        public void NewConversation()
        {
            ActiveConversation = new Conversation();
            Messages = new List<Message>();
            ChartStates = new List<ChartState>();
            ActiveChartVersion = null;
            Notify();
        }

        public void StartStreaming(Message userMessage)
        {
            Messages.Add(userMessage);
            IsStreaming = true;
            Notify();
        }

        public void CompleteStreaming(ChartState? newVersion)
        {
            IsStreaming = false;

            if (newVersion is not null)
            {
                ChartStates.Add(newVersion);
                ActiveChartVersion = newVersion;
            }

            Notify();
           
        }

        // timeout, cancel
        // preserve
        public void FailStreaming()
        {
            IsStreaming = false;
            //if(!string.IsNullOrWhiteSpace())
            Notify();
        }

        public void SetActiveVersion(ChartState version)
        {
            ActiveChartVersion = version;
            Notify();
        }

        public void RenameConversation(int id, string newTitle)
        {
            var conversation = Conversations.FirstOrDefault(c => c.Id == id);

            if (conversation is not null)
            {
                conversation.Title = newTitle;
                Notify();
            }
        }
        
    }
}
