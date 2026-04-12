using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SnapshotCLI.Core;

namespace SnapshotCLI.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===================================");
            Console.WriteLine("       SNAPSHOT CLI - ENTERPRISE   ");
            Console.WriteLine("===================================");

            // --- ZERO-COMPROMISE IDENTITY GENERATION ---
            string profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".snapshotcli_id");
            string machineId;
            
            if (File.Exists(profilePath))
            {
                machineId = File.ReadAllText(profilePath).Trim();
            }
            else
            {
                machineId = Guid.NewGuid().ToString(); // Generate a unique ID
                File.WriteAllText(profilePath, machineId); // Save it permanently
            }

            string displayName = Environment.UserName; 
            Console.WriteLine($"[Identity] Display Name: {displayName}");
            Console.WriteLine($"[Identity] Machine ID: {machineId}");

            // --- NETWORK CONNECTION ---
            Console.Write("\nEnter Server IP (leave blank for localhost): ");
            string ipInput = Console.ReadLine() ?? "";
            string serverIp = string.IsNullOrWhiteSpace(ipInput) ? "127.0.0.1" : ipInput;

            try 
            {
                using var client = new TcpClient();
                await client.ConnectAsync(serverIp, 5000);
                Console.WriteLine("[Client] Connection Secured!\n");

                using var stream = client.GetStream();

                while(true) 
                {
                    Console.WriteLine("\nOptions: Lock File  Unlock File  Upload File  Exit");
                    Console.Write("Select an action: ");
                    string choice = Console.ReadLine() ?? "";
                    if (choice == "4") break;

                    Console.Write("Enter File Name (e.g., test.txt): ");
                    string fileName = Console.ReadLine() ?? "test.txt";

                    var packet = new NetworkPacket 
                    { 
                        MachineId = machineId,
                        DisplayName = displayName,
                        Project = "SnapshotCLI", 
                        Branch = "Main"
                    };

                    if (choice == "1") 
                    {
                        packet.Command = CommandType.LockFile;
                        packet.Payload = fileName;
                    }
                    else if (choice == "2")
                    {
                        packet.Command = CommandType.UnlockFile;
                        packet.Payload = fileName;
                    }
                    else if (choice == "3")
                    {
                        Console.Write("Enter text to put in file: ");
                        string content = Console.ReadLine() ?? "Empty";
                        string base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
                        
                        packet.Command = CommandType.UploadFile;
                        packet.Payload = $"{fileName}|{base64Content}";
                    }
                    else 
                    {
                        continue;
                    }

                    // Send Packet
                    byte[] packetBytes = packet.ToBytes();
                    await stream.WriteAsync(packetBytes, 0, packetBytes.Length);
                    
                    // Receive Server Response
                    byte[] buffer = new byte[4096];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        var response = NetworkPacket.FromBytes(buffer[0..bytesRead]);
                        if (response != null)
                        {
                            Console.WriteLine($"\n>> SERVER REPLY: {response.Payload}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error] Could not connect: {ex.Message}");
            }
        }
    }
}