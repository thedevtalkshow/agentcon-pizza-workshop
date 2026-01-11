
#:package Azure.AI.Agents.Persistent@1.1.0
#:package Azure.AI.Projects@1.2.0-beta.5

#:property EnablePreviewFeatures=true

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

// verify the folder exists
string folderPath = "../documents";
if (!Directory.Exists(folderPath))
{
    string errorMessage = $""""
    Documents folder not found at {folderPath}.
    Create it and add your "Contoso Pizza" files (PDF, TXT, MD, etc.).
    """";
    Console.WriteLine(errorMessage);
    return;
}

List<string> uploadedFileIds = new();

// upload each file in the documents folder
foreach (string filePath in Directory.GetFiles(folderPath))
{
    string fileName = Path.GetFileName(filePath);
    Console.WriteLine($"Uploading file: {fileName}");

    PersistentAgentFileInfo fileInfo = agentsClient.Files.UploadFile(filePath, PersistentAgentFilePurpose.Agents);
    uploadedFileIds.Add(fileInfo.Id);
}

Console.WriteLine("File upload complete.");

// create a vector store
PersistentAgentsVectorStore vectorStore = agentsClient.VectorStores.CreateVectorStore(
    name: "contoso-pizza-store-information",
    fileIds: uploadedFileIds
);

Console.WriteLine($"Created vector store with ID: {vectorStore.Id}");