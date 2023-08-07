#r "System.Net.WebClient"
#r "nuget: Nake.Meta, 3.0.0-beta-01"
#r "nuget: Nake.Utility, 3.0.0-beta-01"

using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Nake;
using static Nake.FS;
using static Nake.Log;
using static Nake.Env;

const string applicationName = @"App";
const string root = @".bootstrap\files\AppBootstrap";
const string currentHashFile = @".bootstrap\current.hash";
const string previousHashFile = @".bootstrap\previous.hash";
const string trackLogFile = @".bootstrap\logs\time_track.log";
const string changesLogFile = @".bootstrap\logs\time_changes.log";
const string conflictsLogFile = @".bootstrap\logs\time_conflicts.log";			   

List<string> updateFolders = new List<string>()
{
    @"*.*",
    @"App.Application",
    @"App.Domain",
    @"App.Infrastructure",
    @"App.Services",
    @"App.Generator",
    @"App.IntegrationTests",
    @"App.UnitTests",
    @"App.Clients.Spa"
};

List<string> ignoreFolders = new List<string>()
{
    @"bin",
    @"obj"
};

List<string> addOnlyList = new List<string>()
{
	"README.md",
	".gitignore",
    @".sln",
    @"App.Services\appsettings.json",
};

var sourceFolders = new Dictionary<string, string>(){ };

// Item1 - file path, Item2 - string to find and replace, Item3 - a new string value
var fileContentReplacements = new List<Tuple<string, string, string>>{

};
var contentReplacements = new List<Tuple<string, string, string>>{

};


[Nake] void Run(bool alwaysCopy = false, string projectName = "")
{		
    try
    {
        BuildReplacements(projectName);
        BootStrapMessage();
        WriteInfoMessage("Starting the files comparison. Please wait!");
        Copy(alwaysCopy);
        ApplyReplacements();
        BootStrapEndMessage();
    }
    catch(Exception ex)
    {
        WriteErrorMessage(ex.ToString());
    }
}
[Nake] void ApplyReplacements()
{
    foreach(var replacement in fileContentReplacements.Where(r => System.IO.File.Exists(r.Item1)))
    {
       File.WriteAllText( replacement.Item1, File.ReadAllText(replacement.Item1).Replace(replacement.Item2, replacement.Item3));
    }
}

[Nake] void Copy(bool alwaysCopy)
{		
    var files = GetUpdateFiles(updateFolders);
    var sourceFileHashes = CreateHashesForSourceFiles(files);
    UpdateFiles(files, sourceFileHashes, alwaysCopy);
    UpdateHashes(sourceFileHashes);
}

bool IsFileOnlyForAdd(string file)
{
    return addOnlyList.Any(item => file.StartsWith(item) || file.EndsWith(item));
}
void BuildReplacements(string projectName = ""){
    if (string.IsNullOrEmpty(projectName)) return;
    if (string.Equals(applicationName, projectName, StringComparison.InvariantCultureIgnoreCase)) return;
    fileContentReplacements.Add(Tuple.Create(@"App.Application\App.Application.csproj","<Product>Clean Bootstrap- Application Layer</Product>",$"<Product>{projectName} - Application Layer</Product>"));
    fileContentReplacements.Add(Tuple.Create(@"App.Domain\App.Domain.csproj","<Product>Clean Bootstrap - Domain Layer</Product>",$"<Product>{projectName} - Domain Layer</Product>"));
    fileContentReplacements.Add(Tuple.Create(@"App.Infrastructure\App.Infrastructure.csproj","<Product>Clean Bootstrap Infrastructure Layer</Product>",$"<Product>{projectName} - Infrastructure Layer</Product>"));
    fileContentReplacements.Add(Tuple.Create(@"App.Services\App.Services.csproj","<Product>Clean Bootstrap</Product>",$"<Product>{projectName} - API Layer</Product>"));
	fileContentReplacements.Add(Tuple.Create(@"App.Services.Common\App.Services.Common.csproj","<Product>Clean Bootstrap  Core Library</Product>",$"<Product>{projectName} - API Layer</Product>"));
}

var totalFiles = 0;
var totalAddedFiles = 0;
var totalReplacedFiles = 0;
var totalRemovedFiles = 0;
var totalConflictFiles = 0;
void UpdateFiles(List<Tuple<string, string>> files, Dictionary<string, string> currentHashes, bool alwaysCopy)
{
    Dictionary<string, string> previousHashes = ReadHashesFromFile(currentHashFile);    
    
    List<string> changes = new List<string>();
    List<string> conflicts = new List<string>();
    List<string> traces = new List<string>();
    totalFiles = files.Count;
    var i = 1;
    DrawTextProgressBar(i, totalFiles);

    foreach (var file in files)
    {
        DrawTextProgressBar(i++, totalFiles);
        var sourceFile = file.Item1;
        var currentFile = file.Item2;
        traces.Add($"Updating {currentFile}");
        
        var currentFileHash = currentHashes[currentFile];
        var previousFileExists = previousHashes.ContainsKey(currentFile);
        var previousFileHash = "";
        if(previousFileExists)
        {
            if(IsFileOnlyForAdd(currentFile))
            {
                currentHashes[currentFile] = previousHashes[currentFile];
                traces.Add("     PThis file is not available for update");
                continue;
            }
            previousFileHash = previousHashes[currentFile];
            traces.Add($"     Previous version: {previousFileHash}");
        }

        if(previousFileExists && previousFileHash != currentFileHash)        
            traces.Add($"     Current version: {currentFileHash}");
        else 
            traces.Add($"     Current version: {currentFileHash}");
        
        var theirFileExists = File.Exists(currentFile);
        var theirFileHash = "";
        if(theirFileExists)
        {
            theirFileHash = CreateHash(currentFile);
            if(previousFileExists && previousFileHash != theirFileHash)        
                traces.Add($"     Client's version: {theirFileHash}");
            else 
                traces.Add($"     Client's version: {theirFileHash}");
        }

        if(!theirFileExists)
        {
            traces.Add("     Client's file doesn't exist");
            changes.Add($"{currentFile} - ADDED");
            CopyFile(Path.Combine(root, sourceFile), currentFile);
            totalAddedFiles++;
            continue;
        }

        if((theirFileHash == currentFileHash)
            || (previousFileExists && currentFileHash == previousFileHash)) 
        {
            if(alwaysCopy)
            {
                CopyFile(Path.Combine(root, sourceFile), currentFile);
                totalReplacedFiles++;
                changes.Add($"{currentFile} - REPLACED");
                continue;                
            }
            
            traces.Add("     There is no changes");
            continue;
        }
        
        if(previousFileExists && previousFileHash == theirFileHash && previousFileHash != currentFileHash)
        {
            traces.Add("     The server's file has been changed");
            changes.Add($"{currentFile} - REPLACED");
            totalReplacedFiles++;
            CopyFile(Path.Combine(root, sourceFile), currentFile);
            continue;           
        }

        if((previousFileExists && previousFileHash != theirFileHash && previousFileHash != currentFileHash)
            || (!previousFileExists && theirFileHash != currentFileHash))
        {
            traces.Add("     CONFLICT!!! The server's and client's files were changed");
            changes.Add($"{currentFile} - REPLACED WITH CONFLICTS");
            conflicts.Add($"{currentFile} - REPLACED WITH CONFLICTS");
            CopyFile(Path.Combine(root, sourceFile), currentFile);
            totalConflictFiles++;
            continue;
        }
    }
    
    var removedFiles = previousHashes.Keys.Where(ph => !currentHashes.ContainsKey(ph));
    foreach (var file in removedFiles)
    {
        if(!File.Exists(file)) continue;
        
        traces.Add($"Removing file {file}");
        
        var theirFileHash = CreateHash(file);
        var previousFileHash = previousHashes[file];
        if(theirFileHash == previousFileHash)
        {
            changes.Add($"{file} - REMOVED");
            DeleteFile(file);
            totalRemovedFiles++;
            continue;         
        }
        
        traces.Add("     CONFLICT!!! The client's file had some changes");
        changes.Add($"{file} - REMOVED WITH CONFLICTS");
        conflicts.Add($"{file} - REMOVED WITH CONFLICTS");
        DeleteFile(file);
    }
    
    var time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    if(!changes.Any())
        changes.Add("All is up to date");
    WriteToFile(changesLogFile.Replace("time", time), changes);
    
    if(traces.Any()){
        WriteToFile(trackLogFile.Replace("time", time), traces);
    }

    if(conflicts.Any())
        WriteToFile(conflictsLogFile.Replace("time", time), conflicts); 
}


List<Tuple<string, string>> GetUpdateFiles(List<string> folders)
{
    folders.Reverse();
    
    var foldersStack = new Stack<Tuple<string, string>>(folders.Select(f => Tuple.Create(f, f)));
        
    var files = new List<Tuple<string, string>>();
    
    while(foldersStack.Any())
    {
        // Item1 - source path
        // Item2 - destination path
        
        var currentFolder = foldersStack.Pop();
        currentFolder = CalcSourcePath(currentFolder);
        
        if(IsIgnoredFolder(currentFolder.Item2)) continue;
        
        var directorysFiles = GetFilesFromDirectory(currentFolder);
        
        if(directorysFiles.Any())
            files.AddRange(directorysFiles);

        var subfolders = GetSubfolders(currentFolder);
        subfolders.Reverse();
        subfolders.ForEach(s => foldersStack.Push(s));
    }

    return files;
}

Dictionary<string, string> CreateHashesForSourceFiles(List<Tuple<string, string>> files)
{
    return files.ToDictionary(f => f.Item2, f => CreateHash(Path.Combine(root, f.Item1)));
}

void UpdateHashes(Dictionary<string, string> fileHashes)
{
    var fileHashesFormatted = fileHashes.Keys
        .Select(f=> $"{{{f}}}-{{{fileHashes[f]}}}")
        .ToList();
    
    MoveFile(currentHashFile, previousHashFile);
    WriteToFile(currentHashFile, fileHashesFormatted);
}

List<Tuple<string, string>> GetSubfolders(Tuple<string, string> folder)
{
    string sourceFolder = folder.Item1;
    string destinationFolder = folder.Item2;
    
    if(ContainsFileTemplate(sourceFolder)) return new List<Tuple<string, string>>();
    
    if(sourceFolder.EndsWith(@"\*"))
    {
        sourceFolder = sourceFolder.Replace(@"\*", "");
        destinationFolder = destinationFolder.Replace(@"\*", "");        
    }
    
    DirectoryInfo directory = new DirectoryInfo(Path.Combine(root, sourceFolder));
    return directory.GetDirectories()
        .Select(d => Tuple.Create(Path.Combine(sourceFolder, d.Name), Path.Combine(destinationFolder, d.Name))).ToList();
}

Tuple<string, string> CalcSourcePath(Tuple<string, string> folder)
{
    if(!sourceFolders.ContainsKey(folder.Item2)) return folder;

    return Tuple.Create(sourceFolders[folder.Item2], folder.Item2);
}

List<Tuple<string, string>> GetFilesFromDirectory(Tuple<string, string> folder)
{
    if(folder.Item1.EndsWith(@"\*")) return new List<Tuple<string, string>>();
    
    var fileTemplate = "*.*";
    var sourceFolder = folder.Item1;
    var destinationFolder = folder.Item2;

    if(ContainsFileTemplate(sourceFolder))
    {
        fileTemplate = Path.GetFileName(sourceFolder);
        sourceFolder = Path.GetDirectoryName(sourceFolder);
        destinationFolder = Path.GetDirectoryName(destinationFolder);
    }
    DirectoryInfo directory = new DirectoryInfo(Path.Combine(root, sourceFolder));
    FileInfo[] files = directory.GetFiles(fileTemplate);
    
    return files.Select(f => 
    {
        if(string.IsNullOrEmpty(sourceFolder)) return Tuple.Create(f.Name, f.Name);
        return Tuple.Create($@"{sourceFolder}\{f.Name}", $@"{destinationFolder}\{f.Name}");
    }).ToList();
}

bool IsIgnoredFolder(string path)
{
    if(ContainsFileTemplate(path))
        path = Path.GetDirectoryName(path);

    if(string.IsNullOrEmpty(path)) return false;
    
    DirectoryInfo directory = new DirectoryInfo(path);
    var folderName = directory.Name;
    
    return ignoreFolders.Contains(path) || ignoreFolders.Contains(folderName);
}

bool ContainsFileTemplate(string path)
{
    return path.EndsWith(@"*.*");        
}

string CreateHash(string path)
{
    using (var md5 = MD5.Create())
    using (var stream = File.OpenRead(path))
    {
        return Convert.ToBase64String(md5.ComputeHash(stream));
    }
}

void CopyFile(string source, string destination)
{
    var sourceFile = Path.GetFileName(source);
    var sourceFolder = Path.GetDirectoryName(source);
    
    FileInfo fileInfo = new FileInfo(destination);
    if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();
    
    //WriteDebugMessage($"     Copy file from {source} to {destination}");
    
    File.Copy(source, destination, true);
}

void MoveFile(string source, string destination)
{
    if(!File.Exists(source)) return;
    
    if (File.Exists(destination))
    {	
        File.Delete(destination);
    }          
    
    FileInfo fileInfo = new FileInfo(destination);
    if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();
    
    File.Move(source, destination);
}

void DeleteFile(string path)
{
    if(!File.Exists(path)) return;
    
    File.Delete(path);
    WriteWarningMessage($"    {path} has been removed");    
}

void WriteToFile(string path, List<string> lines)
{
    FileInfo fileInfo = new FileInfo(path);
    if (!fileInfo.Directory.Exists) fileInfo.Directory.Create();
    
    File.WriteAllLines(path, lines);
}

Dictionary<string, string> ReadHashesFromFile(string path)
{
    if(!File.Exists(path)) return new Dictionary<string, string>();
	Regex regex = new Regex(@"^{(.*)}-{(.*)}$", RegexOptions.IgnoreCase);
	return File.ReadLines(path)
        .Select(line=>{	
           Match match = regex.Match(line);
           if(!match.Success) return new KeyValuePair<string, string>();
           return new KeyValuePair<string, string>(match.Groups[1].Value, match.Groups[2].Value);
        })
        .Where(keyValue => !string.IsNullOrEmpty(keyValue.Key))
        .ToDictionary(keyValue => keyValue.Key, keyValue => keyValue.Value);
}

void WriteInfoMessage(string message, params string[] args)
{
    Console.WriteLine(message, args);
}
void DrawTextProgressBar(int progress, int total)
{
    int totalChunks = 30;
    //draw empty progress bar
    Console.CursorLeft = 0;
    Console.Write("["); //start
    Console.CursorLeft = totalChunks + 1;
    Console.Write("]"); //end
    Console.CursorLeft = 1;

    double pctComplete = Convert.ToDouble(progress) / total;
    int numChunksComplete = Convert.ToInt16(totalChunks * pctComplete);

    //draw completed chunks
    Console.BackgroundColor = ConsoleColor.Magenta;
    Console.Write("".PadRight(numChunksComplete));

    //draw incomplete chunks
    Console.BackgroundColor = ConsoleColor.Gray;
    Console.Write("".PadRight(totalChunks - numChunksComplete));

    //draw totals
    Console.CursorLeft = totalChunks + 5;
    Console.BackgroundColor = ConsoleColor.Black;

    string output = progress.ToString() + " of " + total.ToString() + " files processed";
    Console.Write(output.PadRight(15)); 

}
void WriteDebugMessage(string message, params string[] args)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(message, args);
    Console.ResetColor();
}
void WriteSuccessMessage(string message, params string[] args)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(message, args);
    Console.ResetColor();
}
void WriteWarningMessage(string message, params string[] args)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(message, args);
    Console.ResetColor();
}

void WriteErrorMessage(string message, params string[] args)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message, args);
    Console.ResetColor();
}
void WriteHighlightMessage(string message, params string[] args)
{
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine(message, args);
    Console.ResetColor();
}
[Step] void BootStrapMessage()
{
     Console.ForegroundColor = ConsoleColor.Magenta;
		Console.WriteLine(
    @"
                                                                     
   _________  .__                            __________                  __            __                                          
   \_   ___ \ |  |    ____  _____     ____   \______   \  ____    ____ _/  |_  _______/  |_ _______ _____   ______    ____ _______ 
   /    \  \/ |  |  _/ __ \ \__  \   /    \   |    |  _/ /  _ \  /  _ \\   __\/  ___/\   __\\_  __ \\__  \  \____ \ _/ __ \\_  __ \
   \     \____|  |__\  ___/  / __ \_|   |  \  |    |   \(  <_> )(  <_> )|  |  \___ \  |  |   |  | \/ / __ \_|  |_> >\  ___/ |  | \/
    \______  /|____/ \___  >(____  /|___|  /  |______  / \____/  \____/ |__| /____  > |__|   |__|   (____  /|   __/  \___  >|__|   
           \/            \/      \/      \/          \/                           \/                     \/ |__|         \/                                        
                                                                                       
                                                                                   ");
                Console.ResetColor();	
}
[Step] void BootStrapEndMessage()
{
    WriteDebugMessage("");
    WriteDebugMessage("");
    WriteSuccessMessage("Hey! The Clean Bootstrap has been completed!");
    WriteDebugMessage("");   
    WriteInfoMessage($"  {totalFiles} File(s) processed");
    WriteInfoMessage($"  {totalAddedFiles} File(s) added");
    WriteInfoMessage($"  {totalReplacedFiles} File(s) replaced");
    WriteInfoMessage($"  {totalRemovedFiles} File(s) removed");
    WriteInfoMessage($"  {totalConflictFiles} File(s) with conflicts");
    WriteDebugMessage(""); 
    WriteDebugMessage("Please check the logs file for more detailed information on the execution results.");
    WriteDebugMessage("For any further assistance/feedback please contact me at arasines@hotmail.com.");
    WriteDebugMessage("Have a great day!");
    WriteDebugMessage("");   
    Console.ForegroundColor = ConsoleColor.Magenta;
	Console.WriteLine(
    @"
     ___ ___                                  _________              .___.__                 
    /   |   \ _____   ______  ______  ___.__. \_   ___ \   ____    __| _/|__|  ____    ____  
   /    ~    \\__  \  \____ \ \____ \<   |  | /    \  \/  /  _ \  / __ | |  | /    \  / ___\ 
   \    Y    / / __ \_|  |_> >|  |_> >\___  | \     \____(  <_> )/ /_/ | |  ||   |  \/ /_/  >
    \___|_  / (____  /|   __/ |   __/ / ____|  \______  / \____/ \____ | |__||___|  /\___  / 
          \/       \/ |__|    |__|    \/              \/              \/          \//_____/      
                                                                                     ");
                Console.ResetColor();	
    
    WriteDebugMessage("");

}