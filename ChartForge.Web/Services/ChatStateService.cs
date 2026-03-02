using ChartForge.Core.Entities;
using ChartForge.Core.Enums;

namespace ChartForge.Web.Services
{
    public class ChatStateService
    {
        public Conversation ActiveConversation { get; private set; }
        public List<Message> Messages { get; private set; } = new();
        public List<ChartState> ChartVersions { get; private set; } = new();
        public ChartState? ActiveChartVersion { get; private set; }
        public bool IsStreaming { get; private set; }
        //public string StreamingBuffer { get; private set; } = string.Empty;

        public event Action? OnChange;

        private void Notify() => OnChange?.Invoke();

        public int TotalVersions => ChartVersions.Count;
        public int ActiveVersionNumber => ActiveChartVersion.VersionNumber;

        public bool CanGoPrev =>
            ActiveChartVersion is not null && ActiveVersionNumber > 1;

        public bool CanGoNext =>
            ActiveChartVersion is not null && ActiveVersionNumber < TotalVersions;

        // mutations

        public void LoadConversation(
            Conversation conversation,
            List<Message> messages,
            List<ChartState> chartVersions)
        {
            ActiveConversation = conversation;
            Messages = messages;
            ChartVersions = chartVersions;

            ActiveChartVersion = chartVersions.MaxBy(v => v.VersionNumber);

        IsStreaming = false;
            //StreamingBuffer = string.Empty;
            Notify();
            }

        public void NewConversation()
        {
            ActiveConversation = new Conversation();
            Messages = new List<Message>();
            ChartVersions = new List<ChartState>();
            ActiveChartVersion = null;
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
                ChartVersions.Add(newVersion);
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
        
    }
}
