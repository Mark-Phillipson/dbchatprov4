using System.Text.Json;
using DBChatPro.Models;
using Microsoft.Extensions.AI;
using MudBlazor;
using Azure;
using Azure.AI.OpenAI;
using System.Text;
using Markdig;
using DBChatPro.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DBChatPro.Components.Pages;

public partial class Home : ComponentBase
{
    // Dependency Injection
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IQueryService queryService { get; set; }
    [Inject] public required IConnectionService connectionService { get; set; }
    [Inject] public required IDatabaseService dataService { get; set; }
    [Inject] public required IConfiguration config { get; set; }
    [Inject] public required AIService aiService { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }

    // Table styling
    private bool dense = false;
    private bool hover = true;
    private bool striped = true;
    private bool bordered = true;

    // Form data
    public FormModel FmModel { get; set; } = new FormModel();
    public string ChatPrompt = "";
    public string aiModel = "gpt-4o";
    public string aiPlatform = "OpenAI";
    public string activeDatabase = "VoiceAdmin";

    // General UI data
    private bool Loading = false;
    private bool chatLoading = false;
    private string LoadingMessage = String.Empty;
    public AIConnection ActiveConnection { get; set; } = new();
    public DatabaseSchema dbSchema = new DatabaseSchema() { SchemaRaw = new List<string>(), SchemaStructured = new List<TableSchema>() };

    // Data lists
    public List<HistoryItem> History { get; set; } = new();
    public List<HistoryItem> Favorites { get; set; } = new();
    public List<List<string>> RowData = new();
    public List<AIConnection> Connections { get; set; } = new();
    public List<ChatMessage> ChatHistory = new();

    // Prompt & completion data
    private string Prompt = String.Empty;
    private string Summary = String.Empty;
    private string Query = String.Empty;
    private string Error = String.Empty;

    // UI Drawer stuff
    bool open = true;
    Anchor anchor;
    void ToggleDrawer(Anchor anchor)
    {
        open = !open;
        this.anchor = anchor;
    }

    protected override async Task OnInitializedAsync()
    {
        Connections = await connectionService.GetAIConnections();
        if (Connections.Count > 0)
        {
            ActiveConnection = Connections.FirstOrDefault() ?? new AIConnection();
            activeDatabase = ActiveConnection.Name;
            dbSchema = await dataService.GenerateSchema(ActiveConnection);
        }
        else
        {
            ActiveConnection = new AIConnection();
        }
        History = await queryService.GetQueries(ActiveConnection.Name, QueryType.History);
        Favorites = await queryService.GetQueries(ActiveConnection.Name, QueryType.Favorite);

    }

    private async Task SaveFavorite()
    {
        await queryService.SaveQuery(FmModel.Prompt, ActiveConnection.Name, QueryType.Favorite);
        Favorites = await queryService.GetQueries(ActiveConnection.Name, QueryType.Favorite);
        Snackbar.Add("Saved favorite!", Severity.Success);
    }

    private async Task EditQuery()
    {
        RowData = await dataService.GetDataTable(ActiveConnection, Query);
        Snackbar.Add("Results updated.", Severity.Success);
    }

    public async Task LoadDatabase(string databaseName)
    {
        ActiveConnection = (await connectionService.GetAIConnections()).FirstOrDefault(x => x.Name == databaseName) ?? new AIConnection();
        dbSchema = await dataService.GenerateSchema(ActiveConnection);
        History = await queryService.GetQueries(ActiveConnection.Name, QueryType.History);
        Favorites = await queryService.GetQueries(ActiveConnection.Name, QueryType.Favorite);
        ClearUI();
    }

    private void ClearUI()
    {
        Prompt = String.Empty;
        Summary = String.Empty;
        Query = String.Empty;
        Error = String.Empty;
        RowData = new List<List<string>>();
        FmModel = new FormModel();
    }

    public async Task LoadQuery(string query)
    {
        FmModel.Prompt = query;
        await RunDataChat(query);
    }

    public async Task OnChat()
    {
        chatLoading = true;
        ChatHistory.Add(new ChatMessage(ChatRole.User, ChatPrompt));
        ChatPrompt = "";

        var result = await aiService.ChatPrompt(ChatHistory, aiModel, aiPlatform);

        ChatHistory.Add(new ChatMessage(ChatRole.Assistant, result.Message.Text));
        chatLoading = false;
    }

    public void ClearChat()
    {
        ChatHistory.Clear();
        ChatHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful AI assistant. Provide helpful insights about the following data: " + JsonSerializer.Serialize(RowData)));
    }

    public async Task OnSubmit()
    {

        await RunDataChat(FmModel.Prompt);
    }

    public async Task RunDataChat(string Prompt)
    {
        try
        {
            Loading = true;
            ChatHistory.Clear();
            LoadingMessage = "Getting the AI query...";
            var aiResponse = await aiService.GetAISQLQuery(aiModel, aiPlatform, Prompt, dbSchema, ActiveConnection.DatabaseType);

            Query = aiResponse.query;
            Summary = aiResponse.summary;
            bool isSelectOnly = MakeSureSelectQueryOnly(Query);
            if (!isSelectOnly)
            {
                Loading = false;
                return;
            }

            LoadingMessage = "Running the Database query...";
            RowData = await dataService.GetDataTable(ActiveConnection, aiResponse.query);
            ChatHistory.Add(new ChatMessage(ChatRole.System, "You are a helpful AI assistant. Provide helpful insights about the following data: " + JsonSerializer.Serialize(RowData)));

            Loading = false;
            await queryService.SaveQuery(Prompt, ActiveConnection.Name, QueryType.History);
            History = await queryService.GetQueries(ActiveConnection.Name, QueryType.History);
            Favorites = await queryService.GetQueries(ActiveConnection.Name, QueryType.Favorite);
            Error = string.Empty;
        }
        catch (Exception e)
        {
            Error = e.Message;
            Loading = false;
            LoadingMessage = String.Empty;
        }
    }
    private async Task ExportToCsv()
    {
        if (RowData == null || RowData.Count == 0)
        {
            Snackbar.Add("No data to export.", Severity.Error);
            return;
        }
        var csv = new StringBuilder();
        foreach (var row in RowData!)
        {
            csv.AppendLine(string.Join(",", row));
        }

        var csvContent = csv.ToString();
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var base64 = Convert.ToBase64String(bytes);
        await JSRuntime.InvokeVoidAsync("downloadFile", "ResultExport.csv", base64);
    }
    private bool MakeSureSelectQueryOnly(string query)
    {
        if ((!query.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || query.Trim().Contains("EXEC", StringComparison.OrdinalIgnoreCase)) || Query.Count(c => c == ';') > 1)
        {
            Snackbar.Add("Only single SELECT queries are allowed. You can however copy the SQL and run it in a different tool, for example Azure Data Studio", Severity.Error);
            return false;
        }
        return true;

    }
}