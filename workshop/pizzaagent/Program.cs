using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;

// Create the Foundry Project Client
AIProjectClient projectClient = new AIProjectClient(
    new Uri("<your-foundry-endpoint>"),
    new DefaultAzureCredential()
);

// Get the Persistent Agents Client
PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();

// Create an Agent
PersistentAgent agent = agentsClient.Administration.CreateAgent(
    model: "gpt-4o",
    name: "pizza-agent"
);

Console.WriteLine($"Created agent with ID: {agent.Id}");

// Create a Thread for the Agent
PersistentAgentThread thread = agentsClient.Threads.CreateThread();
Console.WriteLine($"Created thread with ID: {thread.Id}");

// Manage the conversation with the Agent
try
{
    string[] exitCommands = ["exit", "quit"];

    while (true)
    {
        Console.Write("You: ");
        string? userInput = Console.ReadLine();

        // loop if no input
        if (string.IsNullOrWhiteSpace(userInput))
        {
            continue;
        }

        // break out if exit commands are given
        if (exitCommands.Contains(userInput.ToLower()))
        {
            break;
        }

        // send user input to agent thread
        PersistentThreadMessage message = agentsClient.Messages.CreateMessage(
            threadId: thread.Id,
            role: MessageRole.User,
            content: userInput
            );

        // run agent
        ThreadRun run = agentsClient.Runs.CreateRun(
            agent: agent,
            thread: thread
            );        

        // wait for run to complete
        do
        {
            Thread.Sleep(500);
            run = agentsClient.Runs.GetRun(thread.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        // the agent had run, get the last message from the thread
        var messages = agentsClient.Messages.GetMessages(
            threadId: thread.Id,
            order: ListSortOrder.Descending
        );

        MessageTextContent? first_message = messages.FirstOrDefault()?.ContentItems
                                            .OfType<MessageTextContent>()
                                            .FirstOrDefault();

        if (first_message != null)
        {
            Console.WriteLine($"Agent: {first_message.Text}");
        }                                    
    }
}
finally
{
    string agentToBeDeletedId = agent.Id;
    bool deleted = agentsClient.Administration.DeleteAgent(agent.Id);
    Console.WriteLine($"Attempting to delete agent with ID: {agentToBeDeletedId}, success: {deleted}");
}