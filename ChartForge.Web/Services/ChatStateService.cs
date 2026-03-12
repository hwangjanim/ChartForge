using ChartForge.Core.Entities;
using ChartForge.Core.Enums;
using ChartForge.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;

namespace ChartForge.Web.Services;

public enum CanvasTab { Chart, Data }

public class ChatStateService
{
    private readonly IConversationService _conversationService;
    private readonly IWebHostEnvironment _env;

    // Initialized lazily via InitializeAsync; placeholders hold UI defaults until then.
    public User CurrentUser { get; private set; } = new User
    {
        DisplayName = "Dev User",
        Email = "dev@chartforge.com",
        IsActive = true
    };

    public List<Conversation> Conversations { get; private set; } = new();
    public bool IsInitialized { get; private set; }

    public ChatStateService(IConversationService conversationService, IWebHostEnvironment env)
    {
        _conversationService = conversationService;
        _env = env;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        CurrentUser = await _conversationService.GetOrCreateUserAsync(
            email: "dev@chartforge.com",
            displayName: "Dev User",
            ssoSubjectId: "dev-user");

        Conversations = await _conversationService.GetByUserIdAsync(CurrentUser.Id);
        IsInitialized = true;
        Notify();
    }

    // ── Active state ──────────────────────────────────────────────────────────

    public Conversation ActiveConversation { get; private set; } = new Conversation();
    public bool IsNewUnsavedConversation { get; private set; } = true;
    public List<Message> Messages { get; private set; } = new();
    public List<ChartState> ChartStates { get; private set; } = new();
    public ChartState? ActiveChartVersion { get; private set; }
    public List<DataState> DataStates { get; private set; } = new();
    public DataState? ActiveDataVersion { get; private set; }
    public bool IsStreaming { get; private set; }

    private int _tempIdCounter = 0;
    private readonly List<Message> _pendingMessages = new();

    public event Action? OnChange;
    private void Notify() => OnChange?.Invoke();

    public CanvasTab ActiveTab { get; private set; } = CanvasTab.Chart;

    public int TotalVersions => ChartStates.Count;
    public int ActiveVersionNumber => ActiveChartVersion?.VersionNumber ?? 0;
    public bool CanGoPrev => ActiveChartVersion is not null && ActiveVersionNumber > 1;
    public bool CanGoNext => ActiveChartVersion is not null && ActiveVersionNumber < TotalVersions;

    public int TotalDataVersions => DataStates.Count;
    public int ActiveDataVersionNumber => ActiveDataVersion?.VersionNumber ?? 0;
    public bool CanGoDataPrev => ActiveDataVersion is not null && ActiveDataVersionNumber > 1;
    public bool CanGoDataNext => ActiveDataVersion is not null && ActiveDataVersionNumber < TotalDataVersions;

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a conversation's full data (messages + chart states) from the database.
    /// </summary>
    public async Task LoadConversationAsync(Conversation conversation)
    {
        var full = await _conversationService.GetByIdAsync(conversation.Id);
        if (full is null) return;

        ActiveConversation = full;
        Messages = full.Messages.ToList();
        ChartStates = full.ChartStates.ToList();
        ActiveChartVersion = ChartStates.MaxBy(v => v.VersionNumber);
        DataStates = full.DataStates.ToList();
        ActiveDataVersion = DataStates.MaxBy(v => v.VersionNumber);
        IsStreaming = false;
        IsNewUnsavedConversation = false;
        _pendingMessages.Clear();
        Notify();
    }

    public void NewConversation()
    {
        _tempIdCounter--;
        ActiveConversation = new Conversation
        {
            Id = _tempIdCounter,
            Title = "New Conversation",
            UserId = CurrentUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        Messages = new List<Message>();
        ChartStates = new List<ChartState>();
        ActiveChartVersion = null;
        DataStates = new List<DataState>();
        ActiveDataVersion = null;
        IsNewUnsavedConversation = true;
        _pendingMessages.Clear();
        Notify();
    }

    public async Task AddConversationToListAsync(string title)
    {
        ActiveConversation.Title = string.IsNullOrWhiteSpace(title)
            ? "New Chart"
            : title.Length > 30 ? title[..30] : title;
        ActiveConversation.UpdatedAtUtc = DateTime.UtcNow;

        var saved = await _conversationService.CreateAsync(CurrentUser.Id, ActiveConversation.Title);
        int realId = saved.Id;
        ActiveConversation.Id = realId;

        foreach (var msg in _pendingMessages)
        {
            msg.ConversationId = realId;
        }

        var csvPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "102heatmaps.csv");
        if (File.Exists(csvPath))
        {
            var seedData = new DataState
            {
                VersionNumber = 1,
                RawData = await File.ReadAllTextAsync(csvPath),
                CreatedAtUtc = DateTime.UtcNow,
                ConversationId = realId,
            };
            await _conversationService.AddDataStateAsync(seedData);
            DataStates = new List<DataState> { seedData };
            ActiveDataVersion = seedData;
        }

        Conversations.Insert(0, ActiveConversation);
        IsNewUnsavedConversation = false;
        Notify();
    }

    /// <summary>Adds a message to the in-memory state and queues it for DB persistence.</summary>
    public void StartStreaming(Message message)
    {
        Messages.Add(message);
        ActiveConversation.Messages.Add(message);
        _pendingMessages.Add(message);
        IsStreaming = true;
    }

    /// <summary>
    /// Ends streaming, persists queued messages and any new chart/data version to the database.
    /// </summary>
    public async Task CompleteStreamingAsync(ChartState? newVersion, DataState? newDataVersion = null)
    {
        IsStreaming = false;

        foreach (var msg in _pendingMessages)
            await _conversationService.AddMessageAsync(msg);
        _pendingMessages.Clear();

        if (newVersion is not null)
        {
            await _conversationService.AddChartStateAsync(newVersion);
            ChartStates.Add(newVersion);
            ActiveConversation.ChartStates.Add(newVersion);
            ActiveChartVersion = newVersion;
        }

        if (newDataVersion is not null)
        {
            await _conversationService.AddDataStateAsync(newDataVersion);
            DataStates.Add(newDataVersion);
            ActiveConversation.DataStates.Add(newDataVersion);
            ActiveDataVersion = newDataVersion;
        }

        await _conversationService.UpdateTimestampAsync(ActiveConversation.Id);
        Notify();
    }

    public void FailStreaming()
    {
        IsStreaming = false;
        _pendingMessages.Clear();
        Notify();
    }

    public void SetTab(CanvasTab tab)
    {
        ActiveTab = tab;
        Notify();
    }

    public void SetActiveVersion(ChartState version)
    {
        ActiveChartVersion = version;
        Notify();
    }

    public void SetActiveDataVersion(DataState version)
    {
        ActiveDataVersion = version;
        Notify();
    }

    public async Task RenameConversationAsync(int id, string newTitle)
    {
        var conversation = Conversations.FirstOrDefault(c => c.Id == id);
        if (conversation is null) return;

        conversation.Title = newTitle;
        await _conversationService.RenameAsync(id, newTitle);
        Notify();
    }
}
