using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SnapshotCLI.Core;

namespace SnapshotCLI.Server
{
    class Program
    {
        static TcpListener listener = null!;
        static LockManager lockManager = null!;

        static async Task Main(string[] args)
        {
            string storageDir = Path.Combine(AppContext.BaseDirectory, "../../../Storage");
            if (!Directory.Exists(storageDir)) Directory.CreateDirectory(storageDir);
            lockManager = new LockManager(storageDir);

            listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("[Server] SnapshotCLI Enterprise Server Started on Port 5000...");
            Console.WriteLine("[Server] Lock Manager Active. Waiting for connections...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client); 
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var packet = NetworkPacket.FromBytes(buffer[0..bytesRead]);
                    if (packet == null) continue;

                    // Extract the Dual-Identity
                    string mId = string.IsNullOrWhiteSpace(packet.MachineId) ? "UNKNOWN_ID" : packet.MachineId;
                    string dName = string.IsNullOrWhiteSpace(packet.DisplayName) ? "Anonymous" : packet.DisplayName;

                    var response = new NetworkPacket { Command = CommandType.Error, Payload = "Unknown Command" };

                    switch (packet.Command)
                    {
                        case CommandType.LockFile:
                            string lockKey = $"{packet.Project}/{packet.Branch}/{packet.Payload}";
                            bool locked = lockManager.TryLock(lockKey, mId); // MachineId dictates the lock
                            
                            response.Command = CommandType.LockFile;
                            if (locked)
                            {
                                response.Payload = $"SUCCESS: '{packet.Payload}' is now locked by your machine.";
                                Console.WriteLine($"[Lock] {dName} ({mId.Substring(0,8)}...) locked {lockKey}");
                            }
                            else
                            {
                                response.Payload = $"DENIED: '{packet.Payload}' is currently locked by another machine.";
                                Console.WriteLine($"[Lock Denied] {dName} tried to lock {lockKey}");
                            }
                            break;

                        case CommandType.UnlockFile:
                            string unlockKey = $"{packet.Project}/{packet.Branch}/{packet.Payload}";
                            bool unlocked = lockManager.TryUnlock(unlockKey, mId); 
                            
                            response.Command = CommandType.UnlockFile;
                            if (unlocked)
                            {
                                response.Payload = $"SUCCESS: '{packet.Payload}' has been unlocked.";
                                Console.WriteLine($"[Unlock] {dName} unlocked {unlockKey}");
                            }
                            else
                            {
                                response.Payload = $"DENIED: You do not own the lock for '{packet.Payload}'.";
                            }
                            break;

                        case CommandType.UploadFile:
                            string safePayload = packet.Payload ?? ""; 
                            int dividerPosition = safePayload.IndexOf('|');
                            
                            if (dividerPosition != -1)
                            {
                                string fileName = safePayload.Substring(0, dividerPosition);
                                string content = safePayload.Substring(dividerPosition + 1);
                                string fileKey = $"{packet.Project}/{packet.Branch}/{fileName}";

                                // Security verification using MachineId
                                if (!lockManager.CanModify(fileKey, mId))
                                {
                                    response.Command = CommandType.Error;
                                    response.Payload = $"UPLOAD DENIED: '{fileName}' is locked by another user.";
                                    Console.WriteLine($"[Security] Blocked upload from {dName} on {fileKey}.");
                                    break; 
                                }

                                string savePath = GetSafePath(packet.Project, packet.Branch, fileName);
                                byte[] fileBytes = Convert.FromBase64String(content);
                                await File.WriteAllBytesAsync(savePath, fileBytes);

                                response.Command = CommandType.UploadFile;
                                response.Payload = "Upload Successful";
                                Console.WriteLine($"[Success] {dName} updated: {fileKey}");
                            }
                            else
                            {
                                response.Payload = "Invalid payload format. Missing '|'.";
                            }
                            break;

                        case CommandType.ListFiles:
                            string targetDir = GetSafePath(packet.Project, packet.Branch);
                            
                            if (Directory.Exists(targetDir))
                            {
                                // Get just the file names, not the massive full C:/ paths
                                var files = Directory.GetFiles(targetDir).Select(Path.GetFileName).ToArray();
                                response.Command = CommandType.ListFiles;
                                
                                // Return the list, or a friendly message if it's empty
                                response.Payload = files.Length > 0 ? string.Join("\n", files) : "Vault is empty.";
                                Console.WriteLine($"[Vault] {dName} requested the file list.");
                            }
                            else
                            {
                                response.Command = CommandType.Error;
                                response.Payload = "Branch directory does not exist yet.";
                            }
                            break;
                    }

                    byte[] responseBytes = response.ToBytes();
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Client Disconnected: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        static string GetSafePath(string? project, string? branch, string filename = "")
        {
            string baseDir = Path.Combine(AppContext.BaseDirectory, "../../../Storage");
            string safeProject = Path.GetFileName(project ?? "Default"); 
            string safeBranch = Path.GetFileName(branch ?? "Main");
            string safeFile = Path.GetFileName(filename);

            string fullPath = Path.Combine(baseDir, safeProject, safeBranch);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            if (!string.IsNullOrEmpty(safeFile)) fullPath = Path.Combine(fullPath, safeFile);

            return fullPath;
        }
    }
}